using UnityEngine;
using UnityEngine.UI;

// Inspector-bindable wrapper around OpportunityHexHinter's static toggle — a Button's
// OnClick (or a Toggle's OnValueChanged) can only reference a component instance, not a
// static method, so this is what you drag onto the UI element in the prefab.
public class OpportunityHintsToggle : MonoBehaviour
{
    private void OnEnable()
    {
        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(OpportunityHexHinter.HintsEnabled);
    }

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
