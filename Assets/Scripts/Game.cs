using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public PlayableLeader player;
    public List<PlayableLeader> competitors;
    public List<NonPlayableLeader> npcs;
    public PlayableLeader currentlyPlaying;
    
    public int normalMovement = 12;
    public int cavalryMovement = 15;

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
        Hex startingHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.HasCharacter(player));
        startingHex.LookAt();
    }

    public void NewTurn()
    {
        turn++;
        currentlyPlaying = player;
        FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(currentlyPlaying);
        currentlyPlaying.NewTurn();
        competitors.ForEach(x => x.NewTurn());
        // Start the coroutine to refresh hexes in the background
        StartCoroutine(RefreshHexesAsync(player));
        StartCoroutine(currentlyPlaying.RevealVisibleHexesAsync());
    }
    public void NextPlayer()
    {
        if (currentlyPlaying == player)
        {
            currentlyPlaying = competitors[0];
        } else if (competitors.IndexOf(currentlyPlaying) == competitors.Count - 1)
        {
            NewTurn();
        } else
        {
            currentlyPlaying = competitors[competitors.IndexOf(currentlyPlaying) + 1];
        }
        FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(currentlyPlaying);
    }

    private IEnumerator RefreshHexesAsync(Leader currentlyPlaying)
    {
        var hexes = FindFirstObjectByType<Board>().hexes.Values.ToList();

        // Process hexes in smaller batches to prevent frame drops
        int batchSize = 20; // Adjust based on your needs
        for (int i = 0; i < hexes.Count; i += batchSize)
        {
            int endIndex = Mathf.Min(i + batchSize, hexes.Count);
            for (int j = i; j < endIndex; j++)
            {
                hexes[j].RefreshForChangingPLayer(currentlyPlaying);
            }

            // Wait until next frame before processing the next batch
            yield return null;
        }
    }

}
