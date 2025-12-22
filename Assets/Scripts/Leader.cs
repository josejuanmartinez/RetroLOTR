using System;
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
    public int steelAmount = 0;
    public int mithrilAmount = 0;
    public int goldAmount = 0;

    [Header("Creation Slots")]
    [SerializeField] private int characterSlotsAvailable = 0;
    [SerializeField] private int pcSlotsAvailable = 0;
    [SerializeField] private int createdCharacters = 0;
    [SerializeField] private int createdPcs = 0;
    [SerializeField] private int nextCharacterSlotTurn = 1;
    [SerializeField] private int nextPcSlotTurn = 1;

    private readonly HashSet<string> completedActions = new(StringComparer.OrdinalIgnoreCase);

    private Game game;
    private LeaderBiomeConfig leaderBiome;

    public void Initialize(Hex hex, LeaderBiomeConfig leaderBiome, bool showSpawnMessage = true)
    {
        game = FindFirstObjectByType<Game>();
        this.leaderBiome = leaderBiome;
		InitializeFromBiome(this, hex, leaderBiome, showSpawnMessage);
    }

    public LeaderBiomeConfig GetBiome()
    {
        return leaderBiome;
    }

    private void EvaluateCreationSlots()
    {
        if (game == null) game = FindFirstObjectByType<Game>();
        int currentTurn = game != null ? game.turn : 0;

        bool characterSlotAdded = false;
        bool pcSlotAdded = false;

        if (createdCharacters < 5 && currentTurn >= nextCharacterSlotTurn)
        {
            characterSlotsAvailable = Math.Min(characterSlotsAvailable + 1, 5 - createdCharacters);
            nextCharacterSlotTurn += 10;
            characterSlotAdded = true;
        }

        if (createdPcs < 5 && currentTurn >= nextPcSlotTurn)
        {
            pcSlotsAvailable = Math.Min(pcSlotsAvailable + 1, 5 - createdPcs);
            nextPcSlotTurn += 10;
            pcSlotAdded = true;
        }

        if (game != null && game.IsPlayerCurrentlyPlaying() && (characterSlotAdded || pcSlotAdded))
        {
            string message = characterSlotAdded && pcSlotAdded
                ? "A new character slot and a new pc slot are available."
                : characterSlotAdded
                    ? "A new character slot is available."
                    : "A new pc slot is available.";
            _ = ConfirmationDialog.AskOk(message);
        }
    }

    public bool HasCharacterSlot() => characterSlotsAvailable > 0 && createdCharacters < 5;
    public bool HasPcSlot() => pcSlotsAvailable > 0 && createdPcs < 5;

    public bool TryConsumeCharacterSlot()
    {
        if (!HasCharacterSlot()) return false;
        characterSlotsAvailable--;
        createdCharacters++;
        return true;
    }

    public bool TryConsumePcSlot()
    {
        if (!HasPcSlot()) return false;
        pcSlotsAvailable--;
        createdPcs++;
        return true;
    }

    public int GetCreatedCharactersCount() => createdCharacters;
    public int GetCreatedPcsCount() => createdPcs;

    public string GetNextNewCharacterName()
    {
        if (leaderBiome?.newCharacters == null) return null;
        if (createdCharacters >= leaderBiome.newCharacters.Count) return null;
        return leaderBiome.newCharacters[createdCharacters];
    }

    public string GetNextNewPcName()
    {
        if (leaderBiome?.newPCs == null) return null;
        if (createdPcs >= leaderBiome.newPCs.Count) return null;
        return leaderBiome.newPCs[createdPcs];
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

    public int GetSteelPerTurn()
    {
        return controlledPcs.Select(x => x.steel).Sum();
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

        EvaluateCreationSlots();

        leatherAmount += GetLeatherPerTurn();
        mountsAmount += GetMountsPerTurn();
        timberAmount += GetTimberPerTurn();
        ironAmount += GetIronPerTurn();
        steelAmount += GetSteelPerTurn();
        mithrilAmount += GetMithrilPerTurn();
        goldAmount += GetGoldPerTurn();
        
        // Make all characters in nation act!
        controlledCharacters.FindAll(c => !c.killed).ForEach(x => x.NewTurn());
        StartCoroutine(WaitUntilEndOfTurn());
    }

    private IEnumerator WaitUntilEndOfTurn()
    {
        yield return new WaitForEndOfFrame();

        // AI: Act if not player
        if (game.player != this)
        {
            yield return AITurnController.ExecuteLeaderTurn(this as PlayableLeader);
            game.NextPlayer();
            yield break;
        }
        else
        {
            if(this.killed) {
                game.EndGame(false);
                yield break;
            }
            // Refresh UI
            FindFirstObjectByType<StoresManager>().RefreshStores();
            // Refresh hexes
            StartCoroutine(RevealVisibleHexesAsync(() =>
            {
                // Prompt for action to the player
                FindFirstObjectByType<Board>().SelectCharacter(this, true, 1.0f, 0.0f);
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
            for (int j = i; j < endIndex; j++) hexesToReveal[j].RevealArea(1, false);
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
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"leather\">", Color.green);
    }
    public void AddTimber(int amount)
    {
        timberAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"timber\">", Color.green);
    }
    public void AddMounts(int amount)
    {
        mountsAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"mounts\">", Color.green);
    }
    public void AddIron(int amount)
    {
        ironAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"iron\">", Color.green);
    }

    public void AddSteel(int amount)
    {
        steelAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"steel\">", Color.green);
    }

    public void AddMithril(int amount)
    {
        mithrilAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"mithril\">", Color.green);
    }
    public void AddGold(int amount)
    {
        goldAmount += amount;
        if (amount > 0) MessageDisplay.ShowMessage($"+{amount} <sprite name=\"gold\">", Color.green);
    }
    public void RemoveLeather(int leatherCost)
    {
        leatherAmount -= leatherCost;
        if (leatherCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{leatherCost} <sprite name=\"leather\">", Color.red);
    }
    public void RemoveTimber(int timberCost)
    {
        timberAmount -= timberCost;
        if (timberCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{timberCost} <sprite name=\"timber\">", Color.red);
    }
    public void RemoveMounts(int mountsCost)
    {
        mountsAmount -= mountsCost;
        if (mountsCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{mountsCost} <sprite name=\"mounts\">", Color.red);
    }
    public void RemoveIron(int ironCost)
    {
        ironAmount -= ironCost;
        if (ironCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{ironCost} <sprite name=\"iron\">", Color.red);
    }

    public void RemoveSteel(int steelCost)
    {
        steelAmount -= steelCost;
        if (steelCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{steelCost} <sprite name=\"steel\">", Color.red);
    }

    public void RemoveMithril(int mithrilCost)
    {
        mithrilAmount -= mithrilCost;
        if (mithrilCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{mithrilCost} <sprite name=\"mithril\">", Color.red);
    }
    public void RemoveGold(int goldCost)
    {
        goldAmount -= goldCost;
        if (goldCost > 0) MessageDisplay.ShowMessage($"{characterName}: -{goldCost} <sprite name=\"gold\">", Color.red);
    }

    private static string NormalizeActionName(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return string.Empty;
        string baseName = ActionNameUtils.StripShortcut(actionName);
        string normalized = new string(baseName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized;
    }

    public void RecordActionHistory(string actionName)
    {
        string normalized = NormalizeActionName(actionName);
        if (string.IsNullOrEmpty(normalized)) return;
        completedActions.Add(normalized);
    }

    public bool HasPerformedAction(string actionName)
    {
        string normalized = NormalizeActionName(actionName);
        if (string.IsNullOrEmpty(normalized)) return false;
        return completedActions.Contains(normalized);
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
        return leatherAmount + timberAmount * 2 + mithrilAmount * 5 + ironAmount * 3 + steelAmount * 4 + mountsAmount * 2;
    }

    public int GetResourceProductionPoints()
    {
        return GetLeatherPerTurn() + GetTimberPerTurn() * 2 + GetMithrilPerTurn() * 5 + GetIronPerTurn() * 3 + GetSteelPerTurn() * 4 + GetMountsPerTurn() * 2;
    }

    public int GetAllPoints()
    {
        return GetCharacterPoints() + GetPCPoints() + GetArmyPoints() + GetStorePoints();
    }

    public override void Killed(Leader killedBy, bool onlyMask = false)
    {
        bool realmCollapsed = killedBy == this;
        if(realmCollapsed)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{name}'s realm collapsed!", Color.red);
        } else
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{name} was killed by {killedBy.characterName}", Color.red);
        }

        // We just mark them (, true) as if we kill them, they will be removed from controlledCharacters array and change the size dynamically
        // Throwing an error
        controlledCharacters.ForEach(x =>
        {
            x.hex.characters.Remove(x);
            x.hex.armies.Remove(x.GetArmy());
            // Redraw in x.Killed
            if (x != this) x.Killed(killedBy, true);
        });

        List<Character> markedAsKilled = controlledCharacters.FindAll(x => x.killed);
        foreach (Character marked in markedAsKilled)
        {
            if (controlledCharacters.Contains(marked) && marked != this) marked.Killed(killedBy);
        }

        if (realmCollapsed)
        {
            // Autokilled (like bankrupt)
            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = null;
                hex.SetPC(null);
                hex.RedrawPC();
            }
        }
        else        
        {
            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = killedBy;
                pc.acquisitionType = PCAcquisitionType.CapturedByForce;
                killedBy.controlledPcs.Add(pc);
                killedBy.visibleHexes.Add(hex);
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
        killedBy.steelAmount += steelAmount;
        killedBy.mithrilAmount += mithrilAmount;
        killedBy.goldAmount += goldAmount;

        leatherAmount = 0;
        mountsAmount = 0;
        timberAmount = 0;
        ironAmount = 0;
        steelAmount = 0;
        mithrilAmount = 0;
        goldAmount = 0;

        base.Killed(killedBy);
    }

}
