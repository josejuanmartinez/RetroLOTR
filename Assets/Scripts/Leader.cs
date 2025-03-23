using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

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
        biome = GetComponent<BiomeConfig>();
    }

    public int GetGoldPerTurn()
    {
        int gold = 0;
        foreach (PC pc in controlledPcs) gold += (int)pc.citySize;
        foreach (Character character in controlledCharacters)
        {
            if (!character.startingCharacter) gold -= 1;
            if (character.GetArmy() != null) gold -= character.GetArmy().GetMaintenanceCost();
        }
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

        if(this is not PlayableLeader) return;
        FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != this).ToList().ForEach(x =>
        {
            x.CheckStoresConditions(this);
        });

        base.NewTurn();
    }

    // The async version of RevealVisibleHexes
    public IEnumerator RevealVisibleHexesAsync(System.Action onComplete = null)
    {
        if (FindFirstObjectByType<Game>().player != this) yield break; // This will exit without calling onComplete

        List<Hex> allHexes = FindFirstObjectByType<Board>().hexes.Values.ToList();

        allHexes.FindAll(x => !visibleHexes.Contains(x)).ForEach(x => x.Hide());
        var hexesToReveal = visibleHexes.ToList();
        List<Hex> spiedHexes = allHexes.Where(hex => hex.characters.Any(character => character.doubledBy.Contains(this))).ToList();
        hexesToReveal.AddRange(spiedHexes);
        hexesToReveal = hexesToReveal.Distinct().ToList();

        int batchSize = 15;
        for (int i = 0; i < hexesToReveal.Count; i += batchSize)
        {
            int endIndex = Mathf.Min(i + batchSize, hexesToReveal.Count);
            for (int j = i; j < endIndex; j++) hexesToReveal[j].RevealArea();
            yield return null;
        }

        onComplete?.Invoke();
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

    public void AddLeather(int amount) 
    {
        leatherAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} leather", Color.green);
    }
    public void AddTimber(int amount)
    {
        timberAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} timber", Color.green);
    }
    public void AddMounts(int amount)
    {
        mountsAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} mounts", Color.green);
    }
    public void AddIron(int amount)
    {
        ironAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} iron", Color.green);
    }
    public void AddMithril(int amount)
    {
        mithrilAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} mithril", Color.green);
    }
    public void AddGold(int amount)
    {
        goldAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} gold", Color.green);
    }
    public void RemoveLeather(int leatherCost)
    {
        leatherAmount -= leatherCost;
        if (leatherCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{leatherCost} leather", Color.red);
    }
    public void RemoveTimber(int timberCost)
    {
        timberAmount -= timberCost;
        if (timberCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{timberCost} timber", Color.red);
    }
    public void RemoveMounts(int mountsCost)
    {
        mountsAmount -= mountsCost;
        if (mountsCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{mountsCost} mounts", Color.red);
    }
    public void RemoveIron(int ironCost)
    {
        ironAmount -= ironCost;
        if (ironCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{ironCost} iron", Color.red);
    }
    public void RemoveMithril(int mithrilCost)
    {
        mithrilAmount -= mithrilCost;
        if (mithrilCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{mithrilCost} mithril", Color.red);
    }
    public void RemoveGold(int goldCost)
    {
        goldAmount -= goldCost;
        if (goldCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{goldCost} gold", Color.red);
    }
    
}
