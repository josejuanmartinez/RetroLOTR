using System.Collections.Generic;
using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public Board board;
    public HexPathRenderer pathRenderer;

    private Game game;

    private void Start()
    {
        if(!board) board = FindFirstObjectByType<Board>();
        if(!game) game = FindFirstObjectByType<Game>();
        if(!pathRenderer) pathRenderer = FindFirstObjectByType<HexPathRenderer>();
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

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (board != null)
            {
                List<Character> characters = game.player.controlledCharacters;
                Character current = board.selectedCharacter;
                Character nextCharacter = null;

                if (characters != null && characters.Count > 0)
                {
                    int startIndex = characters.IndexOf(current);
                    if (startIndex < 0) startIndex = -1; // if current not in list

                    // Check everyone once, wrapping with modulo
                    for (int offset = 1; offset <= characters.Count; offset++)
                    {
                        int i = (startIndex + offset) % characters.Count;
                        var c = characters[i];

                        if (c != null && !c.killed && !c.hasActionedThisTurn)
                        {
                            nextCharacter = c;
                            break;
                        }
                    }
                }

                if (nextCharacter != null)
                {
                    board.SelectHex(nextCharacter.hex);
                    FindFirstObjectByType<BoardNavigator>()
                    .LookAt(nextCharacter.hex.transform.position, 1.0f, 0.0f);
                }

                return;
            }

            if (pathRenderer != null)
                pathRenderer.HidePath();
        }

    }
}
