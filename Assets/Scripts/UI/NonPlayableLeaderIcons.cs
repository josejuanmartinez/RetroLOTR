using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NonPlayableLeaderIcons : MonoBehaviour
{
    public GameObject nonPlayableLeaderIconPrefab;
    public List<NonPlayableLeaderIcon> nonPlayableLeaderIcons;

    public PlayableLeader playableLeader { get; set;}

    public void SetPlayableLeader(PlayableLeader playableLeader)
    {
        this.playableLeader = playableLeader;
    }
    public void Instantiate(NonPlayableLeader leader, PlayableLeader playableLeader)
    {
        if (leader == null || playableLeader == null || leader.GetAlignment() != playableLeader.GetAlignment())
        {
            Debug.LogWarning(
                $"Cannot place NPL '{leader?.characterName ?? "null"}' under " +
                $"'{playableLeader?.characterName ?? "null"}': their alignments do not match.");
            return;
        }

        nonPlayableLeaderIcons ??= new List<NonPlayableLeaderIcon>();
        this.playableLeader = playableLeader;
        GameObject icon = Instantiate(nonPlayableLeaderIconPrefab, transform);
        icon.name = leader.characterName;
        NonPlayableLeaderIcon npli = icon.GetComponent<NonPlayableLeaderIcon>();
        npli.Initialize(leader);
        nonPlayableLeaderIcons.Add(npli);
        ResortChildrenByAlignment();
    }

    public void RevealToPlayerIfNot(NonPlayableLeader leader)
    {
        if (leader == null || playableLeader == null || leader.GetAlignment() != playableLeader.GetAlignment()) return;

        nonPlayableLeaderIcons ??= new List<NonPlayableLeaderIcon>();
        NonPlayableLeaderIcon npli = nonPlayableLeaderIcons.Find(x => x != null && x.nonPlayableLeader == leader);
        if (npli == null)
        {
            // Undiscovered leaders have no GameObject and therefore reserve no layout slot.
            Instantiate(leader, playableLeader);
            npli = nonPlayableLeaderIcons.Find(x => x != null && x.nonPlayableLeader == leader);
        }
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
