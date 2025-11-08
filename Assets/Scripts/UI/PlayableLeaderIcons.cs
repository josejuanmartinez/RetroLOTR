using UnityEngine;

public class PlayableLeaderIcons : MonoBehaviour
{
    public GameObject playerLeaderIconPrefab;

    public void Instantiate(PlayableLeader leader)
    {
        GameObject icon = Instantiate(playerLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        icon.GetComponent<PlayableLeaderIcon>().Initialize(leader);
    }

    public void HighlightCurrentlyPlaying(PlayableLeader currentlyPlaying)
    {
        for(int i=0;i<transform.childCount;i++)
        {
            PlayableLeaderIcon playableLeaderIcon = transform.GetChild(i).GetComponent<PlayableLeaderIcon>();
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

    public void AddDeadIcon(Leader currentlyPlaying)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).gameObject.name.ToLower() == currentlyPlaying.characterName.ToLower())
            {
                transform.GetChild(i).localScale = new Vector3(1f, 1f, 1f);
                transform.GetChild(i).GetComponent<PlayableLeaderIcon>().SetDead();
            }
            else
            {
                transform.GetChild(i).localScale = new Vector3(1f, 1f, 1f);
            }
        }
    }
}
