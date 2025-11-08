using UnityEngine;

public class NonPlayableLeaderIcons : MonoBehaviour
{
    public GameObject nonPlayableLeaderIconPrefab;

    public void Instantiate(NonPlayableLeader leader)
    {
        GameObject icon = Instantiate(nonPlayableLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        icon.GetComponent<NonPlayableLeaderIcon>().Initialize(leader);
    }
}
