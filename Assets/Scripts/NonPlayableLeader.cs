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
    private bool iconsInitialized = false;

    public List<PlayableLeader> revealedTo = new();
    private bool playerRevealPopupShown = false;

	NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome;
    private readonly Dictionary<Leader, HashSet<string>> actionsAtCapitalByLeader = new();

	public void Initialize(Hex hex, NonPlayableLeaderBiomeConfig nonPlayableLeaderBiome, bool showSpawnMessage = true)
	{
		this.nonPlayableLeaderBiome = nonPlayableLeaderBiome;
        this.nonPlayableLeaderBiome.actionsAtCapital ??= new();
        this.nonPlayableLeaderBiome.actionsAnywhere ??= new();
        base.Initialize(hex, nonPlayableLeaderBiome, showSpawnMessage);
        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.started)
        {
            InitializeIcons();
        }
    }

    public void InitializeIcons()
    {
        if (iconsInitialized) return;
        iconsInitialized = true;
        if (nonPlayableLeaderBiome == null) return;

        PlayableLeaderIcon alignmentPlayableLeader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x.alignment == nonPlayableLeaderBiome.alignment);
        if (!alignmentPlayableLeader)
        {
            Debug.LogWarning($"Could not find PlayableLeaderIcons for alignment {nonPlayableLeaderBiome.alignment}");
            return;
        }
        alignmentPlayableLeader.AddNonPlayableLeader(this);
    }

    public bool ReadyToJoinNotified => readyToJoinNotified;
    public void MarkReadyToJoinNotified() => readyToJoinNotified = true;

    private static string NormalizeActionName(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return string.Empty;
        string baseName = ActionNameUtils.StripShortcut(actionName);
        return new string(baseName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static void RecordActionCompleted(Character actor, string actionName, Hex actionHex)
    {
        if (actor == null) return;
        Leader owner = actor.GetOwner();
        if (owner == null) return;

        owner.RecordActionHistory(actionName);

        PC pc = actionHex != null ? actionHex.GetPC() : null;
        if (pc == null || pc.owner is not NonPlayableLeader npl || !pc.isCapital) return;

        npl.RecordCapitalAction(owner, actionName);
    }

    private void RecordCapitalAction(Leader leader, string actionName)
    {
        string normalized = NormalizeActionName(actionName);
        if (leader == null || string.IsNullOrEmpty(normalized)) return;
        if (nonPlayableLeaderBiome.actionsAtCapital == null || !nonPlayableLeaderBiome.actionsAtCapital.Any()) return;
        if (!nonPlayableLeaderBiome.actionsAtCapital.Any(req => NormalizeActionName(req) == normalized)) return;

        if (!actionsAtCapitalByLeader.TryGetValue(leader, out HashSet<string> actions))
        {
            actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            actionsAtCapitalByLeader[leader] = actions;
        }
        actions.Add(normalized);
    }

	public bool CheckArtifactConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader) || !IsAlignmentCompatibleWith(leader)) return false;
        bool meets = HasRequiredJoinArtifact(leader) || MeetsArtifactCountRequirement(leader);
        return meets && (!triggerJoin || Joined(leader));
    }

    public bool CheckArmiesConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader) || !IsAlignmentCompatibleWith(leader)) return false;

        bool meets = HasArmyRequirements(leader);
        return meets && (!triggerJoin || Joined(leader));
    }

    public bool CheckCharacterConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader) || !IsAlignmentCompatibleWith(leader)) return false;

        bool meets = HasCharacterLevelRequirements(leader) && HasCharacterCountRequirements(leader);
        return meets && (!triggerJoin || Joined(leader));
    }

    public bool CheckStoresConditions(Leader leader, bool triggerJoin = true)
    {
        if (!CanEvaluateJoin(leader) || !IsAlignmentCompatibleWith(leader)) return false;

        bool meets = HasStoreRequirements(leader);
        return meets && (!triggerJoin || Joined(leader));
    }

    public bool CheckJoiningCondition(Character character, CharacterAction action, bool triggerJoin = true)
    {
        if (character == null || action == null) return false;

        if (!string.Equals(action.GetType().Name, "StateAllegiance", StringComparison.OrdinalIgnoreCase)) return false;

        PC pc = character.hex.GetPC();
        if (pc == null || pc.owner != this || !pc.isCapital) return false;

        return triggerJoin ? AttemptJoin(character.GetOwner()) : MeetsJoiningRequirements(character.GetOwner());
    }

    private bool CanEvaluateJoin(Leader leader)
    {
        return !(killed || joined || leader == null || leader == this);
    }

    private bool AlignmentsCompatible(Leader leader)
    {
        if (leader == null) return false;
        AlignmentEnum leaderAlignment = leader.GetAlignment();
        if (alignment == AlignmentEnum.neutral || leaderAlignment == AlignmentEnum.neutral) return true;
        return leaderAlignment == alignment;
    }

    public bool IsAlignmentCompatibleWith(Leader leader)
    {
        return AlignmentsCompatible(leader);
    }

    private IEnumerable<Character> GetLivingCharacters(Leader leader)
    {
        if (leader == null) return Enumerable.Empty<Character>();
        return leader.controlledCharacters.Where(x => x != null && !x.killed);
    }

    private bool HasRequiredJoinArtifact(Leader leader)
    {
        if (leader == null) return false;
        if (nonPlayableLeaderBiome.artifactsToJoin == null || nonPlayableLeaderBiome.artifactsToJoin.Count == 0) return false;
        HashSet<string> requiredArtifacts = new(nonPlayableLeaderBiome.artifactsToJoin.Select(NormalizeActionName), StringComparer.OrdinalIgnoreCase);
        return GetLivingCharacters(leader)
            .SelectMany(x => x.artifacts)
            .Select(x => NormalizeActionName(x.artifactName))
            .Any(x => requiredArtifacts.Contains(x));
    }

    private bool MeetsArtifactCountRequirement(Leader leader)
    {
        if (nonPlayableLeaderBiome.artifactsQtyToJoin <= 0) return true;
        return GetLivingCharacters(leader).SelectMany(x => x.artifacts).Count() >= nonPlayableLeaderBiome.artifactsQtyToJoin;
    }

    private bool HasStoreRequirements(Leader leader)
    {
        if (leader == null) return false;

        bool hasLeather = nonPlayableLeaderBiome.leatherToJoin <= 0 || leader.leatherAmount >= nonPlayableLeaderBiome.leatherToJoin;
        bool hasMounts = nonPlayableLeaderBiome.mountsToJoin <= 0 || leader.mountsAmount >= nonPlayableLeaderBiome.mountsToJoin;
        bool hasTimber = nonPlayableLeaderBiome.timberToJoin <= 0 || leader.timberAmount >= nonPlayableLeaderBiome.timberToJoin;
        bool hasIron = nonPlayableLeaderBiome.ironToJoin <= 0 || leader.ironAmount >= nonPlayableLeaderBiome.ironToJoin;
        bool hasSteel = nonPlayableLeaderBiome.steelToJoin <= 0 || leader.steelAmount >= nonPlayableLeaderBiome.steelToJoin;
        bool hasMithril = nonPlayableLeaderBiome.mithrilToJoin <= 0 || leader.mithrilAmount >= nonPlayableLeaderBiome.mithrilToJoin;
        bool hasGold = nonPlayableLeaderBiome.goldToJoin <= 0 || leader.goldAmount >= nonPlayableLeaderBiome.goldToJoin;

        return hasLeather && hasMounts && hasTimber && hasIron && hasSteel && hasMithril && hasGold;
    }

    private bool HasAnyStoreRequirements()
    {
        return nonPlayableLeaderBiome != null
            && (nonPlayableLeaderBiome.leatherToJoin > 0
                || nonPlayableLeaderBiome.mountsToJoin > 0
                || nonPlayableLeaderBiome.timberToJoin > 0
                || nonPlayableLeaderBiome.ironToJoin > 0
                || nonPlayableLeaderBiome.steelToJoin > 0
                || nonPlayableLeaderBiome.mithrilToJoin > 0
                || nonPlayableLeaderBiome.goldToJoin > 0);
    }

    private bool HasCharacterLevelRequirements(Leader leader)
    {
        IEnumerable<Character> characters = GetLivingCharacters(leader);
        bool hasCommander = nonPlayableLeaderBiome.commanderLevelToJoin <= 0 || characters.Any(x => x.GetCommander() >= nonPlayableLeaderBiome.commanderLevelToJoin);
        bool hasAgent = nonPlayableLeaderBiome.agentLevelToJoin <= 0 || characters.Any(x => x.GetAgent() >= nonPlayableLeaderBiome.agentLevelToJoin);
        bool hasEmmissary = nonPlayableLeaderBiome.emmissaryLevelToJoin <= 0 || characters.Any(x => x.GetEmmissary() >= nonPlayableLeaderBiome.emmissaryLevelToJoin);
        bool hasMage = nonPlayableLeaderBiome.mageLevelToJoin <= 0 || characters.Any(x => x.GetMage() >= nonPlayableLeaderBiome.mageLevelToJoin);
        return hasCommander && hasAgent && hasEmmissary && hasMage;
    }

    private bool HasAnyCharacterLevelRequirements()
    {
        return nonPlayableLeaderBiome != null
            && (nonPlayableLeaderBiome.commanderLevelToJoin > 0
                || nonPlayableLeaderBiome.agentLevelToJoin > 0
                || nonPlayableLeaderBiome.emmissaryLevelToJoin > 0
                || nonPlayableLeaderBiome.mageLevelToJoin > 0);
    }

    private bool HasCharacterCountRequirements(Leader leader)
    {
        IEnumerable<Character> characters = GetLivingCharacters(leader);
        bool commanders = nonPlayableLeaderBiome.commandersToJoin <= 0 || characters.Count(x => x.GetCommander() > 0) >= nonPlayableLeaderBiome.commandersToJoin;
        bool agents = nonPlayableLeaderBiome.agentsToJoin <= 0 || characters.Count(x => x.GetAgent() > 0) >= nonPlayableLeaderBiome.agentsToJoin;
        bool emmissaries = nonPlayableLeaderBiome.emmissarysToJoin <= 0 || characters.Count(x => x.GetEmmissary() > 0) >= nonPlayableLeaderBiome.emmissarysToJoin;
        bool mages = nonPlayableLeaderBiome.magesToJoin <= 0 || characters.Count(x => x.GetMage() > 0) >= nonPlayableLeaderBiome.magesToJoin;
        return commanders && agents && emmissaries && mages;
    }

    private bool HasAnyCharacterCountRequirements()
    {
        return nonPlayableLeaderBiome != null
            && (nonPlayableLeaderBiome.commandersToJoin > 0
                || nonPlayableLeaderBiome.agentsToJoin > 0
                || nonPlayableLeaderBiome.emmissarysToJoin > 0
                || nonPlayableLeaderBiome.magesToJoin > 0);
    }

    private bool HasArmyRequirements(Leader leader)
    {
        IEnumerable<Army> armies = GetLivingCharacters(leader).Where(x => x.IsArmyCommander() && x.GetArmy() != null).Select(x => x.GetArmy());
        bool armyCount = nonPlayableLeaderBiome.armiesToJoin <= 0 || armies.Count() >= nonPlayableLeaderBiome.armiesToJoin;
        bool ma = nonPlayableLeaderBiome.maSizeToJoin <= 0 || armies.Sum(x => x.ma) >= nonPlayableLeaderBiome.maSizeToJoin;
        bool ar = nonPlayableLeaderBiome.arSizeToJoin <= 0 || armies.Sum(x => x.ar) >= nonPlayableLeaderBiome.arSizeToJoin;
        bool li = nonPlayableLeaderBiome.liSizeToJoin <= 0 || armies.Sum(x => x.li) >= nonPlayableLeaderBiome.liSizeToJoin;
        bool hi = nonPlayableLeaderBiome.hiSizeToJoin <= 0 || armies.Sum(x => x.hi) >= nonPlayableLeaderBiome.hiSizeToJoin;
        bool lc = nonPlayableLeaderBiome.lcSizeToJoin <= 0 || armies.Sum(x => x.lc) >= nonPlayableLeaderBiome.lcSizeToJoin;
        bool hc = nonPlayableLeaderBiome.hcSizeToJoin <= 0 || armies.Sum(x => x.hc) >= nonPlayableLeaderBiome.hcSizeToJoin;
        bool ca = nonPlayableLeaderBiome.caSizeToJoin <= 0 || armies.Sum(x => x.ca) >= nonPlayableLeaderBiome.caSizeToJoin;
        bool ws = nonPlayableLeaderBiome.wsSizeToJoin <= 0 || armies.Sum(x => x.ws) >= nonPlayableLeaderBiome.wsSizeToJoin;

        return armyCount && ma && ar && li && hi && lc && hc && ca && ws;
    }

    private bool HasAnyArmyRequirements()
    {
        return nonPlayableLeaderBiome != null
            && (nonPlayableLeaderBiome.armiesToJoin > 0
                || nonPlayableLeaderBiome.maSizeToJoin > 0
                || nonPlayableLeaderBiome.arSizeToJoin > 0
                || nonPlayableLeaderBiome.liSizeToJoin > 0
                || nonPlayableLeaderBiome.hiSizeToJoin > 0
                || nonPlayableLeaderBiome.lcSizeToJoin > 0
                || nonPlayableLeaderBiome.hcSizeToJoin > 0
                || nonPlayableLeaderBiome.caSizeToJoin > 0
                || nonPlayableLeaderBiome.wsSizeToJoin > 0);
    }

    private bool HasCompletedCapitalActions(Leader leader)
    {
        if (nonPlayableLeaderBiome.actionsAtCapital == null || nonPlayableLeaderBiome.actionsAtCapital.Count == 0) return true;
        if (!actionsAtCapitalByLeader.TryGetValue(leader, out HashSet<string> actions)) return false;
        return nonPlayableLeaderBiome.actionsAtCapital.All(x => actions.Contains(NormalizeActionName(x)));
    }

    private bool HasCompletedAnywhereActions(Leader leader)
    {
        if (nonPlayableLeaderBiome.actionsAnywhere == null || nonPlayableLeaderBiome.actionsAnywhere.Count == 0) return true;
        if (leader == null) return false;
        return nonPlayableLeaderBiome.actionsAnywhere.All(action => leader.HasPerformedAction(action));
    }

    private bool MeetsAllJoinConditions(Leader leader)
    {
        return HasStoreRequirements(leader)
            && HasCharacterLevelRequirements(leader)
            && HasCharacterCountRequirements(leader)
            && HasArmyRequirements(leader)
            && MeetsArtifactCountRequirement(leader)
            && HasCompletedAnywhereActions(leader)
            && HasCompletedCapitalActions(leader);
    }

    public bool MeetsJoiningRequirements(Leader leader)
    {
        if (!CanEvaluateJoin(leader)) return false;
        if (!IsAlignmentCompatibleWith(leader)) return false;

        if (HasRequiredJoinArtifact(leader)) return true;

        return MeetsAllJoinConditions(leader);
    }

    public bool AttemptJoin(Leader leader)
    {
        if (!MeetsJoiningRequirements(leader)) return false;
        return Joined(leader);
    }

    public float GetPartialJoinChance(Leader leader, float minChance = 0.02f, float maxChance = 0.15f)
    {
        if (leader == null || nonPlayableLeaderBiome == null) return 0f;
        if (!IsAlignmentCompatibleWith(leader)) return 0f;

        if (HasRequiredJoinArtifact(leader)) return maxChance;

        int total = 0;
        int met = 0;

        if (nonPlayableLeaderBiome.artifactsQtyToJoin > 0)
        {
            total++;
            if (MeetsArtifactCountRequirement(leader)) met++;
        }

        if (HasAnyStoreRequirements())
        {
            total++;
            if (HasStoreRequirements(leader)) met++;
        }

        if (HasAnyCharacterLevelRequirements())
        {
            total++;
            if (HasCharacterLevelRequirements(leader)) met++;
        }

        if (HasAnyCharacterCountRequirements())
        {
            total++;
            if (HasCharacterCountRequirements(leader)) met++;
        }

        if (HasAnyArmyRequirements())
        {
            total++;
            if (HasArmyRequirements(leader)) met++;
        }

        if (nonPlayableLeaderBiome.actionsAnywhere != null && nonPlayableLeaderBiome.actionsAnywhere.Count > 0)
        {
            total++;
            if (HasCompletedAnywhereActions(leader)) met++;
        }

        if (nonPlayableLeaderBiome.actionsAtCapital != null && nonPlayableLeaderBiome.actionsAtCapital.Count > 0)
        {
            total++;
            if (HasCompletedCapitalActions(leader)) met++;
        }

        if (total <= 0) return minChance;

        float ratio = Mathf.Clamp01(met / (float)total);
        return Mathf.Lerp(minChance, maxChance, ratio);
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
            pc.steel,
            pc.mithril
        }).ToList();

        int originalHealth = health;
        bool originalJoined = joined;
        int originalLeatherAmount = leatherAmount;
        int originalMountsAmount = mountsAmount;
        int originalTimberAmount = timberAmount;
        int originalIronAmount = ironAmount;
        int originalSteelAmount = steelAmount;
        int originalMithrilAmount = mithrilAmount;
        int originalGoldAmount = goldAmount;

        int targetLeatherAmount = joinedTo.leatherAmount;
        int targetMountsAmount = joinedTo.mountsAmount;
        int targetTimberAmount = joinedTo.timberAmount;
        int targetIronAmount = joinedTo.ironAmount;
        int targetSteelAmount = joinedTo.steelAmount;
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
                character.owner = joinedTo;
                character.alignment = joinedTo.alignment;
                character.startingCharacter = false;
                joinedTo.controlledCharacters.Add(character);
            }


            // Transfer PCs
            foreach (PC pc in pcsToTransfer)
            {
                pc.owner = joinedTo;
                pc.acquisitionType = PCAcquisitionType.Joined;
                joinedTo.controlledPcs.Add(pc);
                joinedTo.visibleHexes.Add(pc.hex);
                if(joinedTo == FindAnyObjectByType<Game>().player) pc.hex.RevealArea(1);
            }

            // Clear the original leader's collections after transfer
            owner.controlledCharacters.Clear();
            owner.controlledPcs.Clear();
            visibleHexes.Clear();

            health = Mathf.Max(health, 50);
            // Mark as killed and remove from NPCs list safely
            joined = true;

            if(npli != null) npli.SetHired();
            // Schedule the removal for after the current iteration completes
            StartCoroutine(RemoveFromNPCsNextFrame());

            MessageDisplayNoUI.ShowMessage(hex, this, $"{name} has joined {joinedTo.characterName}", Color.green);
            ShowJoinPopup(joinedTo);
            CharacterIcons.RefreshForHumanPlayerOf(joinedTo);
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
                snapshot.pc.steel = snapshot.steel;
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
            steelAmount = originalSteelAmount;
            mithrilAmount = originalMithrilAmount;
            goldAmount = originalGoldAmount;

            joinedTo.leatherAmount = targetLeatherAmount;
            joinedTo.mountsAmount = targetMountsAmount;
            joinedTo.timberAmount = targetTimberAmount;
            joinedTo.ironAmount = targetIronAmount;
            joinedTo.steelAmount = targetSteelAmount;
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

        if (!revealedTo.Contains(leader)) revealedTo.Add(leader);

        // Always refresh the icon state for the leader that just met them
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .ToList()
            .ForEach(x =>
            {
                if (x.playableLeader == leader) x.RevealToPlayerIfNot(this);
            });

        // Show popup only for the human player's turn and only once for the player
        Game game = FindFirstObjectByType<Game>();
        if (showPopup && game != null && game.IsPlayerCurrentlyPlaying() && leader == game.player && !playerRevealPopupShown)
        {
            RevealToPlayerIcons(game);
            playerRevealPopupShown = true;
        }
    }

    public void RevealToPlayer()
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null) return;
        if (!revealedTo.Contains(game.player)) revealedTo.Add(game.player);
        RevealToPlayerIcons(game);
        playerRevealPopupShown = true;
    }

    public bool IsRevealedToLeader(PlayableLeader leader)
    {
        return revealedTo.Contains(leader);
    }
    
    public bool IsRevealedToPlayer()
    {
        return revealedTo.Contains(FindFirstObjectByType<Game>().currentlyPlaying);
    }

    public bool ShouldShowPlayerRevealPopup()
    {
        Game game = FindFirstObjectByType<Game>();
        return game != null && game.IsPlayerCurrentlyPlaying() && !playerRevealPopupShown;
    }

    private void RevealToPlayerIcons(Game game)
    {
        FindObjectsByType<NonPlayableLeaderIcons>(FindObjectsSortMode.None)
            .ToList()
            .ForEach(x =>
            {
                x.RevealToPlayerIfNot(this);
            });
    }

    private string FormatRequirement(string description, bool met, string progress = "")
    {
        string status = met ? "<color=#00ff00>completed</color>" : "<color=#ff0000>pending</color>";
        if (!string.IsNullOrWhiteSpace(progress)) description += $" ({progress})";
        return $"- {description} [{status}]<br>";
    }

    public string GetJoiningConditionsText(Leader leader)
    {
        StringBuilder sb = new();
        bool alignmentOk = IsAlignmentCompatibleWith(leader);

        sb.Append($"<b>{characterName}</b> requires an aligned or neutral sponsor.<br>");
        sb.Append(FormatRequirement("Alignment is the same side or neutral", alignmentOk));
        if (!alignmentOk)
        {
            sb.Append("Unfriendly actions may still weaken this realm, but alliance is impossible while alignments differ.");
            return sb.ToString();
        }

        bool hasArtifactBypass = HasRequiredJoinArtifact(leader);
        if (nonPlayableLeaderBiome.artifactsToJoin != null && nonPlayableLeaderBiome.artifactsToJoin.Count > 0)
        {
            sb.Append(FormatRequirement($"Hold any of: {string.Join(", ", nonPlayableLeaderBiome.artifactsToJoin)} (alternative to the tasks below)", hasArtifactBypass));
        }

        sb.Append("Otherwise, complete every requirement below:<br>");

        // Artifact count
        if (nonPlayableLeaderBiome.artifactsQtyToJoin > 0)
        {
            int currentArtifacts = GetLivingCharacters(leader).SelectMany(x => x.artifacts).Count();
            sb.Append(FormatRequirement($"Hold at least {nonPlayableLeaderBiome.artifactsQtyToJoin} artifacts", currentArtifacts >= nonPlayableLeaderBiome.artifactsQtyToJoin, $"{currentArtifacts}/{nonPlayableLeaderBiome.artifactsQtyToJoin}"));
        }

        // Stores
        if (nonPlayableLeaderBiome.leatherToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"leather\">[{nonPlayableLeaderBiome.leatherToJoin}]", leader != null && leader.leatherAmount >= nonPlayableLeaderBiome.leatherToJoin, $"{leader?.leatherAmount ?? 0}/{nonPlayableLeaderBiome.leatherToJoin}"));
        if (nonPlayableLeaderBiome.mountsToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"mounts\">[{nonPlayableLeaderBiome.mountsToJoin}]", leader != null && leader.mountsAmount >= nonPlayableLeaderBiome.mountsToJoin, $"{leader?.mountsAmount ?? 0}/{nonPlayableLeaderBiome.mountsToJoin}"));
        if (nonPlayableLeaderBiome.timberToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"timber\">[{nonPlayableLeaderBiome.timberToJoin}]", leader != null && leader.timberAmount >= nonPlayableLeaderBiome.timberToJoin, $"{leader?.timberAmount ?? 0}/{nonPlayableLeaderBiome.timberToJoin}"));
        if (nonPlayableLeaderBiome.ironToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"iron\">[{nonPlayableLeaderBiome.ironToJoin}]", leader != null && leader.ironAmount >= nonPlayableLeaderBiome.ironToJoin, $"{leader?.ironAmount ?? 0}/{nonPlayableLeaderBiome.ironToJoin}"));
        if (nonPlayableLeaderBiome.steelToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"steel\">[{nonPlayableLeaderBiome.steelToJoin}]", leader != null && leader.steelAmount >= nonPlayableLeaderBiome.steelToJoin, $"{leader?.steelAmount ?? 0}/{nonPlayableLeaderBiome.steelToJoin}"));
        if (nonPlayableLeaderBiome.mithrilToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"mithril\">[{nonPlayableLeaderBiome.mithrilToJoin}]", leader != null && leader.mithrilAmount >= nonPlayableLeaderBiome.mithrilToJoin, $"{leader?.mithrilAmount ?? 0}/{nonPlayableLeaderBiome.mithrilToJoin}"));
        if (nonPlayableLeaderBiome.goldToJoin > 0) sb.Append(FormatRequirement($"Stock <sprite name=\"gold\">[{nonPlayableLeaderBiome.goldToJoin}]", leader != null && leader.goldAmount >= nonPlayableLeaderBiome.goldToJoin, $"{leader?.goldAmount ?? 0}/{nonPlayableLeaderBiome.goldToJoin}"));

        IEnumerable<Character> livingCharacters = GetLivingCharacters(leader);
        int bestCommander = livingCharacters.Select(x => x.GetCommander()).DefaultIfEmpty(0).Max();
        int bestAgent = livingCharacters.Select(x => x.GetAgent()).DefaultIfEmpty(0).Max();
        int bestEmmissary = livingCharacters.Select(x => x.GetEmmissary()).DefaultIfEmpty(0).Max();
        int bestMage = livingCharacters.Select(x => x.GetMage()).DefaultIfEmpty(0).Max();

        if (nonPlayableLeaderBiome.commanderLevelToJoin > 0) sb.Append(FormatRequirement($"One character with <sprite name=\"commander\"> at least {nonPlayableLeaderBiome.commanderLevelToJoin}", bestCommander >= nonPlayableLeaderBiome.commanderLevelToJoin, $"{bestCommander}/{nonPlayableLeaderBiome.commanderLevelToJoin}"));
        if (nonPlayableLeaderBiome.agentLevelToJoin > 0) sb.Append(FormatRequirement($"One character with <sprite name=\"agent\"> at least {nonPlayableLeaderBiome.agentLevelToJoin}", bestAgent >= nonPlayableLeaderBiome.agentLevelToJoin, $"{bestAgent}/{nonPlayableLeaderBiome.agentLevelToJoin}"));
        if (nonPlayableLeaderBiome.emmissaryLevelToJoin > 0) sb.Append(FormatRequirement($"One character with <sprite name=\"emmissary\"> at least {nonPlayableLeaderBiome.emmissaryLevelToJoin}", bestEmmissary >= nonPlayableLeaderBiome.emmissaryLevelToJoin, $"{bestEmmissary}/{nonPlayableLeaderBiome.emmissaryLevelToJoin}"));
        if (nonPlayableLeaderBiome.mageLevelToJoin > 0) sb.Append(FormatRequirement($"One character with <sprite name=\"mage\"> at least {nonPlayableLeaderBiome.mageLevelToJoin}", bestMage >= nonPlayableLeaderBiome.mageLevelToJoin, $"{bestMage}/{nonPlayableLeaderBiome.mageLevelToJoin}"));

        int commandersCount = livingCharacters.Count(x => x.GetCommander() > 0);
        int agentsCount = livingCharacters.Count(x => x.GetAgent() > 0);
        int emmissariesCount = livingCharacters.Count(x => x.GetEmmissary() > 0);
        int magesCount = livingCharacters.Count(x => x.GetMage() > 0);

        if (nonPlayableLeaderBiome.commandersToJoin > 0) sb.Append(FormatRequirement($"Hire {nonPlayableLeaderBiome.commandersToJoin} <sprite name=\"commander\"> characters", commandersCount >= nonPlayableLeaderBiome.commandersToJoin, $"{commandersCount}/{nonPlayableLeaderBiome.commandersToJoin}"));
        if (nonPlayableLeaderBiome.agentsToJoin > 0) sb.Append(FormatRequirement($"Hire {nonPlayableLeaderBiome.agentsToJoin} <sprite name=\"agent\"> characters", agentsCount >= nonPlayableLeaderBiome.agentsToJoin, $"{agentsCount}/{nonPlayableLeaderBiome.agentsToJoin}"));
        if (nonPlayableLeaderBiome.emmissarysToJoin > 0) sb.Append(FormatRequirement($"Hire {nonPlayableLeaderBiome.emmissarysToJoin} <sprite name=\"emmissary\"> characters", emmissariesCount >= nonPlayableLeaderBiome.emmissarysToJoin, $"{emmissariesCount}/{nonPlayableLeaderBiome.emmissarysToJoin}"));
        if (nonPlayableLeaderBiome.magesToJoin > 0) sb.Append(FormatRequirement($"Hire {nonPlayableLeaderBiome.magesToJoin} <sprite name=\"mage\"> characters", magesCount >= nonPlayableLeaderBiome.magesToJoin, $"{magesCount}/{nonPlayableLeaderBiome.magesToJoin}"));

        IEnumerable<Army> armies = livingCharacters.Where(x => x.IsArmyCommander() && x.GetArmy() != null).Select(x => x.GetArmy());
        int armyCount = armies.Count();
        int ma = armies.Sum(x => x.ma);
        int ar = armies.Sum(x => x.ar);
        int li = armies.Sum(x => x.li);
        int hi = armies.Sum(x => x.hi);
        int lc = armies.Sum(x => x.lc);
        int hc = armies.Sum(x => x.hc);
        int ca = armies.Sum(x => x.ca);
        int ws = armies.Sum(x => x.ws);

        if (nonPlayableLeaderBiome.armiesToJoin > 0) sb.Append(FormatRequirement($"Control {nonPlayableLeaderBiome.armiesToJoin} armies", armyCount >= nonPlayableLeaderBiome.armiesToJoin, $"{armyCount}/{nonPlayableLeaderBiome.armiesToJoin}"));
        if (nonPlayableLeaderBiome.maSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"ma\"> totaling {nonPlayableLeaderBiome.maSizeToJoin}", ma >= nonPlayableLeaderBiome.maSizeToJoin, $"{ma}/{nonPlayableLeaderBiome.maSizeToJoin}"));
        if (nonPlayableLeaderBiome.arSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"ar\"> totaling {nonPlayableLeaderBiome.arSizeToJoin}", ar >= nonPlayableLeaderBiome.arSizeToJoin, $"{ar}/{nonPlayableLeaderBiome.arSizeToJoin}"));
        if (nonPlayableLeaderBiome.liSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"li\"> totaling {nonPlayableLeaderBiome.liSizeToJoin}", li >= nonPlayableLeaderBiome.liSizeToJoin, $"{li}/{nonPlayableLeaderBiome.liSizeToJoin}"));
        if (nonPlayableLeaderBiome.hiSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"hi\"> totaling {nonPlayableLeaderBiome.hiSizeToJoin}", hi >= nonPlayableLeaderBiome.hiSizeToJoin, $"{hi}/{nonPlayableLeaderBiome.hiSizeToJoin}"));
        if (nonPlayableLeaderBiome.lcSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"lc\"> totaling {nonPlayableLeaderBiome.lcSizeToJoin}", lc >= nonPlayableLeaderBiome.lcSizeToJoin, $"{lc}/{nonPlayableLeaderBiome.lcSizeToJoin}"));
        if (nonPlayableLeaderBiome.hcSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"hc\"> totaling {nonPlayableLeaderBiome.hcSizeToJoin}", hc >= nonPlayableLeaderBiome.hcSizeToJoin, $"{hc}/{nonPlayableLeaderBiome.hcSizeToJoin}"));
        if (nonPlayableLeaderBiome.caSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"ca\"> totaling {nonPlayableLeaderBiome.caSizeToJoin}", ca >= nonPlayableLeaderBiome.caSizeToJoin, $"{ca}/{nonPlayableLeaderBiome.caSizeToJoin}"));
        if (nonPlayableLeaderBiome.wsSizeToJoin > 0) sb.Append(FormatRequirement($"Field <sprite name=\"ws\"> totaling {nonPlayableLeaderBiome.wsSizeToJoin}", ws >= nonPlayableLeaderBiome.wsSizeToJoin, $"{ws}/{nonPlayableLeaderBiome.wsSizeToJoin}"));

        if (nonPlayableLeaderBiome.actionsAnywhere != null && nonPlayableLeaderBiome.actionsAnywhere.Count > 0)
        {
            sb.Append(FormatRequirement($"Execute: {string.Join(", ", nonPlayableLeaderBiome.actionsAnywhere)} (any location)", HasCompletedAnywhereActions(leader)));
        }

        if (nonPlayableLeaderBiome.actionsAtCapital != null && nonPlayableLeaderBiome.actionsAtCapital.Count > 0)
        {
            string capitalName = string.IsNullOrWhiteSpace(nonPlayableLeaderBiome.startingCityName) ? "the capital" : nonPlayableLeaderBiome.startingCityName;
            sb.Append(FormatRequirement($"Execute at {capitalName}: {string.Join(", ", nonPlayableLeaderBiome.actionsAtCapital)}", HasCompletedCapitalActions(leader)));
        }

        sb.Append("All requirements persist across turns once completed. Send an aligned emissary to the capital and issue State Allegiance to pledge your alliance.");

        return sb.ToString();
    }

    new public void NewTurn()
    {
        base.NewTurn();
    }
}
