using System.Collections;
using UnityEngine;

// Plays a character's baked per-facing spritesheet animation (see AnimationSpritesheetBaker +
// CharacterSpritesheets) directly on a SpriteRenderer, swapping frames by hand using each
// state's fps/loop/frameCount from the atlas manifest — no Animator/AnimatorController
// involved. The SpriteRenderer must live on the same GameObject as this component.
public class CharacterAnimationController : MonoBehaviour
{
    public enum AnimationKind
    {
        CrouchIdle, Action, Magic, StandingIdle, IdleLook, Death, Hit,
        StandingWalkForward, TurnRight, TurnLeft, Block, Attack
    }

    public enum Orientation { Forward, Back, Left, Right }

    private const float VerticalMovementEpsilon = 0.01f;
    private const string StandingIdleStateName = "standing idle";

    // Last resort when neither the character's own name nor its race has a baked spritesheet —
    // set per-instance (e.g. a generic "Human"/"Common" folder), since there's no single global
    // default that makes sense for every kind of character this component ends up attached to.
    public string fallback;

    // Steady tint applied to this character's sprite whenever it is neither hovered nor the
    // currently selected character (a selected character counts as hovered) — read by
    // Hex.UpdateCharacterSpriteAlpha, not applied by this component itself.
    public Color unhoveredColor = Color.white;
    // Cycles per second of the outline-size pulse Hex.UpdateCharacterSpriteAlpha plays on the
    // currently selected character only. Read from there, not applied by this component itself.
    public float outlinePulseSpeed = 1f;

    // ── Animation bools — one checkbox per baked state. Names/casing match the atlas manifest's
    // "name" field exactly (see AnimationBindings below); default is Standing Idle. ──────────
    public bool crouchIdle;
    public bool action;
    public bool magic;
    public bool standingIdle = true;
    public bool idleLook;
    public bool death;
    public bool hit;
    public bool standingWalkForward;
    public bool turnRight;
    public bool turnLeft;
    public bool block;
    public bool attack;

    // ── Orientation bools — default is Forward ──────────────────────
    public bool forward = true;
    public bool back;
    public bool left;
    public bool right;

    // Whether a non-looping animation (Action/Magic/Death/Hit/Turn*/Block/Attack) repeats from
    // frame 0 once it reaches its last frame, or returns to Standing Idle. Has no effect on
    // animations the atlas itself marks loop:true (Standing Idle, Standing Walk Forward, Crouch
    // Idle, Idle Look) — those just keep cycling regardless of this flag.
    public bool loop;

    // Shared across every character using this component — plain data + delegate pairs, not a
    // dictionary, so lookups stay allocation-free in Update().
    private static readonly (AnimationKind Kind, string StateName, System.Func<CharacterAnimationController, bool> Get, System.Action<CharacterAnimationController, bool> Set)[] AnimationBindings =
    {
        (AnimationKind.CrouchIdle,          "Crouch Idle",           c => c.crouchIdle,          (c, v) => c.crouchIdle = v),
        (AnimationKind.Action,              "Action",                c => c.action,              (c, v) => c.action = v),
        (AnimationKind.Magic,               "Magic",                 c => c.magic,               (c, v) => c.magic = v),
        (AnimationKind.StandingIdle,        StandingIdleStateName,   c => c.standingIdle,        (c, v) => c.standingIdle = v),
        (AnimationKind.IdleLook,            "idle look",             c => c.idleLook,             (c, v) => c.idleLook = v),
        (AnimationKind.Death,               "Death",                 c => c.death,               (c, v) => c.death = v),
        (AnimationKind.Hit,                 "Hit",                   c => c.hit,                 (c, v) => c.hit = v),
        (AnimationKind.StandingWalkForward, "Standing Walk Forward", c => c.standingWalkForward, (c, v) => c.standingWalkForward = v),
        // Atlas state names swapped relative to the kind: the baked "Turn Left"/"Turn Right"
        // clips show the opposite turn from what their names say, so TurnRight pulls the "Turn
        // Left" state (and vice versa) to compensate — turnRight/turnLeft still mean what their
        // names say from the caller's side.
        (AnimationKind.TurnRight,           "Turn Left",             c => c.turnRight,           (c, v) => c.turnRight = v),
        (AnimationKind.TurnLeft,            "Turn Right",            c => c.turnLeft,            (c, v) => c.turnLeft = v),
        (AnimationKind.Block,               "Block",                 c => c.block,               (c, v) => c.block = v),
        (AnimationKind.Attack,              "Attack",                c => c.attack,              (c, v) => c.attack = v),
    };

