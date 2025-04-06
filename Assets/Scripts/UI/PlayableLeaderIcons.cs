using UnityEngine;

public class PlayableLeaderIcons : MonoBehaviour
{
    public GameObject playerLeaderIconPrefab;

    public void Instantiate(Leader leader)
    {
        GameObject icon = Instantiate(playerLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        icon.GetComponent<PlayableLeaderIcon>().Initialize(leader);
    }

    public void HighlightCurrentlyPlaying(Leader currentlyPlaying)
    {
        for(int i=0;i<transform.childCount;i++)
        {
            if(transform.GetChild(i).gameObject.name.ToLower() == currentlyPlaying.characterName.ToLower())
            {
                transform.GetChild(i).localScale = new Vector3(1.5f, 1.5f, 1f);
            } else
            {
                transform.GetChild(i).localScale = new Vector3(1f, 1f, 1f);
            }
        }
    }
}
