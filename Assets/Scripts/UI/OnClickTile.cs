using System;
using UnityEngine;

public class OnClickTile : MonoBehaviour
{
    private Board board;
    private void Start()
    {
        board = FindFirstObjectByType<Board>();
    }

    private void OnMouseDown()
    {
        // Handle left click (primary button)
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }
    }
    private void HandleLeftClick()
    {
        if (board != null)
        {
            if (board.selectedHex != Vector2.one * -1)
            {
                board.UnselectHex();
                // MOVEMENT
                return;
            }
            try
            {
                // Find this hex's Vector2 position in the dictionary
                foreach (var entry in board.hexes)
                {
                    if (entry.Value.gameObject == gameObject)
                    {
                        Vector2 hexPosition = entry.Key;
                        // Call the Select method with this position
                        board.SelectHex(hexPosition);
                        // Optional: Log for debugging
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error selecting hex: " + e.Message);
            }
        }
    }
}