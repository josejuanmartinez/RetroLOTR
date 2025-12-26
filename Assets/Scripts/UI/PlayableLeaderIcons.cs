using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayableLeaderIcons : MonoBehaviour
{
    // public GameObject playerLeaderIconPrefab;
    public PlayableLeaderIcon currentPlayerPlayableIcon;
    public PlayableLeaderIcon competitor1PlayableIcon;
    public PlayableLeaderIcon competitor2PlayableIcon;

    public List<PlayableLeaderIcon> playableLeaderIcons;
    public Game game;
    void Start()
    {
        if(game == null) game = FindFirstObjectByType<Game>();
        playableLeaderIcons = new() {
            currentPlayerPlayableIcon,
            competitor1PlayableIcon,
            competitor2PlayableIcon
        };
        if (game != null && game.currentlyPlaying != null)
        {
            HighlightCurrentlyPlaying(game.currentlyPlaying);
        }
    }

    public void Instantiate(PlayableLeader leader)
    {
        if(game.player == leader)
        {            
            currentPlayerPlayableIcon.Initialize(leader);    
        } 
        else
        {
            if(!competitor1PlayableIcon.IsInitialized())
            {
                competitor1PlayableIcon.Initialize(leader);    
            } else
            {
                competitor2PlayableIcon.Initialize(leader);    
            }
        }

        if (game != null && game.currentlyPlaying != null)
        {
            HighlightCurrentlyPlaying(game.currentlyPlaying);
        }
    }

    public void HighlightCurrentlyPlaying(PlayableLeader currentlyPlaying)
    {
        for(int i=0;i<playableLeaderIcons.Count;i++)
        {
            PlayableLeaderIcon playableLeaderIcon = playableLeaderIcons[i];
            if (playableLeaderIcon == null) continue;
            PlayableLeader playableLeader = playableLeaderIcon.playableLeader;
            if (playableLeader == currentlyPlaying)
            {
                playableLeaderIcon.SetCurrentlyPlayingEffect();
            } else
            {
                playableLeaderIcon.RemoveCurrentlyPlayingEffect();
            }
        }
    }

    public void AddDeadIcon(PlayableLeader deadPlayer)
    {
        for (int i = 0; i < playableLeaderIcons.Count; i++)
        {
            PlayableLeaderIcon playableLeaderIcon = playableLeaderIcons[i];
            if (playableLeaderIcon != null && playableLeaderIcon.playableLeader != null && playableLeaderIcon.playableLeader == deadPlayer)
            {
                playableLeaderIcon.SetDead();
                return;
            }
        }
    }

    public void RefreshVictoryPointsFor(PlayableLeader leader, int points)
    {
        if (leader == null) return;
        for (int i = 0; i < playableLeaderIcons.Count; i++)
        {
            PlayableLeaderIcon playableLeaderIcon = playableLeaderIcons[i];
            if (playableLeaderIcon != null && playableLeaderIcon.playableLeader != null && !playableLeaderIcon.playableLeader.killed && playableLeaderIcon.playableLeader == leader)
            {
                playableLeaderIcon.RefreshVictoryPoints(points);
                return;
            }
        }
    }

    public void RefreshVictoryPointsForAll()
    {
        for (int i = 0; i < playableLeaderIcons.Count; i++)
        {
            PlayableLeaderIcon icon = transform.GetChild(i).GetComponent<PlayableLeaderIcon>();
            if (icon != null && icon.playableLeader != null && !icon.playableLeader.killed && icon.playableLeader.victoryPoints != null)
            {
                icon.RefreshVictoryPoints(icon.playableLeader.victoryPoints.RelativeScore);
            }
        }
    }
}
