using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Preview sprite animation clips from an Animator Controller directly in the editor.
/// No scene object or hierarchy needed — drag the controller from the Project window.
/// Open via Tools → Animator Tester.
/// </summary>
public class AnimatorTesterWindow : EditorWindow
{
    // ── Inputs ────────────────────────────────────────────────────────
    private AnimatorController _ctrl;

    // ── Parameters ────────────────────────────────────────────────────
    private Dictionary<string, float> _floats   = new();
    private Dictionary<string, bool>  _bools    = new();
    private Dictionary<string, int>   _ints     = new();

    // ── Preview state ─────────────────────────────────────────────────
    private AnimatorState        _previewState;
    private Sprite[]             _previewSprites;  // sprite keyframes extracted from the clip
    private float                _previewTime;
    private int                  _previewFrame;
    private bool                 _playing;
    private bool                 _loop = true;
    private double               _lastTick;

    // ── UI ────────────────────────────────────────────────────────────
    private Vector2 _stateScroll;
    private const float PREVIEW_SIZE = 220f;
    private static readonly Color ColBg       = new(0.12f, 0.12f, 0.12f);
    private static readonly Color ColActive   = new(0.25f, 0.85f, 0.35f);
    private static readonly Color ColPlaying  = new(1f,    0.75f, 0.1f);

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Animator Tester")]
    public static void Open()
    {
        var w = GetWindow<AnimatorTesterWindow>("Animator Tester");
        w.minSize = new Vector2(420, 600);
    }

    void OnEnable()  => EditorApplication.update += Tick;
    void OnDisable() { EditorApplication.update -= Tick; _playing = false; }

    // ── Playback tick ─────────────────────────────────────────────────
    void Tick()
    {
        if (!_playing || _previewSprites == null || _previewSprites.Length == 0) return;

        double now = EditorApplication.timeSinceStartup;
        float  dt  = (float)(now - _lastTick);
        _lastTick  = now;

        var clip = _previewState?.motion as AnimationClip;
        if (clip == null) return;

        _previewTime += dt;
        float duration = clip.length;

        if (_previewTime >= duration)
        {
            if (_loop) _previewTime %= duration;
            else       { _previewTime = duration; _playing = false; }
        }

        // Map time → frame index
        _previewFrame = TimeToFrame(_previewTime, clip);
        Repaint();
    }

    // ── Main GUI ──────────────────────────────────────────────────────
    void OnGUI()
    {
        GUILayout.Space(6);
        DrawControllerField();

        if (_ctrl == null)
        {
            EditorGUILayout.HelpBox("Drag an Animator Controller from the Project window.", MessageType.Info);
            return;
        }

        GUILayout.Space(4);
        DrawParameters();
        GUILayout.Space(6);
        DrawStates();

        if (_previewState != null)
        {
            GUILayout.Space(6);
            DrawPreview();
        }
    }

