using UnityEngine;
using System.Collections.Generic;

public class NonPlayableLeaderIcons : MonoBehaviour
{
    public GameObject nonPlayableLeaderIconPrefab;
    public List<NonPlayableLeaderIcon> nonPlayableLeaderIcons;
    public void Instantiate(NonPlayableLeader leader)
    {
        GameObject icon = Instantiate(nonPlayableLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        NonPlayableLeaderIcon npli = icon.GetComponent<NonPlayableLeaderIcon>();
        npli.Initialize(leader);
        nonPlayableLeaderIcons.Add(npli);
    }

    public void RevealToPlayerIfNot(NonPlayableLeader leader)
    {
        NonPlayableLeaderIcon npli = nonPlayableLeaderIcons.Find(x => x.nonPlayableLeader == leader);
        if (npli != null) npli.RevealToPlayer();
    }
}
