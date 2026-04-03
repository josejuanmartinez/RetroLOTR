using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NonPlayableLeaderIcons : MonoBehaviour
{
    public GameObject nonPlayableLeaderIconPrefab;
    public List<NonPlayableLeaderIcon> nonPlayableLeaderIcons;
    public PlayableLeader playableLeader;
    public void Instantiate(NonPlayableLeader leader, PlayableLeader playableLeader)
    {
        GameObject icon = Instantiate(nonPlayableLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        NonPlayableLeaderIcon npli = icon.GetComponent<NonPlayableLeaderIcon>();
        npli.Initialize(leader);
        nonPlayableLeaderIcons.Add(npli);
        this.playableLeader = playableLeader;
        ResortChildrenByAlignment();
    }

    public void RevealToPlayerIfNot(NonPlayableLeader leader)
    {
        NonPlayableLeaderIcon npli = nonPlayableLeaderIcons.Find(x => x.nonPlayableLeader == leader);
        if (npli != null) npli.RevealToPlayer();
    }

    private void ResortChildrenByAlignment()
    {
        nonPlayableLeaderIcons.RemoveAll(x => x == null);

        NonPlayableLeaderIcon[] orderedIcons = nonPlayableLeaderIcons
            .OrderBy(x => (int)x.GetAlignmentValue())
            .ThenBy(x => x.nonPlayableLeader != null ? x.nonPlayableLeader.characterName : x.name)
            .ToArray();

        for (int i = 0; i < orderedIcons.Length; i++)
        {
            orderedIcons[i].transform.SetSiblingIndex(i);
        }
    }
}
