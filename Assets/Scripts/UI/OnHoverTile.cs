using UnityEngine;

public class OnHoverTile : MonoBehaviour
{
    private Board board;
    private Hex hex;
    private Vector2 hexCoordinates; // Store this hex's coordinates
    private static HexPathRenderer pathRenderer;
    private static bool isRightMouseDown = false;
    private static Vector2 currentHoverCoordinates = Vector2.one * -1;

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
        // Highlight this hex
        if (hex != null) hex.Hover();

        // Store current hover coordinates
        currentHoverCoordinates = hexCoordinates;

        // If right mouse is already being held down, update path immediately
        UpdatePathRendering(board);
    }

    private void OnMouseExit()
    {
        // Remove highlight
        if (hex != null)
        {
            hex.Unhover();
        }

        // Clear hover coordinates if this is the current one
        if (currentHoverCoordinates == hexCoordinates)
        {
            currentHoverCoordinates = Vector2.one * -1;
            // Hide path when mouse exits
            if (pathRenderer != null)
            {
                pathRenderer.HidePath();
            }
        }
    }

    // Static method called from Update in PathManager
    public static void UpdateMouseState(bool rightMouseDown)
    {
        Board board = FindFirstObjectByType<Board>();
        // If right mouse button state changed
        if (isRightMouseDown != rightMouseDown)
        {
            isRightMouseDown = rightMouseDown;

            // If button is released, log movement
            if (!isRightMouseDown && pathRenderer != null && currentHoverCoordinates != Vector2.one * -1)
            {
                pathRenderer.HidePath();
                if (board.selectedCharacter != null && !board.selectedCharacter.hasMovedThisTurn)
                {
                    board.Move(board.selectedCharacter, currentHoverCoordinates);
                }
            }
            else if(board.selectedCharacter != null && !board.selectedCharacter.hasMovedThisTurn)
            {
                // Update path rendering based on new state
                UpdatePathRendering(board);
            }
        }
    }

    // Static method to handle path rendering
    private static void UpdatePathRendering(Board board)
    {
        if (pathRenderer == null) return;

        if (board == null) return;

        if (board.selectedCharacter == null) return;

        // Draw path only if right mouse is down, we have a hover position, and selected hex
        if (isRightMouseDown &&
            currentHoverCoordinates != Vector2.one * -1 &&
            board.selectedHex != Vector2.one * -1 &&
            board.selectedHex != currentHoverCoordinates)
        {
            pathRenderer.DrawPathBetweenHexes(board.selectedHex, currentHoverCoordinates, board.selectedCharacter.army != null);
        }
        else
        {
            pathRenderer.HidePath();
        }
    }
}