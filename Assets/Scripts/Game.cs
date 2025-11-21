using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using NUnit.Framework.Constraints;
using System.Threading.Tasks;


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
    public static int MAX_ARTIFACTS = 40;
    public static int MAX_CHARACTERS = 100;
    public static int MAX_PCS = 50;
    public static int MAX_TURNS = 200;
    

    [Header("Starting info")]
    public int turn = 0;
    public bool started = false;

    private Board board;
    void Awake()
    {
        if (!board) board = FindAnyObjectByType<Board>();
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

        board.StartGame();
        AssignAIandHumans();
        currentlyPlaying.NewTurn();

        soundPlayer.PlayOneShot(FindFirstObjectByType<Sounds>().GetSoundByName($"{currentlyPlaying.alignment}_intro"));
        PopupManager.Show(
            currentlyPlaying.GetBiome().joinedTitle,
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(currentlyPlaying.GetBiome().introActor1),
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(currentlyPlaying.GetBiome().introActor2),
            currentlyPlaying.GetBiome().joinedText,
            true
        );
    }

    public bool PointToCharacterWithMissingActions()
    {
        // Make sure all characters have actioned
        Character stillNotActioned = player.controlledCharacters.Find(x => !x.hasActionedThisTurn && !x.killed && board.selectedCharacter != x);
        if ( stillNotActioned != null) board.SelectCharacter(stillNotActioned, true, 1.0f, 2.0f);
        return stillNotActioned != null;
    }

    public async void NextPlayer()
    {
        if (currentlyPlaying == player)
        {
            
            if (PointToCharacterWithMissingActions())
            {
                if(await ConfirmationDialog.AskYesNo("Some characters have not actioned this turn. Finish the turn anyway?")==false)
                {
                    return;
                }
            }

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
            EndGame(player != null && !player.killed);
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
        board.RefreshRelevantHexes();
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

    public bool IsPlayerCurrentlyPlaying()
    {
        return currentlyPlaying == player;
    }
}
