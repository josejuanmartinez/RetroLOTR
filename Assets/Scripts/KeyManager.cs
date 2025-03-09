using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public Board board;
    public HexPathRenderer pathRenderer;

    private void Start()
    {
        board = FindFirstObjectByType<Board>();
        pathRenderer = FindFirstObjectByType<HexPathRenderer>();
    }
    void Update()
    {
        // Check if ESC key was pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (board != null)
            {
                board.UnselectHex();
                return;
            }

            // Hide the path
            if (pathRenderer != null)
            {
                pathRenderer.HidePath();
            }

        }
    }
}
