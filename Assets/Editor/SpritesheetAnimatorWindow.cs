using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Video;

public class SpritesheetAnimatorWindow : EditorWindow
{
    // ── Tabs ─────────────────────────────────────────────────────────
    private int _tab; // 0 = Create Spritesheet from Video, 1 = Spritesheet Editor
    private static readonly string[] TabLabels = { "Create Spritesheet from Video", "Spritesheet Editor" };

    // ── Spritesheet ──────────────────────────────────────────────────
    private Texture2D _sheet;
    private Sprite[]  _sprites;          // all sprites in atlas order (top-left → bottom-right)
    private Sprite[]  _mirroredSpritesCache;

    // ── Animations defined against the current spritesheet ─────────────
    [System.Serializable]
    private class SpriteAnimation
    {
        public string    name = "Animation";
        public int        fps = 12;
        public bool       loop = true;
        public bool       mirrorH;
        public List<int>  frames = new();

        // Always-on gallery playback (independent of the big detail preview below)
        [System.NonSerialized] public int    galleryPos;
        [System.NonSerialized] public double galleryNextTime;
    }

    private List<SpriteAnimation> _animations = new();
    private int                   _activeAnim = -1;
    private Vector2                _animListScroll;

    // ── Detail preview (explicit play/pause, scrubbable) ────────────────
    private bool   _playing;
    private double _nextFrameTime;
    private int    _previewPos;          // index into the active animation's frames

    // ── UI ────────────────────────────────────────────────────────────
    private Vector2 _gridScroll;
    private const int THUMB = 76;
    private const int PAD   = 4;
    private static readonly Color ColSelected   = new(0.25f, 0.55f, 1f,  0.55f);
    private static readonly Color ColUnselected = new(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color ColPreview    = new(0.1f,  0.1f,  0.1f, 1f);
    private static readonly Color ColActiveTile = new(0.2f,  0.15f, 0f,   1f);

    // ── Video → Spritesheet ──────────────────────────────────────────
    // Delegates to the .agents/skills/extract_spritesheet_from_video Python script
    // (opencv-based) instead of driving a VideoPlayer in-editor, which is unreliable
    // outside Play Mode.
    private VideoClip _videoClip;
    private int        _frameCount     = 256;
    private int        _cols           = 16;
    private bool        _pointFilter   = true;
    private string       _videoOutFolder = "Assets/Art/Characters/AnimationSpritesheets";
    private string       _videoSheetName = "NewSpritesheet";

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Spritesheet Animator")]
    public static void Open()
    {
        var w = GetWindow<SpritesheetAnimatorWindow>("Spritesheet Animator");
        w.minSize = new Vector2(560, 720);
    }

    void OnEnable()  => EditorApplication.update += Tick;
    void OnDisable()
    {
        EditorApplication.update -= Tick;
        _playing = false;
    }

    // ── Playback tick ─────────────────────────────────────────────────
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;

        // Big detail preview — active animation only, explicit play/pause
        if (_playing && _activeAnim >= 0 && _activeAnim < _animations.Count)
        {
            var anim = _animations[_activeAnim];
            if (anim.frames.Count > 0 && now >= _nextFrameTime)
            {
                _previewPos++;
                if (_previewPos >= anim.frames.Count)
                {
                    if (anim.loop) _previewPos = 0;
                    else           { _previewPos = anim.frames.Count - 1; _playing = false; }
                }
                _nextFrameTime = now + 1.0 / Mathf.Max(1, anim.fps);
            }
        }

        // Gallery — every defined animation always auto-plays its own loop
        bool galleryDirty = false;
        foreach (var a in _animations)
        {
            if (a.frames.Count == 0) continue;
            if (now < a.galleryNextTime) continue;
            a.galleryPos      = (a.galleryPos + 1) % a.frames.Count;
            a.galleryNextTime = now + 1.0 / Mathf.Max(1, a.fps);
            galleryDirty = true;
        }

        if (_playing || galleryDirty) Repaint();
    }

    // ── Main GUI ──────────────────────────────────────────────────────
    void OnGUI()
    {
        GUILayout.Space(4);
        _tab = GUILayout.Toolbar(_tab, TabLabels, GUILayout.Height(24));
        GUILayout.Space(4);

        if (_tab == 0) DrawVideoTab();
        else           DrawEditorTab();
    }

