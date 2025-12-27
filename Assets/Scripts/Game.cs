using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Game : MonoBehaviour
{
    [Header("Sound")]
    public AudioSource soundPlayer;
    public AudioSource musicPlayer;

    [Header("Playable Leader (Player)")]
    public PlayableLeader player;
    [Header("Other Playable Leaders")]
    public List<PlayableLeader> competitors;
    [Header("Non Playable Leaders")]
    public List<NonPlayableLeader> npcs;
    [Header("Currently Playing")]
    public PlayableLeader currentlyPlaying;
    [Header("Artifacts")]
    public List<Artifact> artifacts = new();

    [Header("Movement")]
    public int characterMovement = 5;
    public int armyMovement = 5;
    public int cavalryMovement = 7;

    [Header("Caps")]
    public static int MAX_LEADERS = 30;
    public static int MAX_BOARD_WIDTH = 25;
    public static int MAX_BOARD_HEIGHT = 75;
    public static int MAX_ARTIFACTS = 100;
    public static int MAX_CHARACTERS = 100;
    public static int MAX_PCS = 50;
    public static int MAX_TURNS = 200;

    [Header("References")]
    public StoresManager storesManager;
    public Board board;
    public CharacterIcons icons;


    [Header("Starting info")]
    public int turn = 0;
    public bool started = false;

    public event Action<int> NewTurnStarted;

    private bool skipNextTurnPrompt = false;
    void Awake()
    {
        if (!board) board = FindAnyObjectByType<Board>();
        if (!storesManager) storesManager = FindAnyObjectByType<StoresManager>();
        if (AIContextCacheManager.Instance == null) gameObject.AddComponent<AIContextCacheManager>();
    }

    private void AssignAIandHumans()
    {
        // Find all ML-Agents in the scene
        List<Character> allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None).ToList();
        foreach(Character character in allCharacters)
        {
            character.isPlayerControlled = character.GetOwner() == player;
        }
    }

    public void SelectPlayer(PlayableLeader playableLeader)
    {
        competitors = new();
        npcs = new();
        player = playableLeader;
        foreach (PlayableLeader otherPlayableLeader in FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None))
        {
            if (otherPlayableLeader == playableLeader) continue;
            competitors.Add(otherPlayableLeader);
        }
        foreach (NonPlayableLeader nonPlayableLeader in FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None))
        {
            npcs.Add(nonPlayableLeader);
        }
    }

    public void StartGame()
    {
        turn = 0;
        started = true;

        currentlyPlaying = player;
        MessageDisplay.ClearPersistent();

        InitializePlayableLeaderIcons();
        InitializeNonPlayableLeaderIcons();
        board.StartGame();
        AssignAIandHumans();
        VictoryPoints.RecalculateAndAssign(this);
        RefreshPlayableLeaderIconVictoryPoints();
        AIContextCacheManager.Instance?.BeginPlayerTurnPrecompute(this);
        NewTurnStarted?.Invoke(turn);
        currentlyPlaying.NewTurn();
        BuildPlayerCharacterIcons();
        SelectFirstPlayerCharacter();

        soundPlayer.PlayOneShot(FindFirstObjectByType<Sounds>().GetSoundByName($"{currentlyPlaying.alignment}_intro"));
        PopupManager.Show(
            currentlyPlaying.GetBiome().joinedTitle,
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(currentlyPlaying.GetBiome().introActor1),
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(currentlyPlaying.GetBiome().introActor2),
            currentlyPlaying.GetBiome().joinedText,
            true
        );
    }

    private void InitializePlayableLeaderIcons()
    {
        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (leaderIcons == null) return;

        if (player != null) leaderIcons.Instantiate(player);
        if (competitors == null) return;

        foreach (PlayableLeader competitor in competitors)
        {
            if (competitor != null) leaderIcons.Instantiate(competitor);
        }
    }

    private void InitializeNonPlayableLeaderIcons()
    {
        foreach (NonPlayableLeader nonPlayableLeader in FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None))
        {
            if (nonPlayableLeader != null) nonPlayableLeader.InitializeIcons();
        }
    }

    private void RefreshPlayableLeaderIconVictoryPoints()
    {
        PlayableLeaderIcons leaderIcons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (leaderIcons == null) return;
        leaderIcons.RefreshVictoryPointsForAll();
        leaderIcons.UpdateVictoryPointColors();
    }

    public bool PointToCharacterWithMissingActions()
    {
        // Make sure all characters have actioned
        Character stillNotActioned = player.controlledCharacters.Find(x => !x.hasActionedThisTurn && !x.killed && board.selectedCharacter != x);
        if ( stillNotActioned != null) board.SelectCharacter(stillNotActioned, true, 1.0f, 2.0f);
        return stillNotActioned != null;
    }

    public async void SelectNextCharacterOrFinishTurnPrompt()
    {
        if (!IsPlayerCurrentlyPlaying() || player == null || board == null) return;

        List<Character> characters = player.controlledCharacters;
        if (characters == null || characters.Count == 0) return;

        Character current = board.selectedCharacter;
        Character nextCharacter = null;

        // Try to find the next character without an action, cycling from current selection
        int startIndex = characters.IndexOf(current);
        if (startIndex < 0) startIndex = -1;
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

        if (nextCharacter != null)
        {
            board.SelectCharacter(nextCharacter);
            return;
        }

        // No characters with free actions; offer to finish turn
        bool finish = await ConfirmationDialog.Ask(
            "No more characters with free actions are available. You may still have movement left. Finish the turn?",
            "Finish Turn",
            "Cancel");
        if (finish)
        {
            skipNextTurnPrompt = true;
            NextPlayer();
        }
    }

    public void SelectNextCharacterInPriorityCycle()
    {
        if (!IsPlayerCurrentlyPlaying() || player == null || board == null) return;

        List<Character> characters = player.controlledCharacters;
        if (characters == null || characters.Count == 0) return;

        List<Character> ordered = new();
        ordered.AddRange(characters.Where(c => c != null && !c.killed && !c.hasActionedThisTurn));
        ordered.AddRange(characters.Where(c => c != null && !c.killed && c.hasActionedThisTurn && c.moved < c.GetMaxMovement()));
        ordered.AddRange(characters.Where(c => c != null && !c.killed && c.hasActionedThisTurn && c.moved >= c.GetMaxMovement()));

        if (ordered.Count == 0) return;

        Character current = board.selectedCharacter;
        int currentIndex = ordered.IndexOf(current);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % ordered.Count : 0;
        Character next = ordered[nextIndex];
        if (next != null) board.SelectCharacter(next);
    }

    public async void NextPlayer()
    {
        bool shouldPrompt = currentlyPlaying == player && !skipNextTurnPrompt;
        skipNextTurnPrompt = false;

        if (shouldPrompt)
        {
            bool hasPendingActions = player.controlledCharacters.Any(x => !x.killed && !x.hasActionedThisTurn);
            string message = hasPendingActions
                ? "Some characters have not actioned yet. End turn?"
                : "End turn?";

            bool finishTurn = await ConfirmationDialog.Ask(message, "Finish Turn", "Cancel");
            if (!finishTurn)
            {
                Character nextCharacter = player.controlledCharacters.Find(x => !x.killed && !x.hasActionedThisTurn);
                if (nextCharacter != null)
                {
                    board.SelectCharacter(nextCharacter, true, 1.0f, 0.0f);
                }
                else
                {
                    Character firstAlive = player.controlledCharacters.Find(x => !x.killed);
                    if (firstAlive != null) board.SelectCharacter(firstAlive, true, 1.0f, 0.0f);
                }
                return;
            }
        }

        PlayableLeader next = FindNextTurnLeader(currentlyPlaying);

        // If no one else alive and player alive, victory; otherwise defeat
        if (next == null)
        {
            EndGame(player != null && !player.killed);
            return;
        }

        // If the only remaining leader is the player and there are no competitors left, end game as win
        if (next == player && !player.killed && competitors.All(c => c == null || c.killed))
        {
            EndGame(true);
            return;
        }

        currentlyPlaying = next;

        if (currentlyPlaying == player)
        {
            NewTurn();
        }
        else
        {
            HideSelectedCharacterIcon();
            MessageDisplay.ShowPersistent($"{currentlyPlaying.characterName} is playing", Color.yellow);
        }
        board.RefreshRelevantHexes();
        currentlyPlaying.NewTurn();

        if (currentlyPlaying == player)
        {
            SelectFirstPlayerCharacter();
        }
    }

    private void NewTurn()
    {
        MessageDisplay.ClearPersistent();
        turn++;
        if (turn >= MAX_TURNS)
        {
            EndGame(false);
            return;
        }
        board?.ClearAllScouting();
        MessageDisplay.ShowMessage($"Turn {turn}", Color.green);
        NewTurnStarted?.Invoke(turn);
        AIContextCacheManager.Instance?.BeginPlayerTurnPrecompute(this);
        storesManager.AdvanceTurn();
    }

    private void HideSelectedCharacterIcon()
    {
        SelectedCharacterIcon selected = FindFirstObjectByType<SelectedCharacterIcon>();
        selected?.Hide();
        Layout layout = FindFirstObjectByType<Layout>();
        if (layout != null)
        {
            layout.GetActionsManager()?.Hide();
        }
    }

    private void BuildPlayerCharacterIcons()
    {
        if (player == null) return;
        icons?.BuildIconsForPlayer(player);
    }

    private void SelectFirstPlayerCharacter()
    {
        if (player == null || board == null) return;

        Character firstAlive = player.controlledCharacters
            .FirstOrDefault(c => c != null && !c.killed);

        if (firstAlive != null)
        {
            board.SelectCharacter(firstAlive, true, 1.0f, 0.0f);
        }
        else
        {
            HideSelectedCharacterIcon();
        }
    }

    // Helper method to find the next alive leader in turn order
    private PlayableLeader FindNextTurnLeader(PlayableLeader current)
    {
        List<PlayableLeader> order = new();
        if (player != null && !player.killed) order.Add(player);
        if (competitors != null) order.AddRange(competitors.Where(c => c != null && !c.killed));

        if (order.Count == 0) return null;

        int currentIndex = order.IndexOf(current);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % order.Count : 0;
        return order[nextIndex];
    }

    // Add this method to handle game ending
    public void EndGame(bool win)
    {
        if (win) MessageDisplay.ShowMessage("Victory!", Color.green); else MessageDisplay.ShowMessage("Defeat!", Color.red);

        //FindObjectsByType<Character>(FindObjectsSortMode.None).ToList().FindAll(x => !x.killed && x.GetAI() != null).Select(x => x.GetAI()).ToList().ForEach(x =>
        //{
        //    x.AddReward(x.GetCharacter().GetOwner().killed ? -25f : 25f);
        //    x.EndEpisode();
        //});

        // For training, we'll start a new game instead of quitting
        //if (Academy.Instance.IsCommunicatorOn)
        //{
            // Reset the game state for a new episode
        //    ResetForNewEpisode();
        //    return;
        //}

        // Only quit or unload scenes if not in training mode
        //UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(0);
        Application.Quit();
        Debug.Log("Game Ended!");
    }

    public bool IsPlayerCurrentlyPlaying()
    {
        return currentlyPlaying == player;
    }
}
