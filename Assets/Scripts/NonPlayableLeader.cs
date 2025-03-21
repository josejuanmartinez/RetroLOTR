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

    public void CheckArtifactConditions(Leader leader)
    {
        if (killed) return;
        if (leader.controlledCharacters.SelectMany(x => x.artifacts).Intersect(artifactsToJoin).Any()) Killed(leader);
    }

    public void CheckArmiesConditions(Leader leader)
    {
        if (killed) return;

        if (armiesToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Count() > armiesToJoin) Killed(leader);

        if (maSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ma).Sum() > maSizeToJoin) Killed(leader);
        if (arSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ar).Sum() > arSizeToJoin) Killed(leader);
        if (liSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().li).Sum() > liSizeToJoin) Killed(leader);
        if (hiSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hi).Sum() > hiSizeToJoin) Killed(leader);
        if (lcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().lc).Sum() > lcSizeToJoin) Killed(leader);
        if (hcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hc).Sum() > hcSizeToJoin) Killed(leader);
        if (caSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ca).Sum() > caSizeToJoin) Killed(leader);
        if (wsSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ws).Sum() > wsSizeToJoin) Killed(leader);

    }

    public void CheckCharacterConditions(Leader leader)
    {
        if (killed) return;

        if (commanderLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetCommander()).Where(x => x > commanderLevelToJoin).Any()) Killed(leader);
        if (agentLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetAgent()).Where(x => x > commanderLevelToJoin).Any()) Killed(leader);
        if (emmissaryLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetEmmissary()).Where(x => x > commanderLevelToJoin).Any()) Killed(leader);
        if (mageLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetMage()).Where(x => x > commanderLevelToJoin).Any()) Killed(leader);

        if (commandersToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= commandersToJoin) Killed(leader);
        if (agentsToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= agentsToJoin) Killed(leader);
        if (emmissarysToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= emmissarysToJoin) Killed(leader);
        if (magesToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= magesToJoin) Killed(leader);

        return;
    }

    public void CheckStoresConditions(Leader leader)
    {
        if (killed) return;

        if (leatherToJoin > 0 && leader.leatherAmount > leatherToJoin) Killed(leader);
        if (mountsToJoin > 0 && leader.mountsAmount > mountsToJoin) Killed(leader);
        if (timberToJoin > 0 && leader.timberAmount > timberToJoin) Killed(leader);
        if (ironToJoin > 0 && leader.ironAmount > ironToJoin) Killed(leader);
        if (mithrilToJoin > 0 && leader.mithrilAmount > mithrilToJoin) Killed(leader);
        if (goldToJoin > 0 && leader.goldAmount > goldToJoin) Killed(leader);

        return;
    }

    public void CheckActionConditionAtCapital(Leader leader, CharacterAction action)
    {
        if (killed) return;

        if (actionsAtCapital.Contains(action)) Killed(leader);
    }

    public void CheckActionConditionAnywhere(Leader leader, CharacterAction action)
    {
        if (killed) return;

        if (actionsAnywhere.Contains(action)) Killed(leader);
    }

    override public void Killed(Leader killedBy)
    {
        NonPlayableLeader nonPlayable = this;
        nonPlayable.health = 1;

        foreach (Character character in GetOwner().controlledCharacters)
        {
            character.owner = killedBy;
            character.alignment = killedBy.alignment;
            killedBy.controlledCharacters.Add(character);
        }

        foreach (PC pc in GetOwner().controlledPcs)
        {
            pc.owner = killedBy;
            killedBy.controlledPcs.Add(pc);
            killedBy.visibleHexes.Add(pc.hex);
        }

        nonPlayable.controlledCharacters = new List<Character>();
        nonPlayable.controlledPcs = new List<PC>();
        nonPlayable.visibleHexes = new List<Hex>();

        killedBy.leatherAmount += nonPlayable.leatherAmount;
        killedBy.mountsAmount += nonPlayable.mountsAmount;
        killedBy.timberAmount += nonPlayable.timberAmount;
        killedBy.ironAmount += nonPlayable.ironAmount;
        killedBy.mithrilAmount += nonPlayable.mithrilAmount;
        killedBy.goldAmount += nonPlayable.goldAmount;

        nonPlayable.leatherAmount = 0;
        nonPlayable.mountsAmount = 0;
        nonPlayable.timberAmount = 0;
        nonPlayable.ironAmount = 0;
        nonPlayable.mithrilAmount = 0;
        nonPlayable.goldAmount = 0;

        nonPlayable.killed = true;

        FindFirstObjectByType<Game>().npcs.Remove(nonPlayable);

        enabled = false;
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}