    void DrawEditorTab()
    {
        DrawTopBar();

        if (_sprites == null || _sprites.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Pick a spritesheet texture imported as Sprite Mode → Multiple.",
                MessageType.Info);
            return;
        }

        GUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
                DrawAnimationsList();

            using (new EditorGUILayout.VerticalScope())
                DrawAnimationEditor();
        }

        GUILayout.Space(6);
        DrawAllAnimationsGallery();
    }

    // ── Video → Spritesheet tab ─────────────────────────────────────────
    void DrawVideoTab()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Source Video", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _videoClip = (VideoClip)EditorGUILayout.ObjectField("Video Clip", _videoClip, typeof(VideoClip), false);
        if (EditorGUI.EndChangeCheck() && _videoClip != null)
            _videoSheetName = $"{_videoClip.name}_spritesheet";

        _frameCount = Mathf.Max(1, EditorGUILayout.IntField("Frames", _frameCount));
        _cols       = Mathf.Max(1, EditorGUILayout.IntField("Columns", _cols));
        GUILayout.Label($"→ {Mathf.CeilToInt((float)_frameCount / _cols)} rows", EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Presets", GUILayout.Width(60));
            if (GUILayout.Button("256 (16×16)"))          { _frameCount = 256; _cols = 16; }
            if (GUILayout.Button("48 (8×6) — 6-phase"))   { _frameCount = 48;  _cols = 8;  }
        }

        _pointFilter = EditorGUILayout.Toggle("Pixel-Perfect (Point Filter)", _pointFilter);

        GUILayout.Space(6);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _videoOutFolder = EditorGUILayout.TextField("Folder", _videoOutFolder);
            if (GUILayout.Button("…", GUILayout.Width(26)))
            {
                string picked = EditorUtility.OpenFolderPanel("Output folder", _videoOutFolder, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    string full = Path.GetFullPath(picked).Replace('\\', '/');
                    string proj = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/') + "/";
                    if (full.StartsWith(proj)) _videoOutFolder = full.Substring(proj.Length).TrimEnd('/');
                }
            }
        }
        _videoSheetName = EditorGUILayout.TextField("Sheet Name", _videoSheetName);

        GUILayout.Space(8);

        if (_videoClip == null)
        {
            EditorGUILayout.HelpBox("Assign a video clip to extract frames from.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Extract Frames → Spritesheet", GUILayout.Height(32)))
            RunVideoExtraction();

        EditorGUILayout.HelpBox(
            "Runs .agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py " +
            "(requires Python on PATH with opencv-python, pillow, numpy installed).", MessageType.None);
    }

    void RunVideoExtraction()
    {
        string videoRelPath = AssetDatabase.GetAssetPath(_videoClip);
        if (string.IsNullOrEmpty(videoRelPath))
        {
            Debug.LogError("[SpritesheetAnimator] Could not resolve an asset path for the selected video clip.");
            return;
        }

        string safe = string.Concat(_videoSheetName.Split(Path.GetInvalidFileNameChars()));
        if (!Directory.Exists(_videoOutFolder)) Directory.CreateDirectory(_videoOutFolder);
        string outRelPath = $"{_videoOutFolder}/{safe}.png";

        string projectRoot = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
        string scriptPath  = Path.GetFullPath(Path.Combine(projectRoot, ".agents/skills/extract_spritesheet_from_video/scripts/extract_spritesheet.py"));

        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"[SpritesheetAnimator] Extraction script not found: {scriptPath}");
            return;
        }

        string args = $"\"{scriptPath}\" --video \"{videoRelPath}\" --out \"{outRelPath}\" --frames {_frameCount} --cols {_cols}";

        string stdout, stderr;
        int    exitCode;

        EditorUtility.DisplayProgressBar("Extracting Spritesheet", $"Sampling {_frameCount} frame(s) from '{_videoClip.name}'…", 0.5f);
        bool started;
        try
        {
            started = TryRunPython(args, projectRoot, out stdout, out stderr, out exitCode);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!started)
        {
            Debug.LogError("[SpritesheetAnimator] Could not find a Python interpreter on PATH (tried 'python' and 'py'). " +
                            "Install Python and the extraction script's dependencies: uv pip install opencv-python pillow numpy");
            return;
        }

        if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[SpritesheetAnimator]\n{stdout.Trim()}");

        if (exitCode != 0)
        {
            Debug.LogError($"[SpritesheetAnimator] Extraction failed (exit {exitCode}):\n{stderr}");
            return;
        }

        var match = Regex.Match(stdout, @"Extracted (\d+) frames");
        if (!match.Success)
        {
            Debug.LogError($"[SpritesheetAnimator] Could not parse extracted frame count from script output:\n{stdout}");
            return;
        }
        int actualFrameCount = int.Parse(match.Groups[1].Value);

        AssetDatabase.ImportAsset(outRelPath, ImportAssetOptions.ForceSynchronousImport);

        // Read the true on-disk pixel size directly from the PNG header. Trusting the
        // just-imported Texture2D here is unsafe: the very first import happens with
        // Unity's default (non-Sprite) settings, which can silently NPOT-rescale the
        // texture — computing the slice grid from that scaled size misaligns every rect.
        string outAbsPath = Path.Combine(projectRoot, outRelPath);
        if (!TryReadPngSize(outAbsPath, out int fileW, out int fileH))
        {
            Debug.LogError($"[SpritesheetAnimator] Could not read PNG dimensions from {outRelPath}");
            return;
        }

        int cols  = _cols;
        int rows  = Mathf.CeilToInt((float)actualFrameCount / cols);
        int cellW = fileW / cols;
        int cellH = fileH / rows;

        var importer = (TextureImporter)AssetImporter.GetAtPath(outRelPath);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.filterMode          = _pointFilter ? FilterMode.Point : FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.npotScale           = TextureImporterNPOTScale.None;
        importer.maxTextureSize      = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(fileW, fileH)), 32, 8192);

        var metas = new SpriteMetaData[actualFrameCount];
        for (int i = 0; i < actualFrameCount; i++)
        {
            int col = i % cols;
            int row = i / cols;
            metas[i] = new SpriteMetaData
            {
                name      = $"{safe}_{i:D3}",
                rect      = new Rect(col * cellW, (rows - 1 - row) * cellH, cellW, cellH),
                alignment = (int)SpriteAlignment.Center,
                pivot     = new Vector2(0.5f, 0.5f),
            };
        }
        ApplySpriteMetaData(importer, metas);
        importer.SaveAndReimport();

        var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(outRelPath);
        if (texAsset == null)
        {
            Debug.LogError($"[SpritesheetAnimator] Expected output spritesheet not found: {outRelPath}");
            return;
        }

        Debug.Log($"[SpritesheetAnimator] Sliced {actualFrameCount} frame(s) → {outRelPath} ({cols}×{rows} grid, {cellW}×{cellH} cells)");

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = texAsset;
        EditorGUIUtility.PingObject(texAsset);

        // Hand off straight into the editor tab
        _sheet = texAsset;
        LoadSprites();
        _tab = 1;
    }

    /// <summary>
    /// Applies a spritesheet slice via ISpriteEditorDataProvider — TextureImporter.spritesheet
    /// was removed, this is the replacement path. Caller must still call importer.SaveAndReimport()
    /// afterward, same as the old spritesheet-assignment flow required.
    /// </summary>
    static void ApplySpriteMetaData(TextureImporter importer, SpriteMetaData[] metas)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var spriteRects = new SpriteRect[metas.Length];
        for (int i = 0; i < metas.Length; i++)
        {
            SpriteMetaData m = metas[i];
            spriteRects[i] = new SpriteRect
            {
                name      = m.name,
                rect      = m.rect,
                alignment = (SpriteAlignment)m.alignment,
                pivot     = m.pivot,
                border    = m.border,
                spriteID  = GUID.Generate(),
            };
        }
        dataProvider.SetSpriteRects(spriteRects);
        dataProvider.Apply();
    }

    /// <summary>Reads width/height straight from the PNG IHDR chunk, bypassing Unity's importer entirely.</summary>
    static bool TryReadPngSize(string absolutePath, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using var stream = File.OpenRead(absolutePath);
            var header = new byte[24];
            if (stream.Read(header, 0, 24) != 24) return false;

            // 8-byte PNG signature, then a 4-byte length + "IHDR", then 4-byte width + 4-byte height (big-endian)
            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47) return false;

            width  = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TryRunPython(string args, string workingDir, out string stdout, out string stderr, out int exitCode)
    {
        foreach (var exe in new[] { "python", "py" })
        {
            if (TryStartProcess(exe, args, workingDir, out stdout, out stderr, out exitCode))
                return true;
        }
        stdout = stderr = null;
        exitCode = -1;
        return false;
    }

    static bool TryStartProcess(string exe, string args, string workingDir, out string stdout, out string stderr, out int exitCode)
    {
        stdout = stderr = null;
        exitCode = -1;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = exe,
            Arguments              = args,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(180000);
            exitCode = process.ExitCode;
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // interpreter not found under this name
        }
    }

    // ── Top bar ───────────────────────────────────────────────────────
    void DrawTopBar()
    {
        GUILayout.Space(6);
        EditorGUI.BeginChangeCheck();
        _sheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", _sheet, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck()) LoadSprites();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    // ── Animations list (left column) ───────────────────────────────────
    void DrawAnimationsList()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(24)))
                AddAnimation();
        }

        _animListScroll = EditorGUILayout.BeginScrollView(_animListScroll, GUILayout.ExpandHeight(true));

        int removeIndex = -1;
        for (int i = 0; i < _animations.Count; i++)
        {
            var  a        = _animations[i];
            bool isActive = i == _activeAnim;

            using (new EditorGUILayout.HorizontalScope())
            {
                Color prevBg = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                if (GUILayout.Button($"{a.name}\n{a.frames.Count}f · {a.fps}fps", GUILayout.Height(32)))
                    SelectAnimation(i);
                GUI.backgroundColor = prevBg;

                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(32)))
                    removeIndex = i;
            }
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0) RemoveAnimation(removeIndex);

        if (_animations.Count == 0)
            EditorGUILayout.HelpBox("Click + to define an animation from this spritesheet.", MessageType.Info);
    }

    void AddAnimation()
    {
        string baseName = "Animation";
        int    n        = _animations.Count + 1;
        string name;
        do { name = $"{baseName}{n:D2}"; n++; } while (_animations.Any(a => a.name == name));

        _animations.Add(new SpriteAnimation { name = name });
        SelectAnimation(_animations.Count - 1);
    }

    void SelectAnimation(int i)
    {
        _activeAnim = i;
        StopActivePreview();
    }

    void RemoveAnimation(int i)
    {
        _animations.RemoveAt(i);
        if (_activeAnim == i)      _activeAnim = -1;
        else if (_activeAnim > i)  _activeAnim--;
        StopActivePreview();
    }

    // ── Animation editor (right column) ─────────────────────────────────
    void DrawAnimationEditor()
    {
        if (_activeAnim < 0 || _activeAnim >= _animations.Count)
        {
            EditorGUILayout.HelpBox("Select an animation on the left, or click + to create one.", MessageType.Info);
            return;
        }

        var anim = _animations[_activeAnim];

        anim.name = EditorGUILayout.TextField("Name", anim.name);
        using (new EditorGUILayout.HorizontalScope())
        {
            anim.fps  = EditorGUILayout.IntSlider("FPS", anim.fps, 1, 60);
            anim.loop = EditorGUILayout.Toggle("Loop", anim.loop, GUILayout.Width(60));
        }
        anim.mirrorH = EditorGUILayout.Toggle("Mirror (swap left/right)", anim.mirrorH);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All"))
            {
                anim.frames = Enumerable.Range(0, _sprites.Length).ToList();
                OnSequenceChanged(anim);
            }
            if (GUILayout.Button("Clear All"))
            {
                anim.frames.Clear();
                StopActivePreview();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{anim.frames.Count} frame(s) selected", EditorStyles.miniLabel);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        DrawFrameGrid(anim);
        GUILayout.Space(6);
        DrawPreviewRow(anim);
        GUILayout.Space(6);
        DrawSaveRow(anim);
    }

    // ── Frame grid ────────────────────────────────────────────────────
    void DrawFrameGrid(SpriteAnimation anim)
    {
        EditorGUILayout.LabelField("Frames  (click to add/remove)", EditorStyles.boldLabel);

        float w    = position.width - 220;
        int   cols = Mathf.Max(1, (int)(w / (THUMB + PAD)));
        int   rows = Mathf.CeilToInt((float)_sprites.Length / cols);
        float h    = rows * (THUMB + PAD) + PAD + 4;

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(Mathf.Min(h, 300)));
        Rect content = GUILayoutUtility.GetRect(w, h);

        for (int i = 0; i < _sprites.Length; i++)
        {
            int  col  = i % cols;
            int  row  = i / cols;
            Rect cell = new(
                content.x + PAD + col * (THUMB + PAD),
                content.y + PAD + row * (THUMB + PAD),
                THUMB, THUMB);

            bool sel = anim.frames.Contains(i);

            // click
            if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
            {
                if (sel) anim.frames.Remove(i);
                else     anim.frames.Add(i);
                OnSequenceChanged(anim);
                Event.current.Use();
                Repaint();
            }

            // background
            EditorGUI.DrawRect(cell, sel ? ColSelected : ColUnselected);

            // sprite thumbnail
            DrawSprite(_sprites[i], Shrink(cell, 3), anim.mirrorH);

            // frame index
            DrawLabel(new Rect(cell.x, cell.yMax - 16, THUMB, 16), i.ToString(), TextAnchor.MiddleCenter, Color.white);

            // selection order badge
            if (sel)
            {
                int order = anim.frames.IndexOf(i);
                Rect badge = new(cell.xMax - 20, cell.y + 2, 18, 14);
                EditorGUI.DrawRect(badge, new Color(0.2f, 0.5f, 1f));
                DrawLabel(badge, (order + 1).ToString(), TextAnchor.MiddleCenter, Color.white);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Preview row ───────────────────────────────────────────────────
    void DrawPreviewRow(SpriteAnimation anim)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            // Large preview box
            const float PS = 180;
            Rect previewRect = GUILayoutUtility.GetRect(PS, PS, GUILayout.Width(PS), GUILayout.Height(PS));
            EditorGUI.DrawRect(previewRect, ColPreview);

            if (anim.frames.Count > 0)
            {
                int pi = Mathf.Clamp(_previewPos, 0, anim.frames.Count - 1);
                DrawSprite(_sprites[anim.frames[pi]], Shrink(previewRect, 6), anim.mirrorH);
            }

            // Controls column
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(12);
                int frameCount = anim.frames.Count;
                int display    = frameCount == 0 ? 0 : _previewPos + 1;
                GUILayout.Label($"Frame {display} / {frameCount}", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|◀", GUILayout.Width(30))) { StopActivePreview(); _previewPos = 0; Repaint(); }
                    if (GUILayout.Button("◀",  GUILayout.Width(30))) { StopActivePreview(); Step(anim, -1); }

                    if (_playing)
                    {
                        if (GUILayout.Button("■ Stop")) StopActivePreview();
                    }
                    else
                    {
                        GUI.enabled = frameCount > 0;
                        if (GUILayout.Button("▶ Play"))
                        {
                            _playing       = true;
                            _nextFrameTime = EditorApplication.timeSinceStartup;
                        }
                        GUI.enabled = true;
                    }

                    if (GUILayout.Button("▶",  GUILayout.Width(30))) { StopActivePreview(); Step(anim, 1); }
                    if (GUILayout.Button("▶|", GUILayout.Width(30))) { StopActivePreview(); _previewPos = Mathf.Max(0, frameCount - 1); Repaint(); }
                }

                GUILayout.Space(8);
                float duration = frameCount > 0 ? (float)frameCount / anim.fps : 0f;
                GUILayout.Label($"{frameCount} frames  ·  {duration:F2}s  ·  {anim.fps} fps", EditorStyles.miniLabel);
            }
        }
    }

    // ── Save row ──────────────────────────────────────────────────────
    void DrawSaveRow(SpriteAnimation anim)
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        string dest = $"Assets/Art/Characters/Animations/{_sheet.name}_{SafeName(anim.name)}.anim";
        EditorGUILayout.LabelField("Saves to", dest, EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = anim.frames.Count > 0 && !string.IsNullOrWhiteSpace(anim.name);
            if (GUILayout.Button($"Save '{anim.name}'  (.anim)", GUILayout.Height(30)))
                SaveClip(anim);
            GUI.enabled = true;

            GUI.enabled = _animations.Any(a => a.frames.Count > 0);
            if (GUILayout.Button("Save All Animations", GUILayout.Height(30)))
                SaveAllClips();
            GUI.enabled = true;
        }
    }

    // ── All-animations gallery (bottom) ─────────────────────────────────
    void DrawAllAnimationsGallery()
    {
        if (_animations.Count == 0) return;

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("All Animations", EditorStyles.boldLabel);

        const float TILE = 96;
        float w    = position.width - 20;
        int   cols = Mathf.Max(1, (int)(w / (TILE + PAD)));

        int i = 0;
        while (i < _animations.Count)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int c = 0; c < cols && i < _animations.Count; c++, i++)
                {
                    var anim     = _animations[i];
                    int capturedIndex = i;

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(TILE)))
                    {
                        Rect box = GUILayoutUtility.GetRect(TILE, TILE, GUILayout.Width(TILE), GUILayout.Height(TILE));

                        if (Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
                        {
                            SelectAnimation(capturedIndex);
                            Event.current.Use();
                            Repaint();
                        }

                        EditorGUI.DrawRect(box, capturedIndex == _activeAnim ? ColActiveTile : ColPreview);

                        if (anim.frames.Count > 0)
                        {
                            int pi = Mathf.Clamp(anim.galleryPos, 0, anim.frames.Count - 1);
                            DrawSprite(_sprites[anim.frames[pi]], Shrink(box, 4), anim.mirrorH);
                        }

                        GUILayout.Label(anim.name, EditorStyles.centeredGreyMiniLabel);
                        GUILayout.Label($"{anim.frames.Count}f · {anim.fps}fps", EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    void LoadSprites()
    {
        _sprites = null;
        _animations = new List<SpriteAnimation>();
        _activeAnim = -1;
        _mirroredSpritesCache = null;
        StopActivePreview();

        if (_sheet == null) return;

        string path = AssetDatabase.GetAssetPath(_sheet);
        _sprites = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderByDescending(s => s.textureRect.y)   // top row first
            .ThenBy(s => s.textureRect.x)              // left to right
            .ToArray();

        if (_sprites.Length == 0)
            Debug.LogWarning($"[SpritesheetAnimator] No sprites found in {path}. Make sure Sprite Mode is set to Multiple.");
    }

    void StopActivePreview() { _playing = false; _previewPos = 0; }

    void OnSequenceChanged(SpriteAnimation anim)
    {
        if (anim.frames.Count == 0) { StopActivePreview(); return; }
        if (_playing) _previewPos %= anim.frames.Count;
        else          _previewPos = 0;
    }

    void Step(SpriteAnimation anim, int dir)
    {
        if (anim.frames.Count == 0) return;
        _previewPos = (_previewPos + dir + anim.frames.Count) % anim.frames.Count;
        Repaint();
    }

    static string SafeName(string name) => string.Concat(name.Split(Path.GetInvalidFileNameChars()));

    static void DrawSprite(Sprite sprite, Rect rect, bool mirrorH = false)
    {
        if (sprite == null || sprite.texture == null) return;

        Rect  tr  = sprite.textureRect;
        float tw  = sprite.texture.width;
        float th  = sprite.texture.height;
        Rect  uv  = new(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
        if (mirrorH) uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);

        float sprAspect = tr.width / tr.height;
        float boxAspect = rect.width / rect.height;
        Rect  draw;
        if (sprAspect > boxAspect)
        {
            float hh = rect.width / sprAspect;
            draw = new Rect(rect.x, rect.y + (rect.height - hh) * 0.5f, rect.width, hh);
        }
        else
        {
            float ww = rect.height * sprAspect;
            draw = new Rect(rect.x + (rect.width - ww) * 0.5f, rect.y, ww, rect.height);
        }

        GUI.DrawTextureWithTexCoords(draw, sprite.texture, uv);
    }

    static void DrawLabel(Rect r, string text, TextAnchor anchor, Color color)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment          = anchor,
            normal = { textColor = color }
        };
        GUI.Label(r, text, style);
    }

    static Rect Shrink(Rect r, float px) =>
        new(r.x + px, r.y + px, r.width - px * 2, r.height - px * 2);

    void SaveAllClips()
    {
        int saved = 0;
        foreach (var anim in _animations)
        {
            if (anim.frames.Count == 0) continue;
            SaveClip(anim);
            saved++;
        }
        Debug.Log($"[SpritesheetAnimator] Saved {saved} animation(s).");
    }

    void SaveClip(SpriteAnimation anim)
    {
        if (_sprites == null || anim.frames.Count == 0) return;

        Sprite[] effectiveSprites = anim.mirrorH ? GetOrCreateMirroredSprites() : _sprites;
        if (effectiveSprites == null) return;

        int nullCount = anim.frames.Count(i => effectiveSprites[i] == null);
        if (nullCount > 0)
        {
            Debug.LogError($"[SpritesheetAnimator] {nullCount} sprite(s) are null — make sure the texture is imported as Sprite Mode: Multiple and has been sliced.");
            return;
        }

        var clip = new AnimationClip { frameRate = anim.fps };

        if (anim.loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        float dt = 1f / anim.fps;
        var   kf = new ObjectReferenceKeyframe[anim.frames.Count];
        for (int i = 0; i < anim.frames.Count; i++)
            kf[i] = new ObjectReferenceKeyframe { time = i * dt, value = effectiveSprites[anim.frames[i]] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, kf);

        const string folder = "Assets/Art/Characters/Animations";
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        string dest = $"{folder}/{_sheet.name}_{SafeName(anim.name)}.anim";

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(dest) != null)
            AssetDatabase.DeleteAsset(dest);

        AssetDatabase.CreateAsset(clip, dest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(saved);
        int keyCount = bindings.Length > 0 ? AnimationUtility.GetObjectReferenceCurve(saved, bindings[0]).Length : 0;
        Debug.Log($"[SpritesheetAnimator] Saved: {dest} — {keyCount} sprite keyframes @ {anim.fps} fps, duration {(float)anim.frames.Count / anim.fps:F2}s{(anim.mirrorH ? " [mirrored]" : "")}");

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
    }

    Sprite[] GetOrCreateMirroredSprites()
    {
        if (_mirroredSpritesCache == null)
            _mirroredSpritesCache = CreateMirroredSprites();
        return _mirroredSpritesCache;
    }

    Sprite[] CreateMirroredSprites()
    {
        int W = _sheet.width, H = _sheet.height;

        // Read pixels via RenderTexture — works even if the source texture is not Read/Write enabled
        var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(_sheet, rt);
        RenderTexture.active = rt;
        var readable = new Texture2D(W, H, TextureFormat.ARGB32, false);
        readable.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        readable.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // Flip horizontally
        Color[] pixels = readable.GetPixels();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W / 2; x++)
            {
                int l = y * W + x, r = y * W + (W - 1 - x);
                (pixels[l], pixels[r]) = (pixels[r], pixels[l]);
            }
        readable.SetPixels(pixels);
        readable.Apply();

        // Save the mirrored sheet next to the source spritesheet
        string sheetDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_sheet)).Replace('\\', '/');
        string mirrorPath = $"{sheetDir}/{_sheet.name}_mirrored.png";
        File.WriteAllBytes(mirrorPath, readable.EncodeToPNG());
        Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(mirrorPath, ImportAssetOptions.ForceSynchronousImport);

        // Slice the mirrored texture to match the original sprite layout (rects flipped on X)
        var importer = (TextureImporter)AssetImporter.GetAtPath(mirrorPath);
        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode       = _sheet.filterMode;
        importer.maxTextureSize   = Mathf.Max(W, H);

        var metas = new SpriteMetaData[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++)
        {
            var s = _sprites[i];
            var r = s.textureRect;
            Vector2 pivotNorm = s.pivot / new Vector2(r.width, r.height);
            metas[i] = new SpriteMetaData
            {
                name      = s.name + "_mir",
                rect      = new Rect(W - r.x - r.width, r.y, r.width, r.height),
                pivot     = new Vector2(1f - pivotNorm.x, pivotNorm.y),
                alignment = (int)SpriteAlignment.Custom,
                border    = new Vector4(s.border.z, s.border.y, s.border.x, s.border.w),
            };
        }
        ApplySpriteMetaData(importer, metas);
        importer.SaveAndReimport();

        // Build parallel array indexed the same way as _sprites
        var nameToSprite = AssetDatabase.LoadAllAssetsAtPath(mirrorPath)
            .OfType<Sprite>()
            .ToDictionary(s => s.name);

        var result = new Sprite[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++)
            nameToSprite.TryGetValue(_sprites[i].name + "_mir", out result[i]);

        return result;
    }
}
