using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SpritesheetAnimatorWindow : EditorWindow
{
    // ── Spritesheet ──────────────────────────────────────────────────
    private Texture2D _sheet;
    private Sprite[]  _sprites;          // all sprites in atlas order (top-left → bottom-right)

    // ── Frame selection ───────────────────────────────────────────────
    private List<int> _seq = new();      // selected frame indices in animation order

    // ── Settings ──────────────────────────────────────────────────────
    private int  _fps     = 12;
    private bool _loop    = true;
    private bool _mirrorH = false;

    // ── Preview ───────────────────────────────────────────────────────
    private bool   _playing;
    private double _nextFrameTime;
    private int    _previewPos;          // index into _seq

    // ── UI ────────────────────────────────────────────────────────────
    private Vector2 _gridScroll;
    private const int THUMB = 76;
    private const int PAD   = 4;
    private static readonly Color ColSelected   = new(0.25f, 0.55f, 1f,  0.55f);
    private static readonly Color ColUnselected = new(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color ColPreview    = new(0.1f,  0.1f,  0.1f, 1f);

    // ── Save ──────────────────────────────────────────────────────────
    private string _outFolder = "Assets/Animations";
    private string _clipName  = "NewAnimation";

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Spritesheet Animator")]
    public static void Open()
    {
        var w = GetWindow<SpritesheetAnimatorWindow>("Spritesheet Animator");
        w.minSize = new Vector2(520, 680);
    }

    void OnEnable()  => EditorApplication.update += Tick;
    void OnDisable() { EditorApplication.update -= Tick; _playing = false; }

    // ── Playback tick ─────────────────────────────────────────────────
    void Tick()
    {
        if (!_playing || _seq.Count == 0) return;
        if (EditorApplication.timeSinceStartup < _nextFrameTime) return;

        _previewPos++;
        if (_previewPos >= _seq.Count)
        {
            if (_loop) _previewPos = 0;
            else       { _previewPos = _seq.Count - 1; _playing = false; }
        }
        _nextFrameTime = EditorApplication.timeSinceStartup + 1.0 / _fps;
        Repaint();
    }

    // ── Main GUI ──────────────────────────────────────────────────────
    void OnGUI()
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
        DrawFrameGrid();
        GUILayout.Space(6);
        DrawPreviewRow();
        GUILayout.Space(6);
        DrawSaveRow();
    }

    // ── Top bar ───────────────────────────────────────────────────────
    void DrawTopBar()
    {
        GUILayout.Space(6);
        EditorGUI.BeginChangeCheck();
        _sheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", _sheet, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck()) LoadSprites();

        using (new EditorGUILayout.HorizontalScope())
        {
            _fps  = EditorGUILayout.IntSlider("FPS", _fps, 1, 60);
            _loop = EditorGUILayout.Toggle("Loop", _loop, GUILayout.Width(60));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select All"))
            {
                _seq = Enumerable.Range(0, _sprites.Length).ToList();
                OnSequenceChanged();
            }
            if (GUILayout.Button("Clear All"))
            {
                _seq.Clear();
                StopPreview();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_seq.Count} frame(s) selected", EditorStyles.miniLabel);
        }

        _mirrorH = EditorGUILayout.Toggle("Mirror (swap left/right)", _mirrorH);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    // ── Frame grid ────────────────────────────────────────────────────
    void DrawFrameGrid()
    {
        EditorGUILayout.LabelField("Frames  (click to add/remove)", EditorStyles.boldLabel);

        float w    = position.width - 20;
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

            bool sel = _seq.Contains(i);

            // click
            if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
            {
                if (sel) _seq.Remove(i);
                else     _seq.Add(i);
                OnSequenceChanged();
                Event.current.Use();
                Repaint();
            }

            // background
            EditorGUI.DrawRect(cell, sel ? ColSelected : ColUnselected);

            // sprite thumbnail
            DrawSprite(_sprites[i], Shrink(cell, 3), _mirrorH);

            // frame index
            DrawLabel(new Rect(cell.x, cell.yMax - 16, THUMB, 16), i.ToString(), TextAnchor.MiddleCenter, Color.white);

            // selection order badge
            if (sel)
            {
                int order = _seq.IndexOf(i);
                Rect badge = new(cell.xMax - 20, cell.y + 2, 18, 14);
                EditorGUI.DrawRect(badge, new Color(0.2f, 0.5f, 1f));
                DrawLabel(badge, (order + 1).ToString(), TextAnchor.MiddleCenter, Color.white);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Preview row ───────────────────────────────────────────────────
    void DrawPreviewRow()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            // Large preview box
            const float PS = 180;
            Rect previewRect = GUILayoutUtility.GetRect(PS, PS, GUILayout.Width(PS), GUILayout.Height(PS));
            EditorGUI.DrawRect(previewRect, ColPreview);

            if (_seq.Count > 0)
            {
                int pi = Mathf.Clamp(_previewPos, 0, _seq.Count - 1);
                DrawSprite(_sprites[_seq[pi]], Shrink(previewRect, 6), _mirrorH);
            }

            // Controls column
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(12);
                int frameCount = _seq.Count;
                int display    = frameCount == 0 ? 0 : _previewPos + 1;
                GUILayout.Label($"Frame {display} / {frameCount}", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|◀", GUILayout.Width(30))) { StopPreview(); _previewPos = 0; Repaint(); }
                    if (GUILayout.Button("◀",  GUILayout.Width(30))) { StopPreview(); Step(-1); }

                    if (_playing)
                    {
                        if (GUILayout.Button("■ Stop")) StopPreview();
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

                    if (GUILayout.Button("▶",  GUILayout.Width(30))) { StopPreview(); Step(1); }
                    if (GUILayout.Button("▶|", GUILayout.Width(30))) { StopPreview(); _previewPos = Mathf.Max(0, frameCount - 1); Repaint(); }
                }

                GUILayout.Space(8);
                float duration = frameCount > 0 ? (float)frameCount / _fps : 0f;
                GUILayout.Label($"{frameCount} frames  ·  {duration:F2}s  ·  {_fps} fps", EditorStyles.miniLabel);
            }
        }
    }

    // ── Save row ──────────────────────────────────────────────────────
    void DrawSaveRow()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _outFolder = EditorGUILayout.TextField("Folder", _outFolder);
            if (GUILayout.Button("…", GUILayout.Width(26)))
            {
                string picked = EditorUtility.OpenFolderPanel("Output folder", _outFolder, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    string full = Path.GetFullPath(picked).Replace('\\', '/');
                    string proj = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/') + "/";
                    if (full.StartsWith(proj)) _outFolder = full.Substring(proj.Length).TrimEnd('/');
                }
            }
        }

        _clipName = EditorGUILayout.TextField("Clip Name", _clipName);

        GUILayout.Space(4);
        GUI.enabled = _seq.Count > 0;
        if (GUILayout.Button("Save Animation Clip  (.anim)", GUILayout.Height(32)))
            SaveClip();
        GUI.enabled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────
    void LoadSprites()
    {
        _seq.Clear();
        _sprites = null;
        StopPreview();

        if (_sheet == null) return;

        string path = AssetDatabase.GetAssetPath(_sheet);
        _sprites = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderByDescending(s => s.textureRect.y)   // top row first
            .ThenBy(s => s.textureRect.x)              // left to right
            .ToArray();

        if (_sprites.Length == 0)
            Debug.LogWarning($"[SpritesheetAnimator] No sprites found in {path}. Make sure Sprite Mode is set to Multiple.");

        _clipName = _sheet.name;
    }

    void StopPreview() { _playing = false; _previewPos = 0; }

    void OnSequenceChanged()
    {
        if (_seq.Count == 0) { StopPreview(); return; }
        if (_playing) _previewPos = _previewPos % _seq.Count;
        else          _previewPos = 0;
    }

    void Step(int dir)
    {
        if (_seq.Count == 0) return;
        _previewPos = (_previewPos + dir + _seq.Count) % _seq.Count;
        Repaint();
    }

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

    void SaveClip()
    {
        if (_sprites == null || _seq.Count == 0) return;

        Sprite[] effectiveSprites = _mirrorH ? CreateMirroredSprites() : _sprites;
        if (effectiveSprites == null) return;

        int nullCount = _seq.Count(i => effectiveSprites[i] == null);
        if (nullCount > 0)
        {
            Debug.LogError($"[SpritesheetAnimator] {nullCount} sprite(s) are null — make sure the texture is imported as Sprite Mode: Multiple and has been sliced.");
            return;
        }

        var clip = new AnimationClip { frameRate = _fps };

        if (_loop)
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

        float dt = 1f / _fps;
        var   kf = new ObjectReferenceKeyframe[_seq.Count];
        for (int i = 0; i < _seq.Count; i++)
            kf[i] = new ObjectReferenceKeyframe { time = i * dt, value = effectiveSprites[_seq[i]] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, kf);

        if (!Directory.Exists(_outFolder))
            Directory.CreateDirectory(_outFolder);

        string safe = string.Concat(_clipName.Split(Path.GetInvalidFileNameChars()));
        string dest = $"{_outFolder}/{safe}.anim";
        AssetDatabase.CreateAsset(clip, dest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(saved);
        int keyCount = bindings.Length > 0 ? AnimationUtility.GetObjectReferenceCurve(saved, bindings[0]).Length : 0;
        Debug.Log($"[SpritesheetAnimator] Saved: {dest} — {keyCount} sprite keyframes @ {_fps} fps, duration {(float)_seq.Count / _fps:F2}s{(_mirrorH ? " [mirrored]" : "")}");

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
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

        // Save PNG next to the clip output
        if (!Directory.Exists(_outFolder)) Directory.CreateDirectory(_outFolder);
        string safe       = string.Concat(_clipName.Split(Path.GetInvalidFileNameChars()));
        string mirrorPath = $"{_outFolder}/{safe}_mirrored.png";
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
        importer.spritesheet = metas;
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
