using UnityEngine;

// Drives the pc/terrain frame sprite tint for a Hex based on its current
// situation, instead of toggling separate frame GameObjects on/off.
// The state booleans below are serialized so they can be toggled directly
// in the Inspector (edit mode, no Play needed) to preview each tint.
[ExecuteAlways]
public class FrameColors : MonoBehaviour
{
    [SerializeField] Color tipColor;
    [SerializeField] Color scoutedColor;
    [SerializeField] Color darknessColor;
    [SerializeField] SpriteRenderer pcSpriteRenderer;
    [SerializeField] SpriteRenderer terrainSpriteRenderer;

    [Header("Hint Pulse")]
    [Tooltip("Cycles per second the standing hint (SetHint) pulses between white and tipColor.")]
    [SerializeField] private float hintPulseSpeed = 2f;

    [Header("Situation (toggle here to preview in edit mode)")]
    [SerializeField] private bool tipping;
    [SerializeField] private bool scouted;
    [SerializeField] private bool darkness;
    [SerializeField] private bool hinting;

    public void SetTip(bool active)
    {
        if (tipping == active) return;
        tipping = active;
        Refresh();
    }

    // Standing hint (not a transient flash like SetTip) for hexes a selected character
    // could move to and play an opportunity card at. Cleared whenever selection changes.
    // Reuses tipColor (rather than its own color) but renders as a continuous white<->tipColor
    // pulse, driven every frame from Update, so it reads as distinct from tip's solid flash.
    public void SetHint(bool active)
    {
        if (hinting == active) return;
        hinting = active;
        Refresh();
    }

    public void SetScouted(bool active)
    {
        if (scouted == active) return;
        scouted = active;
        Refresh();
    }

    public void SetDarkness(bool active)
    {
        if (darkness == active) return;
        darkness = active;
        Refresh();
    }

    // Every Set* method above only refreshes on a state *change*, so without
    // this the initial all-false state would never actually get applied to
    // the sprites - they'd keep whatever color was last baked into the
    // prefab/scene instead of being explicitly cleared on startup.
    private void OnEnable()
    {
        Refresh();
    }

    // Called by Unity whenever a serialized field changes in the Inspector,
    // in both edit mode and play mode, so dragging the booleans above
    // previews the tint without entering Play mode.
    private void OnValidate()
    {
        Refresh();
    }

    // Drives the hint pulse animation. Tip still wins outright (solid, no pulsing) whenever
    // both happen to be active — see the priority comment on Refresh below.
    private void Update()
    {
        if (!hinting || tipping) return;

        float t = (Mathf.Sin(Time.time * hintPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        ApplyColor(Color.Lerp(Color.white, tipColor, t));
    }

    // pcSpriteRenderer/terrainSpriteRenderer are the hex's actual PC/terrain art
    // renderers (same components as Hex.pcTexture/terrainTexture), not blank
    // overlay sprites - their color is a multiplicative tint on top of the
    // artwork. So the idle/no-situation state must be Color.white (untinted,
    // art shows normally), never Color.clear, which would zero the art out.
    //
    // Tip is a transient, player-driven cue that takes priority over the
    // persistent state indicators (darkness/scouted) so it always reads.
    // Hint sits at the same priority tip used to have over darkness/scouted; while active
    // and tip is not, Update() takes over every frame to animate the pulse instead of this
    // method holding a static color.
    private void Refresh()
    {
        if (hinting && !tipping)
        {
            ApplyColor(Color.white);
            return;
        }

        Color color =
            tipping ? tipColor :
            darkness ? darknessColor :
            scouted ? scoutedColor :
            Color.white;

        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        if (pcSpriteRenderer != null) pcSpriteRenderer.color = color;
        if (terrainSpriteRenderer != null) terrainSpriteRenderer.color = color;
    }
}
