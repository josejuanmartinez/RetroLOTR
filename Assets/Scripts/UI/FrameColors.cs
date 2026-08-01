using UnityEngine;

// Drives the pc/terrain frame sprite tint for a Hex based on its current
// situation, instead of toggling separate frame GameObjects on/off.
// The state booleans below are serialized so they can be toggled directly
// in the Inspector (edit mode, no Play needed) to preview each tint.
[ExecuteAlways]
public class FrameColors : MonoBehaviour
{
    [SerializeField] Color scoutedColor;
    [SerializeField] Color darknessColor;
    [SerializeField] Color unhoveredColor;
    [SerializeField] SpriteRenderer pcSpriteRenderer;
    [SerializeField] SpriteRenderer terrainSpriteRenderer;

    [Header("Situation (toggle here to preview in edit mode)")]
    [SerializeField] private bool scouted;
    [SerializeField] private bool darkness;
    [SerializeField] private bool hovered;

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

    // Driven by Hex.Hover()/Unhover() (see OnHoverTile) — mouse over the hex tints it with
    // scoutedColor as a highlight; moving off falls back to unhoveredColor instead of the
    // plain idle white, so an un-hovered hex reads as dimmer than one you're pointing at.
    public void SetHovered(bool active)
    {
        if (hovered == active) return;
        hovered = active;
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

    // pcSpriteRenderer/terrainSpriteRenderer are the hex's actual PC/terrain art
    // renderers (same components as Hex.pcTexture/terrainTexture), not blank
    // overlay sprites - their color is a multiplicative tint on top of the
    // artwork. So the idle/no-situation state must be a real (non-transparent)
    // color, never Color.clear, which would zero the art out.
    private void Refresh()
    {
        Color color =
            darkness ? darknessColor :
            scouted ? scoutedColor :
            hovered ? scoutedColor :
            unhoveredColor;

        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        if (pcSpriteRenderer != null) pcSpriteRenderer.color = color;
        if (terrainSpriteRenderer != null) terrainSpriteRenderer.color = color;
    }
}
