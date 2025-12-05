using System.Collections.Generic;
using System.Linq;
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
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (ctrlHeld && shiftHeld)
        {
            Debug.Log("CTRL+Shift shortcuts: CTRL+Shift+R reveal map, CTRL+Shift+M reset movement, CTRL+Shift+S +100 stores, CTRL+Shift+A list artifacts");
        }

        if (ctrlHeld && shiftHeld && Input.GetKeyDown(KeyCode.R))
        {
            RevealEntireMap();
            return;
        }

        if (ctrlHeld && shiftHeld && Input.GetKeyDown(KeyCode.M))
        {
            ResetAllMovement();
            return;
        }

        if (ctrlHeld && shiftHeld && Input.GetKeyDown(KeyCode.S))
        {
            BoostPlayerStores();
            return;
        }

        if (ctrlHeld && shiftHeld && Input.GetKeyDown(KeyCode.A))
        {
            LogAllArtifacts();
            return;
        }

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

                if (nextCharacter != null) board.SelectCharacter(nextCharacter);

                return;
            }

            if (pathRenderer != null)
                pathRenderer.HidePath();
        }

    }

    private void RevealEntireMap()
    {
        if (!board) board = FindFirstObjectByType<Board>();
        if (!game) game = FindFirstObjectByType<Game>();

        if (board == null || board.hexes == null)
        {
            Debug.LogWarning("Cannot reveal map: board or hexes are missing.");
            return;
        }

        Leader playerLeader = game != null ? game.player : null;
        
        foreach (Hex hex in board.hexes.Values)
        {
            if (hex != null) {
                if(hex.HasAnyPC() && !hex.IsPCRevealed()) hex.RevealPC();
                hex.Reveal(playerLeader);
            }

        }

        MinimapManager.RefreshMinimap();
        Debug.Log("CTRL+Shift+R -> Revealed the entire map");
    }

    private void ResetAllMovement()
    {
        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        foreach (Character character in characters)
        {
            if (character == null || character.killed) continue;
            character.moved = 0;
        }
        Debug.Log("CTRL+Shift+M -> Reset movement for all characters");
    }

    private void BoostPlayerStores(int amount = 100)
    {
        if (!game) game = FindFirstObjectByType<Game>();
        var player = game != null ? game.player : null;
        if (player == null)
        {
            Debug.LogWarning("Cannot boost stores: player not found.");
            return;
        }

        player.leatherAmount += amount;
        player.mountsAmount += amount;
        player.timberAmount += amount;
        player.ironAmount += amount;
        player.mithrilAmount += amount;
        player.goldAmount += amount;

        var storesManager = FindFirstObjectByType<StoresManager>();
        if (storesManager != null) storesManager.RefreshStores();

        Debug.Log($"CTRL+Shift+S -> Added {amount} to all player stores");
    }

    private void LogAllArtifacts()
    {
        List<string> entries = new();

        foreach (var c in FindObjectsByType<Character>(FindObjectsSortMode.None))
        {
            if (c == null || c.artifacts == null || c.artifacts.Count == 0) continue;
            string ownerName = string.IsNullOrWhiteSpace(c.characterName) ? c.name : c.characterName;
            foreach (var art in c.artifacts)
            {
                if (art == null) continue;
                entries.Add($"{art.artifactName} held by {ownerName}");
            }
        }

        foreach (var hex in FindObjectsByType<Hex>(FindObjectsSortMode.None))
        {
            if (hex == null || hex.hiddenArtifacts == null || hex.hiddenArtifacts.Count == 0) continue;
            string hexLabel = hex.GetHoverV2();
            foreach (var art in hex.hiddenArtifacts)
            {
                if (art == null) continue;
                entries.Add($"{art.artifactName} hidden at {hexLabel}");
            }
        }

        if (!game) game = FindFirstObjectByType<Game>();
        if (game != null && game.artifacts != null && game.artifacts.Count > 0)
        {
            foreach (var art in game.artifacts)
            {
                if (art == null) continue;
                entries.Add($"{art.artifactName} (game list)");
            }
        }

        if (entries.Count == 0)
        {
            Debug.Log("CTRL+Shift+A -> No artifacts found");
        }
        else
        {
            Debug.Log("CTRL+Shift+A -> Artifacts:\n" + string.Join("\n", entries));
        }
    }
}
