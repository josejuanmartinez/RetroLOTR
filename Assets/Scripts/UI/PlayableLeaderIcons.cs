using UnityEngine;

public class PlayableLeaderIcons : MonoBehaviour
{
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