    private static readonly (Orientation Value, string FacingName, System.Func<CharacterAnimationController, bool> Get, System.Action<CharacterAnimationController, bool> Set)[] OrientationBindings =
    {
        (Orientation.Forward, "Forward", c => c.forward, (c, v) => c.forward = v),
        (Orientation.Back,    "Back",    c => c.back,    (c, v) => c.back = v),
        (Orientation.Left,    "Left",    c => c.left,    (c, v) => c.left = v),
        (Orientation.Right,   "Right",   c => c.right,   (c, v) => c.right = v),
    };

    // The order a physical turn actually visits: Turn Left steps backward through this cycle,
    // Turn Right steps forward. Two Turn Left calls (or two Turn Right calls) land on Back;
    // a Turn Left followed by a Turn Right steps +1 then -1 and cancels back to Forward.
    private static readonly Orientation[] OrientationCycle =
    {
        Orientation.Forward, Orientation.Right, Orientation.Back, Orientation.Left
    };

    private SpriteRenderer spriteRenderer;
    private Character resolvedForCharacter;
    private string resolvedRaceOrName;
    private bool isShowing;
    private string currentStateName;
    private int frameIndex;
    private float frameTimer;
    private bool cursorHovering;

    // Set only when a Show() call fails because CharacterSpritesheets' Addressables load
    // hadn't finished yet (as opposed to a genuine no-match) — callers like Hex.RedrawCharacters
    // only run on specific game events, not every frame, so without this a character whose
    // first Show() lands mid-load would stay stuck on the old Illustrations fallback sprite
    // forever, even once loading finishes moments later.
    private Character pendingCharacter;

    private void Awake()
    {
        EnsureSpriteRenderer();
    }

    private void Update()
    {
        // Retried every frame rather than gated on CharacterSpritesheets.IsLoaded (whole-game
        // loaded flag): ResolveCharacter/TryResolveRaceOrName already resolves as soon as THIS
        // character's own manifest is registered, independent of whether other characters' races'
        // spritesheets are still loading. Gating on the global flag here used to force every
        // character to keep showing its Hex-drawn card fallback until literally everything in the
        // game finished loading, even once its own spritesheet was ready.
        if (!isShowing && pendingCharacter != null)
        {
            Character retry = pendingCharacter;
            pendingCharacter = null;
            Show(retry);
        }

        if (!isShowing || resolvedRaceOrName == null) return;

        string facing = GetActiveOrientationName();
        string desiredStateName = GetActiveAnimationStateName();

        CharacterSpritesheets.AtlasManifest manifest = CharacterSpritesheets.GetManifest(resolvedRaceOrName, facing);
        CharacterSpritesheets.AtlasState state = manifest != null ? FindState(manifest, desiredStateName) : null;

        if (state == null)
        {
            // Requested animation isn't baked for this character/facing — fall back to idle
            // rather than freeze on whatever frame was last drawn.
            if (desiredStateName != StandingIdleStateName) SetAnimation(AnimationKind.StandingIdle);
            return;
        }

        if (currentStateName != desiredStateName)
        {
            currentStateName = desiredStateName;
            frameIndex = 0;
            frameTimer = 0f;
        }

        float frameDuration = 1f / Mathf.Max(state.fps, 0.01f);
        frameTimer += Time.deltaTime;
        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;
            if (frameIndex >= state.frameCount)
            {
                // A completed Turn Left/Right cycle always rotates the facing, whether or not
                // it then loops — even under loop:true this resolves correctly, since looping
                // just replays the same state, which Update() will now source from the NEW
                // facing's atlas (GetActiveOrientationName() below reads the just-updated bools),
                // continuing the spin one more quarter-turn per cycle.
                AnimationKind? finishedKind = GetKindForStateName(currentStateName);
                if (finishedKind == AnimationKind.TurnLeft) AdvanceOrientation(-1);
                else if (finishedKind == AnimationKind.TurnRight) AdvanceOrientation(1);

                if (state.loop || loop)
                {
                    frameIndex = 0;
                }
                else
                {
                    // Hold the last frame this Update() (still drawn below), switch to Standing
                    // Idle now so next Update() picks it up fresh from frame 0.
                    frameIndex = state.frameCount - 1;
                    SetAnimation(AnimationKind.StandingIdle);
                }
            }
        }

