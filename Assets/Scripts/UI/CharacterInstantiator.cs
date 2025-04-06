using UnityEngine;

public class CharacterInstantiator : MonoBehaviour
{
    public Transform leadersParent;
    public Transform nonPlayableLeadersParent;
    public Transform otherCharactersParent;
    public Character InstantiateCharacter(Leader leader, Hex hex, BiomeConfig biomeConfig)
    {
        Character character = InstantiatePrefab(biomeConfig.characterName, otherCharactersParent).AddComponent<Character>();
        character.InitializeFromBiome(leader, hex, biomeConfig);
        return character;
    }
    public PlayableLeader InstantiatePlayableLeader(Hex hex, LeaderBiomeConfig leaderBiomeConfig)
    {
        PlayableLeader playableLeader = InstantiatePrefab(leaderBiomeConfig.characterName, leadersParent).AddComponent<PlayableLeader>();
        playableLeader.Initialize(hex, leaderBiomeConfig);
        return playableLeader;
    }
    public NonPlayableLeader InstantiateNonPlayableLeader(Hex hex, NonPlayableLeaderBiomeConfig nonPlayableLeaderBiomeConfig)
    {
        NonPlayableLeader nonPlayableLeader = InstantiatePrefab(nonPlayableLeaderBiomeConfig.characterName, nonPlayableLeadersParent).AddComponent<NonPlayableLeader>();
        nonPlayableLeader.Initialize(hex, nonPlayableLeaderBiomeConfig);
        return nonPlayableLeader;
    }

    private GameObject InstantiatePrefab(string name, Transform t)
    {
        GameObject newCharacterPrefab = new(name);
        newCharacterPrefab.transform.parent = t;
        return newCharacterPrefab;
    }

    public void ResetForNewEpisode()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}
