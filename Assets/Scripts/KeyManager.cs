using System.Collections.Generic;
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

        // Check if ESC key was pressed
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Character nextCharacter = null;
            if (board != null)
            {
                List<Character> characters = FindFirstObjectByType<Game>().player.controlledCharacters;

                if (board.selectedCharacter != null)
                {
                    var currentIndex = characters.IndexOf(board.selectedCharacter);
                    if (currentIndex == -1)
                    {
                        nextCharacter = characters[0];
                    }
                    else
                    {
                        var nextIndex = (currentIndex + 1) % characters.Count;
                        nextCharacter = characters[nextIndex];
                    }
                } 
                else
                {                   
                    if (characters.Count > 0) nextCharacter = characters[0];
                }

                if (nextCharacter != null)
                {
                    board.SelectHex(nextCharacter.hex);
                    FindFirstObjectByType<BoardNavigator>().LookAt(nextCharacter.hex.transform.position);
                }
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
