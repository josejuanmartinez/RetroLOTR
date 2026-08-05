using UnityEngine;

// Inspector-bindable wrapper around OpportunityHexHinter's static toggle — a Button's
// OnClick (or a Toggle's OnValueChanged) can only reference a component instance, not a
// static method, so this is what you drag onto the UI element in the prefab.
public class OpportunityHintsToggle : MonoBehaviour
{
    public void SetHintsEnabled(bool enabled)
    {
        OpportunityHexHinter.SetHintsEnabled(enabled);
    }

    public void EnableHints()
    {
        OpportunityHexHinter.SetHintsEnabled(true);
    }

    public void DisableHints()
    {
        OpportunityHexHinter.SetHintsEnabled(false);
    }

    public void ToggleHints()
    {
        OpportunityHexHinter.SetHintsEnabled(!OpportunityHexHinter.HintsEnabled);
    }
}
