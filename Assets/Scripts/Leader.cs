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

    void Awake()
    {
        characterName = gameObject.name;
        biome = GetComponent<BiomeConfig>();
    }

    public int GetGoldPerTurn()
    {
        int gold = 0;
        foreach (PC pc in controlledPcs) gold += (int)pc.citySize;
        foreach (Character character in controlledCharacters) gold -= character.IsArmyCommander()? 2: 1;
        return gold;
    }

    public int GetLeatherPerTurn()
    {
        return controlledPcs.Select(x => x.leather).Sum();
    }

    public int GetMountsPerTurn()
    {
        return controlledPcs.Select(x => x.mounts).Sum();
    }

    public int GetTimberPerTurn()
    {
        return controlledPcs.Select(x => x.timber).Sum();
    }

    public int GetIronPerTurn()
    {
        return controlledPcs.Select(x => x.iron).Sum();
    }
    public int GetMithrilPerTurn()
    {
        return controlledPcs.Select(x => x.mithril).Sum();
    }

    new public AlignmentEnum GetAlignment()
    {
        return biome.alignment;
    }
    new public void NewTurn()
    {
        if (killed) return;
        
        leatherAmount += GetLeatherPerTurn();
        mountsAmount += GetMountsPerTurn();
        timberAmount += GetTimberPerTurn();
        ironAmount += GetIronPerTurn();
        mithrilAmount += GetMithrilPerTurn();
        goldAmount += GetGoldPerTurn();

        controlledCharacters.ForEach(x => x.moved = 0);
        controlledCharacters.ForEach(x => x.hasActionedThisTurn = false);

        FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).ToList().ForEach(x =>
        {
            x.CheckStoresConditions(this);
        });

        base.NewTurn();
    }

    // The async version of RevealVisibleHexes
    public IEnumerator RevealVisibleHexesAsync(System.Action onComplete = null)
    {
        Debug.Log("Starting RevealVisibleHexesAsync");

        if (FindFirstObjectByType<Game>().player != this)
        {
            Debug.Log("Early exit: not the current player");
            yield break; // This will exit without calling onComplete
        }

        List<Hex> allHexes = FindFirstObjectByType<Board>().hexes.Values.ToList();

        allHexes.FindAll(x => !visibleHexes.Contains(x)).ForEach(x => x.Hide());
        var hexesToReveal = visibleHexes.ToList();
        List<Hex> spiedHexes = allHexes.Where(hex => hex.characters.Any(character => character.doubledBy.Contains(this))).ToList();
        hexesToReveal.AddRange(spiedHexes);
        hexesToReveal = hexesToReveal.Distinct().ToList();

        Debug.Log($"Hexes to reveal: {hexesToReveal.Count}");

        int batchSize = 15;
        for (int i = 0; i < hexesToReveal.Count; i += batchSize)
        {
            Debug.Log($"Processing batch {i / batchSize + 1} of {Mathf.Ceil(hexesToReveal.Count / (float)batchSize)}");
            int endIndex = Mathf.Min(i + batchSize, hexesToReveal.Count);
            for (int j = i; j < endIndex; j++) hexesToReveal[j].RevealArea();
            Debug.Log("About to yield for next frame");
            yield return null;
            Debug.Log("Resumed after frame yield");
        }

        Debug.Log("Coroutine completed, calling onComplete callback");
        onComplete?.Invoke();
        Debug.Log("onComplete callback invoked");
    }

    override public Leader GetOwner()
    {
        return owner != null ? owner : this;
    }

    public void AutoPlay()
    {
        if (killed) return;
        Debug.Log("Skipping: " + characterName);
        FindFirstObjectByType<Game>().NextPlayer();
    }

    public bool LeaderSeesHex(Hex hex)
    {
        if (hex.GetPC() != null && hex.GetPC().owner == GetOwner()) return true;
        if (hex.characters.Find(x => x.GetOwner() == GetOwner())) return true;
        return false;
    }
}
