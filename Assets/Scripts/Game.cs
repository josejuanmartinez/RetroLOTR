using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(GameState))]
public class Game : MonoBehaviour
{
    [Header("Training mode")]
    public bool trainingMode = true;

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

    [Header("MAX CAPS")]
    public int normalMovement = 12;
    public int cavalryMovement = 15;

    public static int MAX_LEADERS = 30;
    public static int MAX_BOARD_WIDTH = 25;
    public static int MAX_BOARD_HEIGHT = 75;
    public static int MAX_ARTIFACTS = 40;
    public static int MAX_CHARACTERS = 100;
    public static int MAX_PCS = 50;
    public static int MAX_TURNS = 200;
    

    [Header("Starting info")]
    public int turn = 0;
    public bool started = false;

    [Header("AI")]
    public GameObject strategyGameAgentCharacterPrefab;

    GameState state;
    Dictionary<Character, StrategyGameAgent> characterAgents = new();

    void Awake()
    {
        state = GetComponent<GameState>();
    }

    private void InitializeCharactersAI()
    {
        // Find all ML-Agents in the scene
        List<Character> allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None).ToList();
        foreach(Character character in allCharacters)
        {
            character.isPlayerControlled = character.GetOwner() == player;
            GameObject ai = Instantiate(strategyGameAgentCharacterPrefab, character.transform);
            characterAgents[character] = character.GetAI();
        }

        Debug.Log($"Initialized {characterAgents.Count} Characters AI");
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
        
        FindFirstObjectByType<Board>().StartGame();
        InitializeCharactersAI();
        state.InitializeGameState();
        currentlyPlaying.NewTurn();
    }



    public bool MoveToNextCharacterToAction()
    {
        // Make sure all characters have actioned
        Character stillNotActioned = player.controlledCharacters.Find(x => !x.hasActionedThisTurn && !x.killed && FindFirstObjectByType<Board>().selectedCharacter != x);
        if (stillNotActioned != null) FindFirstObjectByType<Board>().SelectCharacter(stillNotActioned);
        return stillNotActioned != null;
    }

    public void NextPlayer()
    {
        if (currentlyPlaying == player)
        {
            if (trainingMode)
            {
                currentlyPlaying = FindNextAliveCompetitor(0);
            }
            else
            {
                if (MoveToNextCharacterToAction()) return;

                // Find the first non-killed competitor
                currentlyPlaying = FindNextAliveCompetitor(0);
                if (currentlyPlaying == null)
                {
                    EndGame(true);
                    return;
                }
            }
        }
        else
        {
            int currentIndex = competitors.IndexOf(currentlyPlaying);

            // Find the next non-killed competitor
            currentlyPlaying = FindNextAliveCompetitor(currentIndex + 1);
            if (currentlyPlaying == null && !player.killed) currentlyPlaying = player;
        }


        if (currentlyPlaying == null)
        {
            EndGame(player.killed);
            return;
        }

        if (currentlyPlaying == player)
        {
            turn++;
            if (turn >= MAX_TURNS)
            {
                EndGame(false);
                return;
            }
            MessageDisplay.ShowMessage($"Turn {turn++}", Color.green);
        }
        FindFirstObjectByType<Board>().RefreshRelevantHexes();
        currentlyPlaying.NewTurn();
    }

    // Helper method to find the next alive competitor
    private PlayableLeader FindNextAliveCompetitor(int startIndex)
    {
        for (int i = startIndex; i < competitors.Count; i++)
        {
            if (!competitors[i].killed)
            {
                return competitors[i];
            }
        }

        return null;
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

    private void ResetForNewEpisode()
    {
        // Reset game state variables
        turn = 0;

        // Start a new game
        StartGame();
    }
}