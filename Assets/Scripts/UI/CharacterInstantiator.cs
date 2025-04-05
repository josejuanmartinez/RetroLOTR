using UnityEngine;

public class CharacterInstantiator : MonoBehaviour
{
    public Character InstantiateCharacter()
    {
        GameObject newCharacterPrefab = new ();
        return newCharacterPrefab.AddComponent<Character>();
    }
    public Leader InstantiateLeader()
    {
        GameObject newCharacterPrefab = new ();
        return newCharacterPrefab.AddComponent<Leader>();
    }
    public NonPlayableLeader InstantiateNonPlayableLeader()
    {
        GameObject newCharacterPrefab = new ();
        return newCharacterPrefab.AddComponent<NonPlayableLeader>();
    }

    public void ResetForNewEpisode()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}