        Sprite sprite = CharacterSpritesheets.GetSprite($"{state.spriteNamePrefix}_{frameIndex:D2}");
        if (sprite != null) spriteRenderer.sprite = sprite;
    }

    private static CharacterSpritesheets.AtlasState FindState(CharacterSpritesheets.AtlasManifest manifest, string stateName)
    {
        foreach (CharacterSpritesheets.AtlasState state in manifest.states)
            if (state.name == stateName) return state;
        return null;
    }

    private string GetActiveAnimationStateName()
    {
        foreach (var binding in AnimationBindings)
            if (binding.Get(this)) return binding.StateName;
        return StandingIdleStateName;
    }

    private string GetActiveOrientationName()
    {
        foreach (var binding in OrientationBindings)
            if (binding.Get(this)) return binding.FacingName;
        return "Forward";
    }

    private Orientation GetActiveOrientation()
    {
        foreach (var binding in OrientationBindings)
            if (binding.Get(this)) return binding.Value;
        return Orientation.Forward;
    }

    private static AnimationKind? GetKindForStateName(string stateName)
    {
        if (stateName == null) return null;
        foreach (var binding in AnimationBindings)
            if (binding.StateName == stateName) return binding.Kind;
        return null;
    }

    // step = -1 for Turn Left, +1 for Turn Right — one quarter-step around OrientationCycle.
    private void AdvanceOrientation(int step)
    {
        int index = System.Array.IndexOf(OrientationCycle, GetActiveOrientation());
        int next = ((index + step) % OrientationCycle.Length + OrientationCycle.Length) % OrientationCycle.Length;
        SetOrientation(OrientationCycle[next]);
    }

    // ── Public setters — the "public methods to change all the bools" ──────────────
    public void SetAnimation(AnimationKind kind)
    {
        foreach (var binding in AnimationBindings)
            binding.Set(this, binding.Kind == kind);
    }

    public void SetOrientation(Orientation orientation)
    {
        foreach (var binding in OrientationBindings)
            binding.Set(this, binding.Value == orientation);
    }

    public void SetLoop(bool value) => loop = value;

    private void EnsureSpriteRenderer()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    public bool Show(Character character)
    {
        if (character == null)
        {
            Clear();
            return false;
        }

        // Captured before the attempt: if CharacterSpritesheets was still loading when this
        // call started and resolution fails, it's worth an automatic retry once loading
        // finishes. If it was ALREADY loaded and still failed, that's a genuine no-match —
        // don't mark it pending, or Update() would retry it every single frame forever.
        bool wasLoadedBeforeAttempt = CharacterSpritesheets.IsLoaded;
        if (!ResolveCharacter(character))
        {
            Clear();
            pendingCharacter = wasLoadedBeforeAttempt ? null : character;
            return false;
        }

        pendingCharacter = null;
        EnsureSpriteRenderer();
        isShowing = true;
        return true;
    }

    // Resolution failure here can mean "genuinely nothing baked" OR "Addressables hasn't
    // finished its initial load yet" — only success gets cached, so a later Show() call for the
    // same character keeps retrying instead of being permanently stuck on an early failure.
    private bool ResolveCharacter(Character character)
    {
        if (resolvedForCharacter == character && resolvedRaceOrName != null) return true;

        bool resolved = CharacterSpritesheets.TryResolveRaceOrName(character.characterName, character.SpriteVariantBaseName, character.race, fallback, out resolvedRaceOrName);
        if (!resolved)
        {
            if (!CharacterSpritesheets.IsLoaded)
                Debug.LogWarning($"[CharacterAnimationController] '{character.characterName}': CharacterSpritesheets Addressables load isn't finished yet — will retry on the next Show() call.");
            else
                Debug.LogWarning($"[CharacterAnimationController] '{character.characterName}' (variantOf={character.SpriteVariantBaseName}, race={character.race}, fallback='{fallback}'): no baked spritesheet found under that name, variant base, race, or fallback.");
            resolvedForCharacter = null;
            return false;
        }

        if (resolvedForCharacter != character)
        {
            // First resolution of this specific character (or a change of character since the
            // last one shown here) — reset to the documented per-character default.
            SetOrientation(Orientation.Forward);
            SetAnimation(AnimationKind.StandingIdle);
            currentStateName = null;
            frameIndex = 0;
            frameTimer = 0f;
        }
        resolvedForCharacter = character;
        return true;
    }

    // worldDelta is the move in world space: horizontal moves play the side walk,
    // upward moves show the character's back, downward moves walk toward the viewer.
    public bool PlayMovement(Character character, Vector3 worldDelta)
    {
        if (!gameObject.activeInHierarchy) return false;
        if (!Show(character)) return false;
        SetOrientation(ResolveDirectionOrientation(worldDelta));
        SetAnimation(AnimationKind.StandingWalkForward);
        return true;
    }

    // Turns in place — no position change, purely animation — toward the facing worldDelta
    // implies, one quarter-turn (Turn Left/Right) at a time, yielding until each turn's clip
    // actually finishes and the facing updates before starting the next. Callers that want
    // "turn first, then walk" (e.g. Board's per-hex move step) should yield on this BEFORE
    // starting any position tween, then call PlayMovement/start walking once it returns.
    // No-ops immediately if already facing the right way, if inactive, or if nothing resolved.
    public IEnumerator TurnTowardMovement(Character character, Vector3 worldDelta)
    {
        if (!gameObject.activeInHierarchy) yield break;

        // This SpriteRenderer is shared/reused across whichever character is currently moving —
        // giving up after one failed Show() (as a plain "if" would) leaves the PREVIOUS
        // character's sprite on screen for this character's entire move if resolution just
        // hasn't finished loading yet. Show() already clears the sprite on failure, so at worst
        // this is a blank beat, never a stale wrong character. Only bail for real once loading
        // has fully finished and Show() still fails — that's a genuine no-match, not a timing gap.
        while (!Show(character))
        {
            if (CharacterSpritesheets.IsLoaded) yield break;
            yield return null;
        }

        Orientation target = ResolveDirectionOrientation(worldDelta);
        while (GetActiveOrientation() != target)
        {
            SetAnimation(NextTurnKindToward(target));
            Orientation before = GetActiveOrientation();
            // isShowing can flip false mid-turn (e.g. Clear() called from elsewhere) — don't spin forever.
            while (isShowing && GetActiveOrientation() == before) yield return null;
            if (!isShowing) yield break;
        }
    }

    // diff is how many +1 ("Right") steps around OrientationCycle reach target from the current
    // facing. For an exact 180 (diff == 2) either direction is equally short, so Right is picked
    // arbitrarily.
    private AnimationKind NextTurnKindToward(Orientation target)
    {
        int currentIndex = System.Array.IndexOf(OrientationCycle, GetActiveOrientation());
        int targetIndex = System.Array.IndexOf(OrientationCycle, target);
        int diff = ((targetIndex - currentIndex) % OrientationCycle.Length + OrientationCycle.Length) % OrientationCycle.Length;
        return diff <= 2 ? AnimationKind.TurnRight : AnimationKind.TurnLeft;
    }

    public bool PlayAction(Character character)
    {
        if (!gameObject.activeInHierarchy) return false;
        if (!Show(character)) return false;
        SetAnimation(AnimationKind.Action);
        return true;
    }

    private static Orientation ResolveDirectionOrientation(Vector3 worldDelta)
    {
        if (worldDelta.y > VerticalMovementEpsilon) return Orientation.Back;
        if (worldDelta.y < -VerticalMovementEpsilon) return Orientation.Forward;
        // Moving toward -X showed the "_Right" sheet and vice versa — swapped from the naive
        // mapping to match the baked facings' actual on-screen orientation.
        return worldDelta.x < 0f ? Orientation.Right : Orientation.Left;
    }

    public void Clear()
    {
        isShowing = false;
        resolvedForCharacter = null;
        resolvedRaceOrName = null;
        currentStateName = null;
        pendingCharacter = null;
        if (spriteRenderer != null) spriteRenderer.sprite = null;
    }

    // Called by CharacterSpriteHover, which owns the actual collider/mouse events (this
    // component's own GameObject has no collider) — mirrors unhoveredColor/outlinePulseSpeed
    // above in being state this component holds but doesn't drive itself.
    public void SetHoverCursor(bool hovering)
    {
        if (cursorHovering == hovering) return;
        cursorHovering = hovering;
        if (hovering) CursorManager.Instance?.SetClickableCursor();
        else CursorManager.Instance?.SetDefaultCursor();
    }

    private void OnDisable()
    {
        // Restore the default cursor if this gets disabled mid-hover (e.g. the character
        // dies or the hex redraws while the pointer is still over it).
        if (cursorHovering) SetHoverCursor(false);
    }
}
