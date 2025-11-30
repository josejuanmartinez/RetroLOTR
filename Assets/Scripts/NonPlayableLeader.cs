using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class NonPlayableLeader : Leader
{
	public bool joined = false;

    public List<PlayableLeader> revealedTo = new();

	NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome;

    public void Initialize(Hex hex, NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome, bool showSpawnMessage = true)
	{
		this.nonPlayableLeaderBiome = nonPlayableLeaderBiome;
        base.Initialize(hex, nonPlayableLeaderBiome, showSpawnMessage);
        PlayableLeaderIcon alignmentPlayableLeader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None).First((x => x.alignment == nonPlayableLeaderBiome.alignment));
        if (!alignmentPlayableLeader)
        {
            Debug.LogWarning($"Could not find PlayableLeaderIcons for alignment {nonPlayableLeaderBiome.alignment}");
        }
        alignmentPlayableLeader.AddNonPlayableLeader(this);
    }

	public bool CheckArtifactConditions(Leader leader)
    {
        if (killed || joined || leader == this) return false;
        if (leader.controlledCharacters.SelectMany(x => x.artifacts).Select(x => x.artifactName).Intersect(nonPlayableLeaderBiome.artifactsToJoin).Any()) return Joined(leader);
        return false;
    }

    public bool CheckArmiesConditions(Leader leader)
    {
        if (killed || joined || leader == this) return false;

        if (nonPlayableLeaderBiome.armiesToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Count() > nonPlayableLeaderBiome.armiesToJoin) return Joined(leader);

        if (nonPlayableLeaderBiome.maSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ma).Sum() > nonPlayableLeaderBiome.maSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.arSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ar).Sum() > nonPlayableLeaderBiome.arSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.liSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().li).Sum() > nonPlayableLeaderBiome.liSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.hiSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hi).Sum() > nonPlayableLeaderBiome.hiSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.lcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().lc).Sum() > nonPlayableLeaderBiome.lcSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.hcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hc).Sum() > nonPlayableLeaderBiome.hcSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.caSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ca).Sum() > nonPlayableLeaderBiome.caSizeToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.wsSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ws).Sum() > nonPlayableLeaderBiome.wsSizeToJoin) return Joined(leader);

        return false;
    }

    public bool CheckCharacterConditions(Leader leader)
    {
        if (killed || joined || leader == this) return false;

        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetCommander()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) return Joined(leader);
        if (nonPlayableLeaderBiome.agentLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetAgent()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) return Joined(leader);
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetEmmissary()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) return Joined(leader);
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetMage()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) return Joined(leader);

        if (nonPlayableLeaderBiome.commandersToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.commandersToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.agentsToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.agentsToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.emmissarysToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.emmissarysToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.magesToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.magesToJoin) return Joined(leader);

        return false;
    }

    public bool CheckStoresConditions(Leader leader)
    {
        if (killed || joined || leader == this) return false;

        if (nonPlayableLeaderBiome.leatherToJoin > 0 && leader.leatherAmount > nonPlayableLeaderBiome.leatherToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.mountsToJoin > 0 && leader.mountsAmount > nonPlayableLeaderBiome.mountsToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.timberToJoin > 0 && leader.timberAmount > nonPlayableLeaderBiome.timberToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.ironToJoin > 0 && leader.ironAmount > nonPlayableLeaderBiome.ironToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.mithrilToJoin > 0 && leader.mithrilAmount > nonPlayableLeaderBiome.mithrilToJoin) return Joined(leader);
        if (nonPlayableLeaderBiome.goldToJoin > 0 && leader.goldAmount > nonPlayableLeaderBiome.goldToJoin) return Joined(leader);

        return false;
    }

    public bool CheckActionConditionAtCapital(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return false;

        if (nonPlayableLeaderBiome.actionsAtCapital.Contains(action.actionName)) {
            return CheckStoresConditions(leader) || CheckCharacterConditions(leader) || CheckArmiesConditions(leader) || CheckArtifactConditions(leader);
        }
        return false;
    }

    public bool CheckActionConditionAnywhere(Leader leader, CharacterAction action)
    {
        if (killed || joined || leader == this) return false;

        if (nonPlayableLeaderBiome.actionsAnywhere.Contains(action.actionName)) {
            return CheckStoresConditions(leader) || CheckCharacterConditions(leader) || CheckArmiesConditions(leader) || CheckArtifactConditions(leader);
        }

        return false;
    }

    public void CheckJoiningCondition(Character character, CharacterAction action)
    {
        bool joined = false;
        if (character.hex.GetPC() != null && character.hex.GetPC().owner == this) joined = CheckActionConditionAtCapital(character.GetOwner(), action);
        if(!joined) CheckActionConditionAnywhere(character.GetOwner(), action);
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        bool realmCollapsed = killedBy == this;
        bool enemyAlignment = alignment != AlignmentEnum.neutral && killedBy.GetAlignment() != alignment;
        if(realmCollapsed || enemyAlignment || joined)
        {            
            NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None).FirstOrDefault(x => x.nonPlayableLeader == this);
            if(npli != null) npli.SetDead();
            base.Killed(killedBy);
        } 
        else
        {         
            Joined(killedBy);
        }
    }

    public bool Joined(Leader joinedTo)
    {
        Leader owner = GetOwner();

        // Snapshot current state so we can roll back on failure
        List<Character> originalCharacters = new(owner.controlledCharacters);
        List<PC> originalPcs = new(owner.controlledPcs);
        List<Hex> originalVisibleHexes = new(visibleHexes);
        List<Character> originalJoinedToCharacters = new(joinedTo.controlledCharacters);
        List<PC> originalJoinedToPcs = new(joinedTo.controlledPcs);
        List<Hex> originalJoinedToVisibleHexes = new(joinedTo.visibleHexes);

        var characterSnapshots = originalCharacters.Select(character => new
        {
            character,
            owner = character.owner,
            alignment = character.alignment,
            startingCharacter = character.startingCharacter,
            health = character.health
        }).ToList();

        var pcSnapshots = originalPcs.Select(pc => new
        {
            pc,
            owner = pc.owner,
            citySize = pc.citySize,
            fortSize = pc.fortSize,
            loyalty = pc.loyalty,
            leather = pc.leather,
            mounts = pc.mounts,
            timber = pc.timber,
            iron = pc.iron,
            mithril = pc.mithril
        }).ToList();

        int originalHealth = health;
        bool originalJoined = joined;
        int originalLeatherAmount = leatherAmount;
        int originalMountsAmount = mountsAmount;
        int originalTimberAmount = timberAmount;
        int originalIronAmount = ironAmount;
        int originalMithrilAmount = mithrilAmount;
        int originalGoldAmount = goldAmount;

        int targetLeatherAmount = joinedTo.leatherAmount;
        int targetMountsAmount = joinedTo.mountsAmount;
        int targetTimberAmount = joinedTo.timberAmount;
        int targetIronAmount = joinedTo.ironAmount;
        int targetMithrilAmount = joinedTo.mithrilAmount;
        int targetGoldAmount = joinedTo.goldAmount;

        NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None).FirstOrDefault(x => x.nonPlayableLeader == this);
        Color? npliBorderColor = npli != null ? npli.border.color : null;

        try
        {
            
            // Create temporary lists to avoid modifying collections during iteration
            List<Character> charactersToTransfer = new (owner.controlledCharacters);
            List<PC> pcsToTransfer = new (owner.controlledPcs);

            // Transfer characters
            foreach (Character character in charactersToTransfer)
            {
                character.health = Math.Min(character.health, 50);
                character.owner = joinedTo;
                character.alignment = joinedTo.alignment;
                character.startingCharacter = false;
                joinedTo.controlledCharacters.Add(character);
            }


            // Transfer PCs
            foreach (PC pc in pcsToTransfer)
            {
                pc.owner = joinedTo;
                pc.DecreaseSize();
                pc.DecreaseFort();            
                joinedTo.controlledPcs.Add(pc);
                joinedTo.visibleHexes.Add(pc.hex);
                if(joinedTo == FindAnyObjectByType<Game>().player) pc.hex.RevealArea(1);
            }

            // Clear the original leader's collections after transfer
            owner.controlledCharacters.Clear();
            owner.controlledPcs.Clear();
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

            health = Math.Min(health, 50);
            // Mark as killed and remove from NPCs list safely
            joined = true;

            if(npli != null) npli.SetHired();
            // Schedule the removal for after the current iteration completes
            StartCoroutine(RemoveFromNPCsNextFrame());

            MessageDisplayNoUI.ShowMessage(hex, this, $"{name} has joined {joinedTo.characterName}", Color.green);
            return true;    
        } catch(Exception e)
        {
            Debug.LogError(e);
            joined = originalJoined;
            health = originalHealth;

            // Restore characters
            foreach (var snapshot in characterSnapshots)
            {
                snapshot.character.owner = snapshot.owner;
                snapshot.character.alignment = snapshot.alignment;
                snapshot.character.startingCharacter = snapshot.startingCharacter;
                snapshot.character.health = snapshot.health;
            }

            // Restore PCs
            foreach (var snapshot in pcSnapshots)
            {
                snapshot.pc.owner = snapshot.owner;
                snapshot.pc.citySize = snapshot.citySize;
                snapshot.pc.fortSize = snapshot.fortSize;
                snapshot.pc.loyalty = snapshot.loyalty;
                snapshot.pc.leather = snapshot.leather;
                snapshot.pc.mounts = snapshot.mounts;
                snapshot.pc.timber = snapshot.timber;
                snapshot.pc.iron = snapshot.iron;
                snapshot.pc.mithril = snapshot.mithril;
                snapshot.pc.hex.RedrawPC();
            }

            owner.controlledCharacters.Clear();
            owner.controlledCharacters.AddRange(originalCharacters);
            owner.controlledPcs.Clear();
            owner.controlledPcs.AddRange(originalPcs);
            visibleHexes.Clear();
            visibleHexes.AddRange(originalVisibleHexes);

            joinedTo.controlledCharacters.Clear();
            joinedTo.controlledCharacters.AddRange(originalJoinedToCharacters);
            joinedTo.controlledPcs.Clear();
            joinedTo.controlledPcs.AddRange(originalJoinedToPcs);
            joinedTo.visibleHexes.Clear();
            joinedTo.visibleHexes.AddRange(originalJoinedToVisibleHexes);

            leatherAmount = originalLeatherAmount;
            mountsAmount = originalMountsAmount;
            timberAmount = originalTimberAmount;
            ironAmount = originalIronAmount;
            mithrilAmount = originalMithrilAmount;
            goldAmount = originalGoldAmount;

            joinedTo.leatherAmount = targetLeatherAmount;
            joinedTo.mountsAmount = targetMountsAmount;
            joinedTo.timberAmount = targetTimberAmount;
            joinedTo.ironAmount = targetIronAmount;
            joinedTo.mithrilAmount = targetMithrilAmount;
            joinedTo.goldAmount = targetGoldAmount;

            if (npli != null && npliBorderColor.HasValue) npli.border.color = npliBorderColor.Value;
        }
        
        return joined;
    }

    private IEnumerator RemoveFromNPCsNextFrame()
    {
        // Wait until the next frame to remove from the NPCs list
        yield return null;
        if (!joined) yield break;
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.npcs.Contains(this)) game.npcs.Remove(this);
    }

    public void RevealToPlayer()
    {
        revealedTo.Add(FindFirstObjectByType<Game>().player);
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None).ToList().ForEach(x => x.RevealToPlayerIfNot(this));  
    }

    public bool IsRevealedToPlayer()
    {
        return revealedTo.Contains(FindFirstObjectByType<Game>().player);
    }

    public string GetJoiningConditionsText(AlignmentEnum playerAlignment)
    {        
        if(playerAlignment != alignment && playerAlignment != AlignmentEnum.neutral)
        {
            return $"{characterName} follows another alignment and will not join your cause.<br/><br/>Unfriendly actions can weaken this nation.";
        }

        StringBuilder sb = new ($"{characterName} will join you under the following conditions:");
        sb.Append("<br><br>");
        if (nonPlayableLeaderBiome.artifactsToJoin != null && nonPlayableLeaderBiome.artifactsToJoin.Count > 0)
        {
            sb.Append($"- Artifacts: {string.Join(", ", nonPlayableLeaderBiome.artifactsToJoin)}<br>");
        }

        if (nonPlayableLeaderBiome.artifactsQtyToJoin > 0) sb.Append($"- Possess {nonPlayableLeaderBiome.artifactsQtyToJoin} artifacts<br>");

        if (nonPlayableLeaderBiome.leatherToJoin > 0) sb.Append($"- <sprite name=\"leather\">[{nonPlayableLeaderBiome.leatherToJoin}]<br>");

        if (nonPlayableLeaderBiome.mountsToJoin > 0) sb.Append($"- <sprite name=\"mounts\">[{nonPlayableLeaderBiome.mountsToJoin}]<br>");

        if (nonPlayableLeaderBiome.timberToJoin > 0) sb.Append($"- <sprite name=\"timber\">[{nonPlayableLeaderBiome.timberToJoin}]<br>");

        if (nonPlayableLeaderBiome.ironToJoin > 0) sb.Append($"- <sprite name=\"iron\">[{nonPlayableLeaderBiome.ironToJoin}]<br>");

        if (nonPlayableLeaderBiome.mithrilToJoin > 0) sb.Append($"- <sprite name=\"mithril\">[{nonPlayableLeaderBiome.mithrilToJoin}]");

        if (nonPlayableLeaderBiome.goldToJoin > 0) sb.Append($"- <sprite name=\"gold\">[{nonPlayableLeaderBiome.goldToJoin}]");
        
        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0) sb.Append($"- Have a <sprite name=\"commander\"> of level [{nonPlayableLeaderBiome.commanderLevelToJoin}]");

        if (nonPlayableLeaderBiome.agentLevelToJoin > 0) sb.Append($"- Have an <sprite name=\"agent\"> of level [{nonPlayableLeaderBiome.agentLevelToJoin}]");
        
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0) sb.Append($"- Have an <sprite name=\"emmissary\"> of level [{nonPlayableLeaderBiome.emmissaryLevelToJoin}]");
        
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0) sb.Append($"- Have a <sprite name=\"mage\"> of level [{nonPlayableLeaderBiome.mageLevelToJoin}]");
        
        if (nonPlayableLeaderBiome.armiesToJoin > 0) sb.Append($"- Have at least {nonPlayableLeaderBiome.armiesToJoin} armies");

        if (nonPlayableLeaderBiome.maSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ma\">{nonPlayableLeaderBiome.maSizeToJoin}");

        if (nonPlayableLeaderBiome.arSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ar\">{nonPlayableLeaderBiome.arSizeToJoin}");
        
        if (nonPlayableLeaderBiome.liSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"li\">{nonPlayableLeaderBiome.liSizeToJoin}");

        if (nonPlayableLeaderBiome.hiSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"hi\">{nonPlayableLeaderBiome.hiSizeToJoin}");

        if (nonPlayableLeaderBiome.lcSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"lc\">{nonPlayableLeaderBiome.lcSizeToJoin}");

        if (nonPlayableLeaderBiome.hcSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"hc\">{nonPlayableLeaderBiome.hcSizeToJoin}");

        if (nonPlayableLeaderBiome.caSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ca\">{nonPlayableLeaderBiome.caSizeToJoin}");

        if (nonPlayableLeaderBiome.wsSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ws\">{nonPlayableLeaderBiome.wsSizeToJoin}");

        if (nonPlayableLeaderBiome.commandersToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.commanderLevelToJoin} <sprite name=\"commander\">");

        if (nonPlayableLeaderBiome.agentsToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.agentsToJoin} <sprite name=\"agent\">");

        if (nonPlayableLeaderBiome.emmissarysToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.emmissarysToJoin} <sprite name=\"emmissary\">");
        
        if (nonPlayableLeaderBiome.magesToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.magesToJoin} <sprite name=\"mage\">");

        sb.Append($"The final action to hire them should be run at capital and was not revealed.");      

        return sb.ToString();
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}
