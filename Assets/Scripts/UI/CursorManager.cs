using UnityEngine;
using UnityEngine.EventSystems;

public class CursorManager : MonoBehaviour
{
    public Texture2D uiCursor;
    public Texture2D defaultCursor;
    public Vector2 hotSpot = Vector2.zero;

    void Update()
    {
        // Check if mouse is over any UI element
        /*if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (CursorTextureCurrently(defaultCursor))
                Cursor.SetCursor(uiCursor, hotSpot, CursorMode.Auto);
        }
        else
        {
            if (CursorTextureCurrently(uiCursor))
                Cursor.SetCursor(defaultCursor, hotSpot, CursorMode.Auto);
        }*/
    }
    /*
    private bool CursorTextureCurrently(Texture2D tex)
    {
        // There's no direct way to get current cursor texture,
        // so you can track it manually instead.
        return true;
    }*/
}
