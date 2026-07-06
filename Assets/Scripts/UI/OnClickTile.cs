using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OnClickTile : MonoBehaviour
{
    private Board board;
    private Hex hex;
    private static Board sharedBoard;

    private void Start()
    {
        if (sharedBoard == null) sharedBoard = FindFirstObjectByType<Board>();
        board = sharedBoard;
        hex = GetComponent<Hex>();
    }

    private void OnMouseDown()
    {
        // Handle left click (primary button)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverVisibleUIElement()) HandleLeftClick();
    }
    private void HandleLeftClick()
    {
        if (BoardNavigator.IsNavigationInputLocked()) return;
        if (PopupManager.IsShowing) return;
        // hex.v2 is this hex's dictionary key directly — no need to scan board.hexes
        // (4550 entries) by gameObject identity on every single click.
        if (board != null && hex != null)
        {
            try
            {
                Sounds.Instance?.PlayUiClick();
                board.SelectHex(hex.v2);
            }
            catch (Exception e)
            {
                Debug.LogError("Error selecting hex: " + e.Message);
            }
        }
    }

    private bool IsPointerOverVisibleUIElement()
    {
        if (EventSystem.current == null)
            return false;

        // Set up the new Pointer Event
        PointerEventData eventData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new();

        // Raycast using the Graphics Raycaster and the Event Data
        EventSystem.current.RaycastAll(eventData, results);

        // Only return true if we hit a visible UI element (not just the Canvas)
        foreach (var result in results)
        {
            // Skip the Canvas itself
            if (result.gameObject.GetComponent<Canvas>() != null)
                continue;

            // Check if it's an Image with non-zero alpha
            Image image = result.gameObject.GetComponent<Image>();
            if (image != null && image.color.a > 0.01f && image.raycastTarget)
                return true;

            // Check if it's Text with non-zero alpha
            TMPro.TextMeshProUGUI tmpText = result.gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null && tmpText.color.a > 0.01f)
                return true;
        }

        return false;
    }
}
