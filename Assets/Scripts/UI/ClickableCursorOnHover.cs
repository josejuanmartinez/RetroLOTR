using UnityEngine;
using UnityEngine.EventSystems;

// Drop-on component: shows the clickable mouse cursor while the pointer is over this
// UI element and restores the default when it leaves (or the element is disabled or
// destroyed mid-hover, e.g. a menu closing under the cursor).
public class ClickableCursorOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private bool hovered;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        CursorManager.Instance?.SetClickableCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        CursorManager.Instance?.SetDefaultCursor();
    }

    private void OnDisable()
    {
        if (!hovered) return;
        hovered = false;
        CursorManager.Instance?.SetDefaultCursor();
    }
}
