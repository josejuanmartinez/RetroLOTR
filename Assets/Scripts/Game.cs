using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.MLAgents;

[RequireComponent(typeof(GameState))]
public class Game : MonoBehaviour
{
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

    public int normalMovement = 12;
    public int cavalryMovement = 15;
    public int maxPcsPerPlayer = 8;
    public int maxCharactersPerPlayer = 8;

    public int turn = 0;
    public bool started = false;

    GameState state;
    Dictionary<Character, StrategyGameAgent> characterAgents = new();

    void Awake()
    {
        state = GetComponent<GameState>();
    }

    void Start()
    {
        // InitializeMLAgents();
    }

    private void InitializeMLAgents()
    {
        // Find all ML-Agents in the scene
        List<Character> allCharactersWithAgents = FindObjectsByType<Character>(FindObjectsSortMode.None).ToList().FindAll(x => x.GetOwner() != player && x.GetAI() != null);

        // Map each agent to its controlled character
        foreach (Character character in allCharactersWithAgents)
        {
            if (character.GetAI() != null) characterAgents[character] = character.GetAI();
        }

        Debug.Log($"Initialized {characterAgents.Count} ML-Agents for training");
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
        state.ResetGame();

        // Start a new episode for all agents
        foreach (StrategyGameAgent agent in characterAgents.Values) agent.OnEpisodeBegin();

        currentlyPlaying = player;
        FindFirstObjectByType<Board>().StartGame();
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
            if (MoveToNextCharacterToAction()) return;

            // Find the first non-killed competitor
            currentlyPlaying = FindNextAliveCompetitor(0);
            if (currentlyPlaying == null)
            {
                EndGame(true);
                return;
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
        }
        else
        {
            if (currentlyPlaying == player) MessageDisplay.ShowMessage($"Turn {turn++}", Color.green);
            currentlyPlaying.NewTurn();
        }
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