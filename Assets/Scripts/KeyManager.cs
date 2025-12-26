using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public Board board;
    public HexPathRenderer pathRenderer;
    public BoardNavigator boardNavigator;
    public float keyboardMoveSpeed = 10f;

    private Game game;
    private ActionsManager actionsManager;
    private List<(KeyCode key, char letter)> actionHotkeys;

    private void Start()
    {
        if(!board) board = FindFirstObjectByType<Board>();
        if(!game) game = FindFirstObjectByType<Game>();
        if(!pathRenderer) pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        if(!boardNavigator) boardNavigator = FindFirstObjectByType<BoardNavigator>();
        if(!actionsManager) actionsManager = FindFirstObjectByType<Layout>()?.GetActionsManager() ?? FindFirstObjectByType<ActionsManager>();
        BuildActionHotkeys();
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
            if (PopupManager.IsShowing)
            {
                PopupManager.HidePopup();
                return;
            }

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
            if (board != null && game != null)
            {
                game.SelectNextCharacterInPriorityCycle();
                return;
            }

            if (pathRenderer != null)
                pathRenderer.HidePath();
        }

        HandleActionHotkeys(ctrlHeld, shiftHeld);
        HandleCharacterMovementHotkeys(ctrlHeld, shiftHeld);
        HandleKeyboardCameraMovement();
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
        player.steelAmount += amount;
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

    private void HandleKeyboardCameraMovement()
    {
        if (boardNavigator == null) return;
        if (BoardNavigator.IsPointerOverVisibleUIElement()) return;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.up;
        if (Input.GetKey(KeyCode.S)) move += Vector3.down;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;

        if (move.sqrMagnitude > 0f)
        {
            move.Normalize();
            boardNavigator.transform.position += move * keyboardMoveSpeed * Time.deltaTime;
        }
    }

    private void HandleActionHotkeys(bool ctrlHeld, bool shiftHeld)
    {
        if (ctrlHeld || shiftHeld) return;

        if (actionsManager == null)
        {
            actionsManager = FindFirstObjectByType<Layout>()?.GetActionsManager() ?? FindFirstObjectByType<ActionsManager>();
            if (actionsManager == null) return;
            BuildActionHotkeys();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            actionsManager.PreviousPage();
            return;
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            actionsManager.NextPage();
            return;
        }

        foreach (var hotkey in actionHotkeys)
        {
            if (Input.GetKeyDown(hotkey.key))
            {
                actionsManager.ExecuteActionByHotkey(hotkey.letter);
                return;
            }
        }
    }

    private void HandleCharacterMovementHotkeys(bool ctrlHeld, bool shiftHeld)
    {
        if (ctrlHeld || shiftHeld) return;
        if (!board) board = FindFirstObjectByType<Board>();
        if (board == null || board.selectedCharacter == null || board.selectedCharacter.hex == null) return;
        if (board.moving) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            MoveSelectedCharacter(HexMoveDirection.Left);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Keypad6))
        {
            MoveSelectedCharacter(HexMoveDirection.Right);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad7))
        {
            MoveSelectedCharacter(HexMoveDirection.UpLeft);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            MoveSelectedCharacter(HexMoveDirection.UpRight);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            MoveSelectedCharacter(HexMoveDirection.DownLeft);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            MoveSelectedCharacter(HexMoveDirection.DownRight);
        }
    }

    private void BuildActionHotkeys()
    {
        actionHotkeys = new List<(KeyCode key, char letter)>();
        if (ActionsManager.ActionHotkeyLetters == null) return;

        foreach (char letter in ActionsManager.ActionHotkeyLetters)
        {
            if (Enum.TryParse(letter.ToString(), out KeyCode key))
            {
                actionHotkeys.Add((key, letter));
            }
        }
    }

    private enum HexMoveDirection
    {
        UpRight,
        Right,
        DownRight,
        DownLeft,
        Left,
        UpLeft
    }

    private void MoveSelectedCharacter(HexMoveDirection direction)
    {
        Character character = board.selectedCharacter;
        Hex currentHex = character != null ? character.hex : null;
        if (currentHex == null) return;

        Vector2 directionVector = GetDirectionVector(direction);
        Vector2Int? target = GetNeighborInDirection(currentHex, directionVector);
        if (!target.HasValue) return;

        board.Move(character, target.Value);
    }

    private Vector2Int? GetNeighborInDirection(Hex currentHex, Vector2 directionVector)
    {
        if (board == null || board.hexes == null) return null;

        Vector2Int[] neighbors = (currentHex.v2.x & 1) == 0 ? board.evenRowNeighbors : board.oddRowNeighbors;
        Vector2 currentPos = currentHex.transform.position;
        float bestDot = -1f;
        Vector2Int? best = null;

        foreach (Vector2Int offset in neighbors)
        {
            Vector2Int neighborPos = currentHex.v2 + offset;
            if (!board.hexes.TryGetValue(neighborPos, out Hex neighborHex)) continue;

            Vector2 delta = (Vector2)neighborHex.transform.position - currentPos;
            if (delta.sqrMagnitude <= Mathf.Epsilon) continue;

            float dot = Vector2.Dot(delta.normalized, directionVector);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = neighborPos;
            }
        }

        return bestDot >= 0.7f ? best : null;
    }

    private static Vector2 GetDirectionVector(HexMoveDirection direction)
    {
        return direction switch
        {
            HexMoveDirection.Left => Vector2.left,
            HexMoveDirection.Right => Vector2.right,
            HexMoveDirection.UpLeft => (Vector2.up + Vector2.left).normalized,
            HexMoveDirection.UpRight => (Vector2.up + Vector2.right).normalized,
            HexMoveDirection.DownLeft => (Vector2.down + Vector2.left).normalized,
            HexMoveDirection.DownRight => (Vector2.down + Vector2.right).normalized,
            _ => Vector2.zero
        };
    }
}
