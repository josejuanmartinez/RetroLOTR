using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OnHoverTile : MonoBehaviour
{
    private Board board;
    private Hex hex;
    private Vector2Int hexCoordinates; // Store this hex's coordinates
    private static HexPathRenderer pathRenderer;
    private static bool isRightMouseDown = false;
    private static Vector2Int currentHoverCoordinates = Vector2Int.one * -1;

    void Start()
    {
        board = FindFirstObjectByType<Board>();
        if (board != null)
        {
            var hexes = board.hexes;
            // Find our own hex coordinates
            foreach (var hexPair in hexes)
            {
                if (hexPair.Value.gameObject == gameObject)
                {
                    hexCoordinates = hexPair.Key;
                    break;
                }
            }
        }
        else
        {
            Debug.LogError("Board component not found!");
        }

        // Get the Hex component
        hex = GetComponent<Hex>();

        // Find or create the path renderer (only once)
        if (pathRenderer == null)
        {
            pathRenderer = FindFirstObjectByType<HexPathRenderer>();
            if (pathRenderer == null)
            {
                GameObject pathRendererObj = new("HexPathRenderer");
                pathRenderer = pathRendererObj.AddComponent<HexPathRenderer>();
            }
        }
    }

    private void OnMouseEnter()
    {
        if (board == null) return;

        if (FindFirstObjectByType<Layout>() != null)
        {
            try
            {
                FindFirstObjectByType<Layout>().GetHexNumberManager().Show(hexCoordinates);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        

        if (IsPointerOverVisibleUIElement())
        {
            if (hex != null) hex.Unhover();
            return;
        }
        // Highlight this hex
        if (hex != null) hex.Hover();

        // Store current hover coordinates
        currentHoverCoordinates = hexCoordinates;

        // If right mouse is already being held down, update path immediately
        if (board.selectedCharacter != null && board.selectedCharacter.moved < board.selectedCharacter.GetMaxMovement())
        {
            UpdatePathRendering(board);
        }
            
    }

    private void OnMouseExit()
    {
        if (IsPointerOverVisibleUIElement())
        {
            if (hex != null) hex.Unhover();
            return;
        }

        // Remove highlight
        if (hex != null) hex.Unhover();

        // Clear hover coordinates if this is the current one
        if (currentHoverCoordinates == hexCoordinates)
        {
            currentHoverCoordinates = Vector2Int.one * -1;
            // Hide path when mouse exits
            if (pathRenderer != null) pathRenderer.HidePath();
        }
    }

    // Static method called from Update in PathManager
    public static void UpdateMouseState(bool rightMouseDown)
    {
        Board board = FindFirstObjectByType<Board>();

        if (IsPointerOverVisibleUIElement() || board.moving)
        {
            if(pathRenderer) pathRenderer.HidePath();
            if (board)
            {
                Hex hex = board.GetHex(currentHoverCoordinates);
                if (hex) hex.Unhover();
            }
            return;
        }

        // If right mouse button state changed
        if (isRightMouseDown != rightMouseDown)
        {
            isRightMouseDown = rightMouseDown;

            // If button is released, log movement
            if (!isRightMouseDown && pathRenderer != null && currentHoverCoordinates != Vector2.one * -1)
            {
                pathRenderer.HidePath();
                if (board.selectedCharacter != null && board.selectedCharacter.moved < board.selectedCharacter.GetMaxMovement())
                {
                    board.Move(board.selectedCharacter, currentHoverCoordinates);
                } else
                {
                    pathRenderer.HidePath();
                }
            }
            else if(board.selectedCharacter != null && board.selectedCharacter.moved < board.selectedCharacter.GetMaxMovement())
            {
                // Update path rendering based on new state
                UpdatePathRendering(board);
            }
            else
            {
                pathRenderer.HidePath();
            }
        }
    }

    // Static method to handle path rendering
    private static void UpdatePathRendering(Board board)
    {
        if (IsPointerOverVisibleUIElement())
        {
            Debug.Log("IGNORING HEX");
            pathRenderer.HidePath();
            return;
        }

        if (pathRenderer == null) return;

        if (board == null) return;

        if (board.selectedCharacter == null) return;

        // Draw path only if right mouse is down, we have a hover position, and selected hex
        if (isRightMouseDown &&
            currentHoverCoordinates != Vector2.one * -1 &&
            board.selectedHex != Vector2.one * -1 &&
            board.selectedHex != currentHoverCoordinates)
        {
            pathRenderer.DrawPathBetweenHexes(
                board.selectedHex, 
                currentHoverCoordinates,
                board.selectedCharacter);
        }
        else
        {
            pathRenderer.HidePath();
        }
    }
    private static bool IsPointerOverVisibleUIElement()
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