using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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
    [Header("Next Button")]
    public TextMeshProUGUI nextButton;

    public int normalMovement = 12;
    public int cavalryMovement = 15;
    public int maxPcsPerPlayer = 8;
    public int maxCharactersPerPlayer = 8;

    public int turn = 0;

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
        NewTurn();
    }

    public void NewTurn()
    {
        turn++;

        // Check if player is killed
        if (player.killed || player.controlledCharacters.Count < 1 || player.controlledPcs.Count < 1)
        {
            player.killed = true;
            // End the game if player is killed
            EndGame();
            return;
        }
        MessageDisplay.ShowMessage($"Turn {turn}", Color.green);

        currentlyPlaying = player;
        FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(currentlyPlaying);
        player.NewTurn();

        // Only process competitors who aren't killed
        competitors.ForEach(x => { if (!x.killed) x.NewTurn(); });

        // Start the coroutine to refresh hexes in the background
        StartCoroutine(player.RevealVisibleHexesAsync(() =>
        {
            FindFirstObjectByType<Board>().SelectCharacter(player);
        }
        ));
    }

    public void NextPlayer()
    {
        
        if (currentlyPlaying == player)
        {
            // Make sure all characters have actioned
            Character stillNotActioned = player.controlledCharacters.Find(x => !x.hasActionedThisTurn);
            if (stillNotActioned != null)
            {
                nextButton.text = "Awaiting Orders";
                FindFirstObjectByType<Board>().SelectCharacter(stillNotActioned);
                return;
            }
            nextButton.text = "End Turn\n<b>>>></b>";


            // Find the first non-killed competitor
            currentlyPlaying = FindNextAliveCompetitor(0);

            // If no alive competitors found, start a new turn
            if (currentlyPlaying == null)
            {
                EndGame(true);
                return;
            }

            FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(currentlyPlaying);
            currentlyPlaying.AutoPlay();
        }
        else
        {
            int currentIndex = competitors.IndexOf(currentlyPlaying);

            // Find the next non-killed competitor
            currentlyPlaying = FindNextAliveCompetitor(currentIndex + 1);

            // If no more alive competitors found, start a new turn
            if (currentlyPlaying == null)
            {
                if(player.killed)
                {
                    EndGame(true);
                    return;
                } else
                {
                    NewTurn();
                    return;
                }   
            }

            FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(currentlyPlaying);
            currentlyPlaying.AutoPlay();
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
    public void EndGame(bool win = false)
    {
        if (win) MessageDisplay.ShowMessage("Victory!", Color.green); else MessageDisplay.ShowMessage("Defeat!", Color.red);
        
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(0);
        Application.Quit();
        // Add Unity-specific code to end the game here
        // For example:
        Debug.Log("Game Ended!");

        // You might want to show a game over screen, pause the game, etc.
        // Example: UnityEngine.SceneManagement.SceneManager.LoadScene("GameOverScene");

        // Or if using a GameManager:
        // GameManager.Instance.GameOver();
    }
}
