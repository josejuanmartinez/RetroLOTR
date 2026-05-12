using UnityEngine;
using UnityEngine.EventSystems;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    public Texture2D clickableCursor;
    public Texture2D draggableCursor;
    public Texture2D defaultCursor;
    public Texture2D waitingCursor;
    public Texture2D disableCursor;
    public Vector2 hotSpot = Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetDraggableCursor()
    {
        if (draggableCursor != null)
            Cursor.SetCursor(draggableCursor, hotSpot, CursorMode.Auto);
    }

    public void SetDisableCursor()
    {
        if (disableCursor != null)
            Cursor.SetCursor(disableCursor, hotSpot, CursorMode.Auto);
    }

    public void SetDefaultCursor()
    {
        if (defaultCursor != null)
            Cursor.SetCursor(defaultCursor, hotSpot, CursorMode.Auto);
        else
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