    // ── Controller field ──────────────────────────────────────────────
    void DrawControllerField()
    {
        EditorGUI.BeginChangeCheck();
        _ctrl = (AnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", _ctrl, typeof(AnimatorController), false);
        if (EditorGUI.EndChangeCheck()) OnControllerChanged();
    }

    // ── Parameters ────────────────────────────────────────────────────
    void DrawParameters()
    {
        if (_ctrl.parameters.Length == 0) return;

        EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        foreach (var p in _ctrl.parameters)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string badge = p.type switch
                {
                    AnimatorControllerParameterType.Float   => "F",
                    AnimatorControllerParameterType.Bool    => "B",
                    AnimatorControllerParameterType.Int     => "I",
                    AnimatorControllerParameterType.Trigger => "T",
                    _                                       => "?"
                };
                GUILayout.Label(badge, EditorStyles.miniLabel, GUILayout.Width(14));
                GUILayout.Label(p.name, GUILayout.Width(140));

                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _floats[p.name] = EditorGUILayout.FloatField(_floats.GetValueOrDefault(p.name, p.defaultFloat));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        _bools[p.name]  = EditorGUILayout.Toggle(_bools.GetValueOrDefault(p.name, p.defaultBool));
                        break;
                    case AnimatorControllerParameterType.Int:
                        _ints[p.name]   = EditorGUILayout.IntField(_ints.GetValueOrDefault(p.name, p.defaultInt));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        GUILayout.Label("(trigger)", EditorStyles.miniLabel);
                        break;
                }
            }
        }

        bool paramsChanged = EditorGUI.EndChangeCheck();

        // Auto-switch to the state the new params would activate
        var suggested = EvaluateActiveState();
        if (suggested != null && suggested != _previewState)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("  → activates:", EditorStyles.miniLabel);
                var s = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColActive } };
                GUILayout.Label(suggested.name, s);
            }

            if (paramsChanged)
                StartPreview(suggested);  // auto-switch, keeps playing
        }
    }

    // ── States list ───────────────────────────────────────────────────
    void DrawStates()
    {
        EditorGUILayout.LabelField("States", EditorStyles.boldLabel);

        var states    = _ctrl.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var suggested = EvaluateActiveState();
        float listH   = Mathf.Min(states.Length * 22f + 8f, 180f);

        _stateScroll = EditorGUILayout.BeginScrollView(_stateScroll, GUILayout.Height(listH));

        foreach (var state in states)
        {
            bool isPreviewing = state == _previewState;
            bool isSuggested  = state == suggested;
            var  clip         = state.motion as AnimationClip;

            Rect row = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(row, isPreviewing ? new Color(0.2f, 0.15f, 0.0f)
                                  : isSuggested  ? new Color(0.05f, 0.18f, 0.05f)
                                  :                new Color(0.17f, 0.17f, 0.17f));

            Color dotCol = isPreviewing ? ColPlaying : isSuggested ? ColActive : Color.gray;
            string dot   = isPreviewing ? "▶" : isSuggested ? "●" : "○";
            GUI.Label(new Rect(row.x + 4, row.y + 2, 16, 18), dot,
                new GUIStyle(EditorStyles.label) { normal = { textColor = dotCol } });

            GUI.Label(new Rect(row.x + 22, row.y + 2, 180, 18), state.name,
                new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isPreviewing ? FontStyle.Bold : FontStyle.Normal,
                    normal    = { textColor = isPreviewing ? ColPlaying : Color.white }
                });

            if (clip != null)
            {
                int frames = ExtractSprites(clip).Length;
                GUI.Label(new Rect(row.x + 205, row.y + 2, 120, 18),
                    $"{frames} frames  {clip.length:F1}s", EditorStyles.miniLabel);
            }
            else
            {
                GUI.Label(new Rect(row.x + 205, row.y + 2, 120, 18), "no clip", EditorStyles.miniLabel);
            }

            if (GUI.Button(new Rect(row.xMax - 62, row.y + 1, 58, 18), "▶ Play", EditorStyles.miniButton))
                StartPreview(state);
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Preview box ───────────────────────────────────────────────────
    void DrawPreview()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        var clip = _previewState.motion as AnimationClip;

        using (new EditorGUILayout.HorizontalScope())
        {
            // Sprite preview box
            Rect box = GUILayoutUtility.GetRect(PREVIEW_SIZE, PREVIEW_SIZE,
                GUILayout.Width(PREVIEW_SIZE), GUILayout.Height(PREVIEW_SIZE));
            EditorGUI.DrawRect(box, ColBg);

            if (_previewSprites != null && _previewSprites.Length > 0)
            {
                int fi = Mathf.Clamp(_previewFrame, 0, _previewSprites.Length - 1);
                DrawSprite(_previewSprites[fi], Shrink(box, 6));
            }

            // Right column
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(8);

                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = ColPlaying } };
                GUILayout.Label(_previewState.name, titleStyle);

                if (clip != null)
                    GUILayout.Label($"{clip.length:F2}s  ·  {_previewSprites?.Length ?? 0} frames", EditorStyles.miniLabel);

                GUILayout.Space(6);

                int total = _previewSprites?.Length ?? 0;
                GUILayout.Label($"Frame  {_previewFrame + 1} / {total}", EditorStyles.centeredGreyMiniLabel);

                GUILayout.Space(4);

                // Time scrub
                if (clip != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float t = EditorGUILayout.Slider(_previewTime, 0f, clip.length);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _previewTime  = t;
                        _previewFrame = TimeToFrame(t, clip);
                        _playing      = false;
                    }
                }

                GUILayout.Space(6);

                // Transport
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|◀", GUILayout.Width(28))) Seek(0f, clip);
                    if (GUILayout.Button("◀",  GUILayout.Width(28))) SeekFrame(_previewFrame - 1, clip);

                    if (_playing)
                    {
                        if (GUILayout.Button("⏸ Pause")) _playing = false;
                    }
                    else
                    {
                        if (GUILayout.Button("▶ Resume"))
                        {
                            _playing  = true;
                            _lastTick = EditorApplication.timeSinceStartup;
                        }
                    }

                    if (GUILayout.Button("▶",  GUILayout.Width(28))) SeekFrame(_previewFrame + 1, clip);
                    if (GUILayout.Button("▶|", GUILayout.Width(28))) Seek(clip?.length ?? 0f, clip);
                }

                GUILayout.Space(4);
                _loop = EditorGUILayout.Toggle("Loop", _loop);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    void OnControllerChanged()
    {
        _previewState   = null;
        _previewSprites = null;
        _playing        = false;
        _floats.Clear(); _bools.Clear(); _ints.Clear();

        if (_ctrl == null) return;
        foreach (var p in _ctrl.parameters)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Float: _floats[p.name] = p.defaultFloat; break;
                case AnimatorControllerParameterType.Bool:  _bools[p.name]  = p.defaultBool;  break;
                case AnimatorControllerParameterType.Int:   _ints[p.name]   = p.defaultInt;   break;
            }
        }
    }

    void StartPreview(AnimatorState state, bool keepTime = false)
    {
        _previewState = state;

        var clip = state.motion as AnimationClip;
        _previewSprites = clip != null ? ExtractSprites(clip) : null;

        if (!keepTime) { _previewTime = 0f; _previewFrame = 0; }

        // Always auto-play
        _playing  = true;
        _lastTick = EditorApplication.timeSinceStartup;

        if (_previewSprites == null || _previewSprites.Length == 0)
            Debug.LogWarning($"[AnimatorTester] No sprite keyframes found in clip '{clip?.name}'. Make sure the .anim was saved by the Spritesheet Animator tool.");
    }

    void Seek(float t, AnimationClip clip)
    {
        if (clip == null) return;
        _playing      = false;
        _previewTime  = Mathf.Clamp(t, 0f, clip.length);
        _previewFrame = TimeToFrame(_previewTime, clip);
        Repaint();
    }

    void SeekFrame(int frame, AnimationClip clip)
    {
        if (clip == null || _previewSprites == null) return;
        int clamped = Mathf.Clamp(frame, 0, _previewSprites.Length - 1);
        _previewFrame = clamped;
        _previewTime  = clamped / clip.frameRate;
        _playing      = false;
        Repaint();
    }

    int TimeToFrame(float t, AnimationClip clip)
    {
        if (_previewSprites == null || _previewSprites.Length == 0) return 0;
        if (clip.length <= 0f) return 0;
        float norm = Mathf.Clamp01(t / clip.length);
        return Mathf.Clamp(Mathf.FloorToInt(norm * _previewSprites.Length), 0, _previewSprites.Length - 1);
    }

    /// <summary>Extract ordered sprites from the m_Sprite ObjectReferenceCurve of a clip.</summary>
    static Sprite[] ExtractSprites(AnimationClip clip)
    {
        if (clip == null) return System.Array.Empty<Sprite>();

        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;
            return AnimationUtility.GetObjectReferenceCurve(clip, binding)
                .Select(k => k.value as Sprite)
                .Where(s => s != null)
                .ToArray();
        }
        return System.Array.Empty<Sprite>();
    }

    AnimatorState EvaluateActiveState()
    {
        if (_ctrl == null) return null;
        var sm = _ctrl.layers[0].stateMachine;

        foreach (var t in sm.anyStateTransitions)
            if (t.destinationState != null && CheckConditions(t.conditions))
                return t.destinationState;

        if (sm.defaultState != null)
            foreach (var t in sm.defaultState.transitions)
                if (t.destinationState != null && CheckConditions(t.conditions))
                    return t.destinationState;

        return sm.defaultState;
    }

    bool CheckConditions(AnimatorCondition[] conds)
    {
        foreach (var c in conds)
        {
            bool ok = c.mode switch
            {
                AnimatorConditionMode.If       =>  _bools.GetValueOrDefault(c.parameter),
                AnimatorConditionMode.IfNot    => !_bools.GetValueOrDefault(c.parameter, true),
                AnimatorConditionMode.Greater  =>  _floats.GetValueOrDefault(c.parameter) > c.threshold,
                AnimatorConditionMode.Less     =>  _floats.GetValueOrDefault(c.parameter) < c.threshold,
                AnimatorConditionMode.Equals   =>  _ints.GetValueOrDefault(c.parameter) == (int)c.threshold,
                AnimatorConditionMode.NotEqual =>  _ints.GetValueOrDefault(c.parameter) != (int)c.threshold,
                _                             =>  true
            };
            if (!ok) return false;
        }
        return true;
    }

    static void DrawSprite(Sprite sprite, Rect rect)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect  tr  = sprite.textureRect;
        float tw  = sprite.texture.width;
        float th  = sprite.texture.height;
        Rect  uv  = new(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
        float sa  = tr.width / tr.height;
        float ba  = rect.width / rect.height;
        Rect  draw = sa > ba
            ? new Rect(rect.x, rect.y + (rect.height - rect.width / sa) * .5f, rect.width, rect.width / sa)
            : new Rect(rect.x + (rect.width - rect.height * sa) * .5f, rect.y, rect.height * sa, rect.height);
        GUI.DrawTextureWithTexCoords(draw, sprite.texture, uv);
    }

    static Rect Shrink(Rect r, float px) =>
        new(r.x + px, r.y + px, r.width - px * 2, r.height - px * 2);
}
