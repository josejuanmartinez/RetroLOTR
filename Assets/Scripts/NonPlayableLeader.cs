using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NonPlayableLeader : Leader
{

    [Header("Conditions to Join")]
    [Header("Artifacts to Join")]
    public List<Artifact> artifactsToJoin;

    [Header("Stores to Join")]
    public int leatherToJoin = 0;
    public int mountsToJoin = 0;
    public int timberToJoin = 0;
    public int ironToJoin = 0;
    public int mithrilToJoin = 0;
    public int goldToJoin = 0;

    [Header("Character Level to Join")]
    public int commanderLevelToJoin = 0;
    public int agentLevelToJoin = 0;
    public int emmissaryLevelToJoin = 0;
    public int mageLevelToJoin = 0;

    [Header("Armies Size to Join")]
    public int armiesToJoin = 0;
    public int maSizeToJoin = 0;
    public int arSizeToJoin = 0;
    public int liSizeToJoin = 0;
    public int hiSizeToJoin = 0;
    public int lcSizeToJoin = 0;
    public int hcSizeToJoin = 0;
    public int caSizeToJoin = 0;
    public int wsSizeToJoin = 0;

    [Header("Characters to Join")]
    public int commandersToJoin = 0;
    public int agentsToJoin = 0;
    public int emmissarysToJoin = 0;
    public int magesToJoin = 0;

    [Header("Actions to Join")]
    [Header("Actions At Capital to Join")]
    public List<CharacterAction> actionsAtCapital;

    [Header("Actions Anywhere to Join")]
    public List<CharacterAction> actionsAnywhere;

    public bool joined = false;
    public void CheckArtifactConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;
        if (leader.controlledCharacters.SelectMany(x => x.artifacts).Intersect(artifactsToJoin).Any()) Joined(leader);
    }

    public void CheckArmiesConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (armiesToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Count() > armiesToJoin) Joined(leader);

        if (maSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ma).Sum() > maSizeToJoin) Joined(leader);
        if (arSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ar).Sum() > arSizeToJoin) Joined(leader);
        if (liSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().li).Sum() > liSizeToJoin) Joined(leader);
        if (hiSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hi).Sum() > hiSizeToJoin) Joined(leader);
        if (lcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().lc).Sum() > lcSizeToJoin) Joined(leader);
        if (hcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hc).Sum() > hcSizeToJoin) Joined(leader);
        if (caSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ca).Sum() > caSizeToJoin) Joined(leader);
        if (wsSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ws).Sum() > wsSizeToJoin) Joined(leader);

    }

    public void CheckCharacterConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (commanderLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetCommander()).Where(x => x > commanderLevelToJoin).Any()) Joined(leader);
        if (agentLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetAgent()).Where(x => x > commanderLevelToJoin).Any()) Joined(leader);
        if (emmissaryLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetEmmissary()).Where(x => x > commanderLevelToJoin).Any()) Joined(leader);
        if (mageLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetMage()).Where(x => x > commanderLevelToJoin).Any()) Joined(leader);

        if (commandersToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= commandersToJoin) Joined(leader);
        if (agentsToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= agentsToJoin) Joined(leader);
        if (emmissarysToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= emmissarysToJoin) Joined(leader);
        if (magesToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= magesToJoin) Joined(leader);

        return;
    }

    public void CheckStoresConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (leatherToJoin > 0 && leader.leatherAmount > leatherToJoin) Joined(leader);
        if (mountsToJoin > 0 && leader.mountsAmount > mountsToJoin) Joined(leader);
        if (timberToJoin > 0 && leader.timberAmount > timberToJoin) Joined(leader);
        if (ironToJoin > 0 && leader.ironAmount > ironToJoin) Joined(leader);
        if (mithrilToJoin > 0 && leader.mithrilAmount > mithrilToJoin) Joined(leader);
        if (goldToJoin > 0 && leader.goldAmount > goldToJoin) Joined(leader);

        return;
    }

    public void CheckActionConditionAtCapital(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return;

        if (actionsAtCapital.Contains(action)) Joined(leader);
    }

    public void CheckActionConditionAnywhere(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return;

        if (actionsAnywhere.Contains(action)) Joined(leader);
    }

    override public void Killed(Leader joinedTo)
    {
        if (killed || joinedTo == this) return;

        if(!joined)
        {
            health = 1;
            controlledCharacters.ForEach(x => x.health = 1);
            controlledPcs.ForEach(x => x.DecreaseSize());
            controlledPcs.ForEach(x => x.DecreaseFort());
        } else
        {
            killed = true;
            health = 0;
        }

        Joined(joinedTo);
    }

    public void Joined(Leader joinedTo)
    {
        if (joined || joinedTo == this) return;
        if (!killed && joinedTo.GetAlignment() != alignment) return;

        if(killed)
        {
            MessageDisplay.ShowMessage($"{name} has been killed by {joinedTo.characterName}", Color.red);
        } else
        {
            MessageDisplay.ShowMessage($"{name} has joined {joinedTo.characterName}", Color.green);
        }

        NonPlayableLeader nonPlayable = this;

        // Create temporary lists to avoid modifying collections during iteration
        List<Character> charactersToTransfer = new List<Character>(GetOwner().controlledCharacters);
        List<PC> pcsToTransfer = new List<PC>(GetOwner().controlledPcs);

        // Transfer characters
        foreach (Character character in charactersToTransfer)
        {
            character.owner = joinedTo;
            character.alignment = joinedTo.alignment;
            joinedTo.controlledCharacters.Add(character);
        }

        // Transfer PCs
        foreach (PC pc in pcsToTransfer)
        {
            pc.owner = joinedTo;
            joinedTo.controlledPcs.Add(pc);
            joinedTo.visibleHexes.Add(pc.hex);
            if(joinedTo == FindAnyObjectByType<Game>().player) pc.hex.RevealArea(1);
        }

        // Clear the original leader's collections after transfer
        nonPlayable.controlledCharacters.Clear();
        nonPlayable.controlledPcs.Clear();
        nonPlayable.visibleHexes.Clear();

        // Transfer resources
        joinedTo.leatherAmount += nonPlayable.leatherAmount;
        joinedTo.mountsAmount += nonPlayable.mountsAmount;
        joinedTo.timberAmount += nonPlayable.timberAmount;
        joinedTo.ironAmount += nonPlayable.ironAmount;
        joinedTo.mithrilAmount += nonPlayable.mithrilAmount;
        joinedTo.goldAmount += nonPlayable.goldAmount;

        // Reset resources to 0
        nonPlayable.leatherAmount = 0;
        nonPlayable.mountsAmount = 0;
        nonPlayable.timberAmount = 0;
        nonPlayable.ironAmount = 0;
        nonPlayable.mithrilAmount = 0;
        nonPlayable.goldAmount = 0;

        // Mark as killed and remove from NPCs list safely
        nonPlayable.joined = true;

        // Schedule the removal for after the current iteration completes
        StartCoroutine(RemoveFromNPCsNextFrame());

        enabled = false;
    }

    private IEnumerator RemoveFromNPCsNextFrame()
    {
        // Wait until the next frame to remove from the NPCs list
        yield return null;
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.npcs.Contains(this)) game.npcs.Remove(this);
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}