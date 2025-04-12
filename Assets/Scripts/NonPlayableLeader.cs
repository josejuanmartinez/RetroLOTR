using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NonPlayableLeader : Leader
{
	public bool joined = false;

	NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome;

	public void Initialize(Hex hex, NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome)
	{
		this.nonPlayableLeaderBiome = nonPlayableLeaderBiome;
        base.Initialize(hex, nonPlayableLeaderBiome);
	}

	public void CheckArtifactConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;
        if (leader.controlledCharacters.SelectMany(x => x.artifacts).Select(x => x.artifactName).Intersect(nonPlayableLeaderBiome.artifactsToJoin).Any()) Joined(leader);
    }

    public void CheckArmiesConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (nonPlayableLeaderBiome.armiesToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Count() > nonPlayableLeaderBiome.armiesToJoin) Joined(leader);

        if (nonPlayableLeaderBiome.maSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ma).Sum() > nonPlayableLeaderBiome.maSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.arSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ar).Sum() > nonPlayableLeaderBiome.arSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.liSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().li).Sum() > nonPlayableLeaderBiome.liSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.hiSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hi).Sum() > nonPlayableLeaderBiome.hiSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.lcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().lc).Sum() > nonPlayableLeaderBiome.lcSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.hcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hc).Sum() > nonPlayableLeaderBiome.hcSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.caSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ca).Sum() > nonPlayableLeaderBiome.caSizeToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.wsSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ws).Sum() > nonPlayableLeaderBiome.wsSizeToJoin) Joined(leader);

    }

    public void CheckCharacterConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetCommander()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) Joined(leader);
        if (nonPlayableLeaderBiome.agentLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetAgent()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) Joined(leader);
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetEmmissary()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) Joined(leader);
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetMage()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) Joined(leader);

        if (nonPlayableLeaderBiome.commandersToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.commandersToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.agentsToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.agentsToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.emmissarysToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.emmissarysToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.magesToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.magesToJoin) Joined(leader);

        return;
    }

    public void CheckStoresConditions(Leader leader)
    {
        if (killed || joined || leader == this) return;

        if (nonPlayableLeaderBiome.leatherToJoin > 0 && leader.leatherAmount > nonPlayableLeaderBiome.leatherToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.mountsToJoin > 0 && leader.mountsAmount > nonPlayableLeaderBiome.mountsToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.timberToJoin > 0 && leader.timberAmount > nonPlayableLeaderBiome.timberToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.ironToJoin > 0 && leader.ironAmount > nonPlayableLeaderBiome.ironToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.mithrilToJoin > 0 && leader.mithrilAmount > nonPlayableLeaderBiome.mithrilToJoin) Joined(leader);
        if (nonPlayableLeaderBiome.goldToJoin > 0 && leader.goldAmount > nonPlayableLeaderBiome.goldToJoin) Joined(leader);

        return;
    }

    public void CheckActionConditionAtCapital(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return;

        if (nonPlayableLeaderBiome.actionsAtCapital.Contains(action.actionName)) Joined(leader);
    }

    public void CheckActionConditionAnywhere(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return;

        if (nonPlayableLeaderBiome.actionsAnywhere.Contains(action.actionName)) Joined(leader);
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        if(!joined && killedBy.GetAlignment() == alignment && killedBy != this)
        {
            health = 1;
            controlledCharacters.ForEach(x => x.health = 1);
            controlledPcs.ForEach(x => x.DecreaseSize());
            controlledPcs.ForEach(x => x.DecreaseFort());
            Joined(killedBy);
        } else
        {
            base.Killed(killedBy);
        }
    }

    public void Joined(Leader joinedTo)
    {
        MessageDisplay.ShowMessage($"{name} has joined {joinedTo.characterName}", Color.green);
        
        // Create temporary lists to avoid modifying collections during iteration
        List<Character> charactersToTransfer = new (GetOwner().controlledCharacters);
        List<PC> pcsToTransfer = new (GetOwner().controlledPcs);

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
        GetOwner().controlledCharacters.Clear();
        GetOwner().controlledPcs.Clear();
        visibleHexes.Clear();

        // Transfer resources
        joinedTo.leatherAmount += leatherAmount;
        joinedTo.mountsAmount += mountsAmount;
        joinedTo.timberAmount += timberAmount;
        joinedTo.ironAmount += ironAmount;
        joinedTo.mithrilAmount += mithrilAmount;
        joinedTo.goldAmount += goldAmount;

        // Reset resources to 0
        leatherAmount = 0;
        mountsAmount = 0;
        timberAmount = 0;
        ironAmount = 0;
        mithrilAmount = 0;
        goldAmount = 0;

        // Mark as killed and remove from NPCs list safely
        joined = true;

        // Schedule the removal for after the current iteration completes
        StartCoroutine(RemoveFromNPCsNextFrame());
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