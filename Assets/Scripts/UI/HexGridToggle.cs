using UnityEngine;
using UnityEngine.UI;

// Inspector-bindable wrapper around Board's static singleton — a Toggle's OnValueChanged
// can only reference a component instance, not Board.Instance directly, so this is what
// you drag onto the UI element in the prefab.
public class HexGridToggle : MonoBehaviour
{
    private void OnEnable()
    {
        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null && Board.Instance != null)
            toggle.SetIsOnWithoutNotify(Board.Instance.showHexGrid);
    }

    public void SetHexGridEnabled(bool enabled)
    {
        if (Board.Instance != null) Board.Instance.SetHexGridEnabled(enabled);
    }
}
