using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Leader : Character
{
    [Header("Nation data")]
    public List<Character> controlledCharacters = new();
    public List<PC> controlledPcs = new();
    public List<Hex> visibleHexes = new();

    [Header("Stores")]
    public int leatherAmount = 0;
    public int mountsAmount = 0;
    public int timberAmount = 0;
    public int ironAmount = 0;
    public int mithrilAmount = 0;
    public int goldAmount = 0;

    private Game game;
    private LeaderBiomeConfig leaderBiome;

    public void Initialize(Hex hex, LeaderBiomeConfig leaderBiome)
    {
        game = FindFirstObjectByType<Game>();
        this.leaderBiome = leaderBiome;
		InitializeFromBiome(this, hex, leaderBiome);
        if(leaderBiome is not NonPlayableLeaderBiomeConfig) FindFirstObjectByType<PlayableLeaderIcons>().Instantiate(this);
    }

    public LeaderBiomeConfig GetBiome()
    {
        return leaderBiome;
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
        return leaderBiome.alignment;
    }
    new public void NewTurn()
    {
        if (!killed && goldAmount < -10) Killed(this);

        if (killed) return;

        leatherAmount += GetLeatherPerTurn();
        mountsAmount += GetMountsPerTurn();
        timberAmount += GetTimberPerTurn();
        ironAmount += GetIronPerTurn();
        mithrilAmount += GetMithrilPerTurn();
        goldAmount += GetGoldPerTurn();


        // Any NPC joins due to my new good stores?
        if (this is PlayableLeader)
        {
            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != this).ToList().ForEach(x =>
            {
                x.CheckStoresConditions(this);
            });
        }
        
        controlledCharacters.FindAll(x => !x.killed).ForEach(x => x.NewTurn());
        StartCoroutine(WaitUntilEndOfTurn());
    }

    private IEnumerator WaitUntilEndOfTurn()
    {
        yield return new WaitForEndOfFrame();

        // AI: Act if not player
        if (game.player != this || game.autoplay)
        {
            yield return new WaitUntil(() => controlledCharacters.All(c => c.killed || c.hasActionedThisTurn || c.hasMovedThisTurn));
            FindFirstObjectByType<Game>().NextPlayer();
        }
        else
        {
            // Refresh UI
            FindFirstObjectByType<StoresManager>().RefreshStores();
            // Refresh hexes
            StartCoroutine(RevealVisibleHexesAsync(() =>
            {
                // Prompt for action to the player
                FindFirstObjectByType<Board>().SelectCharacter(this);
            }
            ));
        }
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

    public int GetCharacterPoints()
    {
        if (killed) return 0;
        return controlledCharacters.FindAll(x => !x.killed).Select(x => x.GetCommander() + x.GetAgent() + x.GetEmmissary() + x.GetMage() + x.artifacts.Count * 10 + x.health).Sum();
    }

    public int GetPCPoints()
    {
        int points = controlledPcs.Select(x => x.GetDefense()).Sum();
        points -= controlledPcs.FindAll(x => x.hiddenButRevealed).Count() * 10;
        points += controlledPcs.Select(x => x.GetProductionPoints()).Sum();
        return points;
    }

    public int GetArmyPoints()
    {
        int offence = controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().GetOffence()).Sum();
        int defence = controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().GetDefence()).Sum();
        return offence + defence;
    }

    public int GetStorePoints()
    {
        return leatherAmount + timberAmount * 2 + mithrilAmount * 5 + ironAmount * 3 + mountsAmount * 2;
    }

    public int GetResourceProductionPoints()
    {
        return GetLeatherPerTurn() + GetTimberPerTurn() * 2 + GetMithrilPerTurn() * 5 + GetIronPerTurn() * 3 + GetMountsPerTurn() * 2;
    }

    public int GetAllPoints()
    {
        return GetCharacterPoints() + GetPCPoints() + GetArmyPoints() + GetStorePoints();
    }

    public override void Killed(Leader killedBy, bool onlyMask = false)
    {
        if(killedBy == this)
        {
            MessageDisplay.ShowMessage($"{name}'s realm collapsed!", Color.red);
        } else
        {
            MessageDisplay.ShowMessage($"{name} was killed by {killedBy.characterName}", Color.red);
        }

        controlledCharacters.ForEach(x =>
        {
            x.hex.characters.Remove(x);
            x.hex.armies.Remove(x.GetArmy());
            x.hex.RedrawCharacters();
            x.hex.RedrawArmies();
            if (x != this) x.Killed(killedBy, true);
        });

        List<Character> markedAsKilled = controlledCharacters.FindAll(x => x.killed);
        foreach (Character marked in markedAsKilled)
        {
            if (controlledCharacters.Contains(marked) && marked != this) marked.Killed(killedBy);
        }

        // Not autokilled (like bankrupt)
        if (killedBy != this)
        {
            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = killedBy;
                killedBy.controlledPcs.Add(pc);
                killedBy.visibleHexes.Add(pc.hex);
            }
        }
        else
        {
            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = null;
                pc.hex.SetPC(null);
                pc.hex.RedrawPC();
            }
        }

        GetOwner().controlledCharacters.Clear();
        GetOwner().controlledPcs.Clear();
        visibleHexes.Clear();

        killedBy.leatherAmount += leatherAmount;
        killedBy.mountsAmount += mountsAmount;
        killedBy.timberAmount += timberAmount;
        killedBy.ironAmount += ironAmount;
        killedBy.mithrilAmount += mithrilAmount;
        killedBy.goldAmount += goldAmount;

        leatherAmount = 0;
        mountsAmount = 0;
        timberAmount = 0;
        ironAmount = 0;
        mithrilAmount = 0;
        goldAmount = 0;

        base.Killed(killedBy);
    }

}
