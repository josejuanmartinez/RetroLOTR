using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BiomeConfig))]
public class Leader : Character
{
    public BiomeConfig biome;

    public List<Character> controlledCharacters;
    public List<PC> controlledPcs;
    public List<Hex> visibleHexes;

    [Header("Stores")]
    public int leatherAmount = 0;
    public int mountsAmount = 0;
    public int timberAmount = 0;
    public int ironAmount = 0;
    public int mithrilAmount = 0;
    public int goldAmount = 0;

    public bool killed = false;

    void Awake()
    {
        characterName = gameObject.name;
        biome = GetComponent<BiomeConfig>();
        Initialize(null, biome.alignment);
    }

    new public AlignmentEnum GetAlignment()
    {
        return biome.alignment;
    }

    new public void NewTurn()
    {
        if (killed) return;
        foreach (PC pc in controlledPcs)
        {
            leatherAmount += pc.leather;
            mountsAmount += pc.mounts;
            timberAmount += pc.timber;
            ironAmount += pc.iron;
            mithrilAmount += pc.mithril;
            goldAmount += ((int)pc.citySize) + 1;
        }

        controlledCharacters.ForEach(x => x.moved = 0);
        controlledCharacters.ForEach(x => x.hasActionedThisTurn = false);

        base.NewTurn();
    }

    // The async version of RevealVisibleHexes
    public IEnumerator RevealVisibleHexesAsync()
    {
        if (FindFirstObjectByType<Game>().player != this) yield break;

        FindFirstObjectByType<Board>().hexes.Values.ToList().FindAll(x => !visibleHexes.Contains(x)).ForEach(x => x.Hide());

        var hexesToReveal = visibleHexes.ToList(); // Create a copy to avoid potential modification issues

        // Process revealing in smaller batches to prevent frame drops
        int batchSize = 15; // Adjust based on your needs
        for (int i = 0; i < hexesToReveal.Count; i += batchSize)
        {
            int endIndex = Mathf.Min(i + batchSize, hexesToReveal.Count);
            for (int j = i; j < endIndex; j++)
            {
                hexesToReveal[j].RevealArea();
            }

            // Wait until next frame before processing the next batch
            yield return null;
        }
    }
    override public Leader GetOwner()
    {
        if (owner)
        {
            return owner;
        }
        else
        {
            return this;
        }
    }

    public void AutoPlay()
    {
        if (killed) return;
        Debug.Log("Skipping: " + characterName);
        FindFirstObjectByType<Game>().NextPlayer();
    }

    public bool LeaderSeesHex(Hex hex)
    {
        if (hex.pc != null && hex.pc.owner == GetOwner()) return true;
        if (hex.characters.Find(x => x.GetOwner() == GetOwner())) return true;
        return false;
    }
}
