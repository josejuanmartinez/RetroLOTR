using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class NonPlayableLeader : Leader
{
	public bool joined = false;
    private bool readyToJoinNotified = false;

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

    public bool ReadyToJoinNotified => readyToJoinNotified;
    public void MarkReadyToJoinNotified() => readyToJoinNotified = true;

	public bool CheckArtifactConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;
        bool meets = leader.controlledCharacters.SelectMany(x => x.artifacts).Select(x => x.artifactName).Intersect(nonPlayableLeaderBiome.artifactsToJoin).Any();
        if (!meets) return false;
        return triggerJoin ? Joined(leader) : true;
    }

    public bool CheckArmiesConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;

        bool meets = false;
        if (nonPlayableLeaderBiome.armiesToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Count() > nonPlayableLeaderBiome.armiesToJoin) meets = true;

        if (nonPlayableLeaderBiome.maSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ma).Sum() > nonPlayableLeaderBiome.maSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.arSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ar).Sum() > nonPlayableLeaderBiome.arSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.liSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().li).Sum() > nonPlayableLeaderBiome.liSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.hiSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hi).Sum() > nonPlayableLeaderBiome.hiSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.lcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().lc).Sum() > nonPlayableLeaderBiome.lcSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.hcSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().hc).Sum() > nonPlayableLeaderBiome.hcSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.caSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ca).Sum() > nonPlayableLeaderBiome.caSizeToJoin) meets = true;
        if (nonPlayableLeaderBiome.wsSizeToJoin > 0 && leader.controlledCharacters.FindAll(x => x.IsArmyCommander()).Select(x => x.GetArmy().ws).Sum() > nonPlayableLeaderBiome.wsSizeToJoin) meets = true;

        if (!meets) return false;
        return triggerJoin ? Joined(leader) : true;
    }

    public bool CheckCharacterConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;

        bool meets = false;

        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetCommander()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) meets = true;
        if (nonPlayableLeaderBiome.agentLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetAgent()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) meets = true;
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetEmmissary()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) meets = true;
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0 && leader.controlledCharacters.Select(x => x.GetMage()).Where(x => x > nonPlayableLeaderBiome.commanderLevelToJoin).Any()) meets = true;

        if (nonPlayableLeaderBiome.commandersToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.commandersToJoin) meets = true;
        if (nonPlayableLeaderBiome.agentsToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.agentsToJoin) meets = true;
        if (nonPlayableLeaderBiome.emmissarysToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.emmissarysToJoin) meets = true;
        if (nonPlayableLeaderBiome.magesToJoin > 0 && leader.controlledCharacters.Where(x => x.GetCommander() > 0).Count() >= nonPlayableLeaderBiome.magesToJoin) meets = true;

        if (!meets) return false;
        return triggerJoin ? Joined(leader) : true;
    }

    public bool CheckStoresConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;

        bool meets = false;
        if (nonPlayableLeaderBiome.leatherToJoin > 0 && leader.leatherAmount > nonPlayableLeaderBiome.leatherToJoin) meets = true;
        if (nonPlayableLeaderBiome.mountsToJoin > 0 && leader.mountsAmount > nonPlayableLeaderBiome.mountsToJoin) meets = true;
        if (nonPlayableLeaderBiome.timberToJoin > 0 && leader.timberAmount > nonPlayableLeaderBiome.timberToJoin) meets = true;
        if (nonPlayableLeaderBiome.ironToJoin > 0 && leader.ironAmount > nonPlayableLeaderBiome.ironToJoin) meets = true;
        if (nonPlayableLeaderBiome.mithrilToJoin > 0 && leader.mithrilAmount > nonPlayableLeaderBiome.mithrilToJoin) meets = true;
        if (nonPlayableLeaderBiome.goldToJoin > 0 && leader.goldAmount > nonPlayableLeaderBiome.goldToJoin) meets = true;

        if (!meets) return false;
        return triggerJoin ? Joined(leader) : true;
    }

    public bool CheckActionConditionAtCapital(Leader leader, CharacterAction action, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;

        if (nonPlayableLeaderBiome.actionsAtCapital.Contains(action.actionName)) {
            return CheckStoresConditions(leader, triggerJoin) || CheckCharacterConditions(leader, triggerJoin) || CheckArmiesConditions(leader, triggerJoin) || CheckArtifactConditions(leader, triggerJoin);
        }
        return false;
    }

    public bool CheckActionConditionAnywhere(Leader leader, CharacterAction action, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader)) return false;

        if (nonPlayableLeaderBiome.actionsAnywhere.Contains(action.actionName)) {
            return CheckStoresConditions(leader, triggerJoin) || CheckCharacterConditions(leader, triggerJoin) || CheckArmiesConditions(leader, triggerJoin) || CheckArtifactConditions(leader, triggerJoin);
        }

        return false;
    }

    public bool CheckJoiningCondition(Character character, CharacterAction action, bool triggerJoin = true)
    {
        if (character == null || action == null) return false;

        bool joinOrWouldJoin = false;
        if (character.hex.GetPC() != null && character.hex.GetPC().owner == this) joinOrWouldJoin = CheckActionConditionAtCapital(character.GetOwner(), action, triggerJoin);
        if(!joinOrWouldJoin) joinOrWouldJoin = CheckActionConditionAnywhere(character.GetOwner(), action, triggerJoin);
        return joinOrWouldJoin;
    }

    private bool CanEvaluateJoin(Leader leader)
    {
        return !(killed || joined || leader == null || leader == this);
    }

    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        // If already allied, defer to base leader death handling
        if (joined)
        {
            base.Killed(killedBy, onlyMask);
            return;
        }

        bool canJoinAttacker = killedBy != null &&
                               killedBy != this &&
                               (alignment == AlignmentEnum.neutral ||
                                killedBy.GetAlignment() == AlignmentEnum.neutral ||
                                killedBy.GetAlignment() == alignment);

        // Try to convert instead of die when alignment allows (same or neutral)
        if (canJoinAttacker && Joined(killedBy))
        {
            health = Mathf.Max(health, 50);
            return;
        }

        // Could not or should not join: collapse realm (kill PCs/armies)
        NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None).FirstOrDefault(x => x.nonPlayableLeader == this);
        if (npli != null) npli.SetDead();

        // Force a realm collapse so holdings are removed rather than transferred
        base.Killed(this, onlyMask);
    }

    public bool Joined(Leader joinedTo)
    {
        short max_it = 10;
        while(true)
        {
            if(max_it-- < 0) break;
            if(joinedTo == null) break;
            if(joinedTo is PlayableLeader) break;
            if(joinedTo is not PlayableLeader) joinedTo = joinedTo.GetOwner();
        }
        if(joinedTo == null || joinedTo is not PlayableLeader) return false;

        PlayableLeader playableLeaderJoinedTo = joinedTo as PlayableLeader;
        
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
            character.owner,
            character.alignment,
            character.startingCharacter,
            character.health
        }).ToList();

        var pcSnapshots = originalPcs.Select(pc => new
        {
            pc,
            pc.owner,
            pc.citySize,
            pc.fortSize,
            pc.loyalty,
            pc.leather,
            pc.mounts,
            pc.timber,
            pc.iron,
            pc.mithril
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


        NonPlayableLeaderIcons npls = FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None).FirstOrDefault(x => x.playableLeader = playableLeaderJoinedTo);
        if(!npls) return false;
        NonPlayableLeaderIcon npli = FindObjectsByType<NonPlayableLeaderIcon>(FindObjectsSortMode.None).FirstOrDefault(x => x.nonPlayableLeader == this);
        if(!npli) return false;
        npli.transform.parent = npls.transform;

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

            health = Mathf.Max(health, 50);
            // Mark as killed and remove from NPCs list safely
            joined = true;

            if(npli != null) npli.SetHired();
            // Schedule the removal for after the current iteration completes
            StartCoroutine(RemoveFromNPCsNextFrame());

            MessageDisplayNoUI.ShowMessage(hex, this, $"{name} has joined {joinedTo.characterName}", Color.green);
            ShowJoinPopup(joinedTo);
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

    private void ShowJoinPopup(Leader joinedTo)
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null) return;

        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        string title = $"{characterName} joins {joinedTo.characterName}";
        string text = $"{characterName} has pledged allegiance to {joinedTo.characterName}.";
        PopupManager.Show(
            title,
            illustrations != null ? illustrations.GetIllustrationByName(characterName) : null,
            illustrations != null ? illustrations.GetIllustrationByName(joinedTo.characterName) : null,
            text,
            false);
    }

    private IEnumerator RemoveFromNPCsNextFrame()
    {
        // Wait until the next frame to remove from the NPCs list
        yield return null;
        if (!joined) yield break;
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.npcs.Contains(this)) game.npcs.Remove(this);
    }

    public void RevealToLeader(PlayableLeader leader, bool showPopup = true)
    {
        if (leader == null) return;

        revealedTo.Add(leader);

        // Always refresh the icon state for the leader that just met them
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .ToList()
            .ForEach(x =>
            {
                if (x.playableLeader == leader) x.RevealToPlayerIfNot(this);
            });

        // Show popup only for the human player turn
        if(showPopup && FindFirstObjectByType<Game>().currentlyPlaying == leader && leader == FindFirstObjectByType<Game>().player)
        {
            FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None).ToList().ForEach(x => x.RevealToPlayerIfNot(this));      
        }
    }

    public void RevealToPlayer()
    {
        revealedTo.Add(FindFirstObjectByType<Game>().player);
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None).ToList().ForEach(x => x.RevealToPlayerIfNot(this));  
    }

    public bool IsRevealedToLeader(PlayableLeader leader)
    {
        return revealedTo.Contains(leader);
    }
    
    public bool IsRevealedToPlayer()
    {
        return revealedTo.Contains(FindFirstObjectByType<Game>().currentlyPlaying);
    }

    public string GetJoiningConditionsText(AlignmentEnum playerAlignment)
    {        
        if(playerAlignment != alignment && alignment != AlignmentEnum.neutral)
        {
            return $"{characterName} follows another alignment and will not join your cause.<br><br>Unfriendly actions can weaken this nation.";
        }

        StringBuilder sb = new ($"{characterName} will join you if you fullfill one of the following conditions:");
        sb.Append("<br><br>");
        if (nonPlayableLeaderBiome.artifactsToJoin != null && nonPlayableLeaderBiome.artifactsToJoin.Count > 0)
        {
            sb.Append($"- Possess artifacts: any of {string.Join(", ", nonPlayableLeaderBiome.artifactsToJoin)}<br>");
        }

        if (nonPlayableLeaderBiome.artifactsQtyToJoin > 0) sb.Append($"- Accumulate {nonPlayableLeaderBiome.artifactsQtyToJoin} artifacts<br>");

        if (nonPlayableLeaderBiome.leatherToJoin > 0) sb.Append($"- <sprite name=\"leather\">[{nonPlayableLeaderBiome.leatherToJoin}]<br>");

        if (nonPlayableLeaderBiome.mountsToJoin > 0) sb.Append($"- <sprite name=\"mounts\">[{nonPlayableLeaderBiome.mountsToJoin}]<br>");

        if (nonPlayableLeaderBiome.timberToJoin > 0) sb.Append($"- <sprite name=\"timber\">[{nonPlayableLeaderBiome.timberToJoin}]<br>");

        if (nonPlayableLeaderBiome.ironToJoin > 0) sb.Append($"- <sprite name=\"iron\">[{nonPlayableLeaderBiome.ironToJoin}]<br>");

        if (nonPlayableLeaderBiome.mithrilToJoin > 0) sb.Append($"- <sprite name=\"mithril\">[{nonPlayableLeaderBiome.mithrilToJoin}]<br>");

        if (nonPlayableLeaderBiome.goldToJoin > 0) sb.Append($"- <sprite name=\"gold\">[{nonPlayableLeaderBiome.goldToJoin}]<br>");
        
        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0) sb.Append($"- Have a <sprite name=\"commander\"> of level [{nonPlayableLeaderBiome.commanderLevelToJoin}]<br>");

        if (nonPlayableLeaderBiome.agentLevelToJoin > 0) sb.Append($"- Have an <sprite name=\"agent\"> of level [{nonPlayableLeaderBiome.agentLevelToJoin}]<br>");
        
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0) sb.Append($"- Have an <sprite name=\"emmissary\"> of level [{nonPlayableLeaderBiome.emmissaryLevelToJoin}]<br>");
        
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0) sb.Append($"- Have a <sprite name=\"mage\"> of level [{nonPlayableLeaderBiome.mageLevelToJoin}]<br>");
        
        if (nonPlayableLeaderBiome.armiesToJoin > 0) sb.Append($"- Have at least {nonPlayableLeaderBiome.armiesToJoin} armies<br>");

        if (nonPlayableLeaderBiome.maSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ma\">{nonPlayableLeaderBiome.maSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.arSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ar\">{nonPlayableLeaderBiome.arSizeToJoin}<br>");
        
        if (nonPlayableLeaderBiome.liSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"li\">{nonPlayableLeaderBiome.liSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.hiSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"hi\">{nonPlayableLeaderBiome.hiSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.lcSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"lc\">{nonPlayableLeaderBiome.lcSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.hcSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"hc\">{nonPlayableLeaderBiome.hcSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.caSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ca\">{nonPlayableLeaderBiome.caSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.wsSizeToJoin > 0) sb.Append($"- Have at least an army with <sprite name=\"ws\">{nonPlayableLeaderBiome.wsSizeToJoin}<br>");

        if (nonPlayableLeaderBiome.commandersToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.commanderLevelToJoin} <sprite name=\"commander\"><br>");

        if (nonPlayableLeaderBiome.agentsToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.agentsToJoin} <sprite name=\"agent\"><br>");

        if (nonPlayableLeaderBiome.emmissarysToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.emmissarysToJoin} <sprite name=\"emmissary\"><br>");
        
        if (nonPlayableLeaderBiome.magesToJoin > 0) sb.Append($"- Hire at least {nonPlayableLeaderBiome.magesToJoin} <sprite name=\"mage\"><br>");

        sb.Append($"The final action to hire them should be run at capital and was not revealed.");      

        return sb.ToString();
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}
