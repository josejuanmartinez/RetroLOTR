using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

public class Character : MonoBehaviour
{
    public static int MAX_RELEVANT_HEXES = Game.MAX_CHARACTERS + Game.MAX_ARTIFACTS + Game.MAX_PCS;
    public const int MAX_SKILL_LEVEL = 10;
    public const int MAX_ARTIFACTS = 10;

    [Header("Metadata")]
    public bool startingCharacter;

    [Header("Given name")]
    public string characterName;
    
    [Header("Allegiance")]
    public AlignmentEnum alignment;
    
    [Header("Owner")]
    public Leader owner;
    
    [Header("Current placement")]
    public Hex hex;

    [Header("Current character stats")]
    [SerializeField] int commander = 0;
    [SerializeField] int agent = 0;
    [SerializeField] int emmissary = 0;
    [SerializeField] int mage = 0;
    public int health = 100;
    public int moved = 0;
    public bool killed;

    [Header("Turn data")]
    public bool hasActionedThisTurn;
    public string lastPlayedActionClassNameThisTurn;
    public string lastPlayedActionNameThisTurn;
    public string lastPlayedCardSpriteNameThisTurn;
    public bool isEmbarked;
    public List<Hex> reachableHexes = new();
    public List<Hex> relevantHexes = new();

    [Header("Spionage")]
    public List<Leader> doubledBy = new();
    private Dictionary<Leader, int> doubledByTurns = new();

    [Header("Artifacts")]
    public List<Artifact> artifacts = new();

    [Header("Army")]
    [SerializeField] private Army army = null;

    [Header("AI")]
    public bool isPlayerControlled = true;

    [Header("Army")]
    public RacesEnum race = RacesEnum.Common;
    public SexEnum sex = SexEnum.Male;

    [Header("Statuses")]
    public List<StatusEffectEnum> statusEffects = new();
    private Dictionary<StatusEffectEnum, int> statusEffectTurns = new();
    private bool burningForestTroopLossPending;
    private bool poisonedFearTriggered;
    private string temporaryActionDifficultyReductionClassName;
    private int temporaryActionDifficultyReductionValue;
    private int temporaryActionDifficultyReductionTurns;
    private Hex temporaryActionDifficultyReductionHex;

    private BiomeConfig characterBiome;

    private Colors colors;
    private bool awaken = false;

    public struct StatusSnapshot
    {
        public Dictionary<StatusEffectEnum, int> statusEffectTurns;
        public int moved;
        public bool hasActionedThisTurn;
        public bool isEmbarked;
        public bool burningForestTroopLossPending;
        public bool poisonedFearTriggered;
        public string temporaryActionDifficultyReductionClassName;
        public int temporaryActionDifficultyReductionValue;
        public int temporaryActionDifficultyReductionTurns;
        public Hex temporaryActionDifficultyReductionHex;
    }

    void Awake()
    {
        army = null;
        doubledBy = new();
        doubledByTurns = new();
        reachableHexes = new();
        statusEffects = new();
        InitializeStatusEffects();
        burningForestTroopLossPending = false;
        poisonedFearTriggered = false;
        temporaryActionDifficultyReductionClassName = null;
        temporaryActionDifficultyReductionValue = 0;
        temporaryActionDifficultyReductionTurns = 0;
        temporaryActionDifficultyReductionHex = null;
        killed = false;
        lastPlayedActionClassNameThisTurn = null;
        lastPlayedActionNameThisTurn = null;
        lastPlayedCardSpriteNameThisTurn = null;
        awaken = true;
        colors = FindFirstObjectByType<Colors>();
    }

    private void InitializeStatusEffects()
    {
        statusEffectTurns = new Dictionary<StatusEffectEnum, int>();
        foreach (StatusEffectEnum effect in Enum.GetValues(typeof(StatusEffectEnum)))
        {
            statusEffectTurns[effect] = 0;
        }
    }
    public void InitializeFromBiome(Leader leader, Hex hex, BiomeConfig characterBiome, bool showSpawnMessage = true)
    {
        if (!awaken) Awake();
        this.characterBiome = characterBiome;
        Initialize(
            leader, 
            characterBiome.alignment, 
            hex, 
            characterBiome.characterName, 
            characterBiome.commander, 
            characterBiome.agent, 
            characterBiome.emmissary, 
            characterBiome.mage, 
            characterBiome.race, 
            characterBiome.sex,
            characterBiome.artifacts,
            characterBiome.startingArmySize,
            characterBiome.preferedTroopType,
            characterBiome.startingWarships,
            showSpawnMessage);
    }

    public void Initialize(
        Leader owner, 
        AlignmentEnum alignment, 
        Hex hex, 
        string characterName,
        int commander,
        int agent,
        int emmissary,
        int mage,
        RacesEnum race,
        SexEnum sex,
        List<Artifact> artifacts,
        int startingArmySize = 0,
        TroopsTypeEnum preferedTroopType = TroopsTypeEnum.ma,
        int startingWarships = 0,
        bool showSpawnMessage = true)
    {
        if (!awaken) Awake();

        if (showSpawnMessage)
        {
            string ownerName = "";
            if (owner != null && owner.characterName != null) ownerName = owner.characterName;
            if (ownerName.Trim() == "") ownerName = "themselves";
            MessageDisplayNoUI.ShowMessage(hex, this, $"Character {characterName} starts serving {ownerName}", Color.green);
        }

        this.characterName = characterName;
        this.commander = Mathf.Clamp(commander, 0, MAX_SKILL_LEVEL);
        this.agent = Mathf.Clamp(agent, 0, MAX_SKILL_LEVEL);
        this.emmissary = Mathf.Clamp(emmissary, 0, MAX_SKILL_LEVEL);
        this.mage = Mathf.Clamp(mage, 0, MAX_SKILL_LEVEL);
        this.alignment = alignment;
        this.race = race;
        this.sex = sex;
        this.startingCharacter = true;
        this.artifacts = artifacts;

        owner.GetOwner().controlledCharacters.Add(this);
        this.owner = owner.GetOwner();
        hasActionedThisTurn = false;
        moved = 0;
        isEmbarked = false;
        army = null;
        this.hex = hex;
        hex.characters.Add(this);

        if (startingArmySize > 0 || startingWarships > 0) CreateArmy(preferedTroopType, startingArmySize, startingCharacter, startingWarships);
        RefreshArtifactPcVisibilityForHex(this.hex);
    }

    public AlignmentEnum GetAlignment()
    {
        return owner != null? owner.GetAlignment() : alignment;
    }

    public async Task Pass()
    {
        CharacterAction action = FindFirstObjectByType<ActionsManager>().DEFAULT;
        action.Initialize(this);
        await action.Execute();
    }

    public void Halt(int turns = 1)
    {
        int clampedTurns = Mathf.Max(1, turns);
        ApplyStatusEffect(StatusEffectEnum.Halted, clampedTurns);
        if (clampedTurns == 1)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} halted (reduced movement) for next turn!", Color.red);
        }
        else
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} halted (reduced movement) for {clampedTurns} turns!", Color.red);
        }
    }

    public void Encourage(int turns = 1)
    {
        ApplyStatusEffect(StatusEffectEnum.Encouraged, turns);
    }

    public bool IsEncouraged()
    {
        return HasStatusEffect(StatusEffectEnum.Encouraged);
    }

    public void ClearEncouraged()
    {
        ClearStatusEffect(StatusEffectEnum.Encouraged);
    }

    public bool IsRefusingDuels()
    {
        return HasStatusEffect(StatusEffectEnum.RefusingDuels);
    }

    public void RefuseDuels(int turns = 1)
    {
        ApplyStatusEffect(StatusEffectEnum.RefusingDuels, turns);
    }

    public bool HasDuelSupremacy()
    {
        return HasStatusEffect(StatusEffectEnum.DuelSupremacy);
    }

    public void GainDuelSupremacy(int turns = 1)
    {
        ApplyStatusEffect(StatusEffectEnum.DuelSupremacy, turns);
    }

    public bool IsHidden()
    {
        return HasStatusEffect(StatusEffectEnum.Hidden);
    }

    public void Hide(int turns = 1)
    {
        ApplyStatusEffect(StatusEffectEnum.Hidden, turns);
    }

    public bool HasStatusEffect(StatusEffectEnum effect)
    {
        return GetStatusEffectTurns(effect) > 0;
    }

    public int GetStatusEffectTurns(StatusEffectEnum effect)
    {
        if (statusEffectTurns == null || statusEffectTurns.Count == 0)
        {
            InitializeStatusEffects();
        }

        return statusEffectTurns.TryGetValue(effect, out int turns) ? Mathf.Max(0, turns) : 0;
    }

    public void ApplyStatusEffect(StatusEffectEnum effect, int turns = 1)
    {
        if (statusEffectTurns == null || statusEffectTurns.Count == 0)
        {
            InitializeStatusEffects();
        }

        turns = GetNormalizedStatusTurns(effect, turns);
        if (IsEncouraged() && IsBlockedByEncouraged(effect)) return;
        if (effect == StatusEffectEnum.Haste && HasStatusEffect(StatusEffectEnum.Frozen)) return;

        int current = GetStatusEffectTurns(effect);
        statusEffectTurns[effect] = Mathf.Max(current, turns);

        statusEffects ??= new List<StatusEffectEnum>();
        if (!statusEffects.Contains(effect)) statusEffects.Add(effect);

        if (effect == StatusEffectEnum.Burning)
        {
            ClearStatusEffect(StatusEffectEnum.Frozen);
            if (turns > current) burningForestTroopLossPending = true;
        }
        else if (effect == StatusEffectEnum.Frozen)
        {
            ClearStatusEffect(StatusEffectEnum.Haste);
            ClearStatusEffect(StatusEffectEnum.Burning);
        }
        else if (effect == StatusEffectEnum.Poisoned && turns > current)
        {
            poisonedFearTriggered = false;
        }
        else if (effect == StatusEffectEnum.Encouraged)
        {
            ClearSuppressedStatusesIfEncouraged();
        }
    }

    public void ClearStatusEffect(StatusEffectEnum effect)
    {
        if (statusEffectTurns == null || statusEffectTurns.Count == 0)
        {
            InitializeStatusEffects();
        }

        statusEffectTurns[effect] = 0;
        statusEffects?.Remove(effect);
        ResetStatusSpecialState(effect);
    }

    public void NewTurn()
    {
        Game game = FindFirstObjectByType<Game>();
        Leader player = game != null ? game.player : null;

        if (health < 100)
        {
            health = Mathf.Min(100, health + 5);
        }
        if (HasStatusEffect(StatusEffectEnum.Hope) && health < 100)
        {
            health = Mathf.Min(100, health + 5);
        }

        ApplyArtifactPassiveEffects();

        ClearSuppressedStatusesIfEncouraged();

        if (!killed)
        {
            ProcessMorgulTouch();
        }
        if (!killed)
        {
            ProcessBurning();
        }
        if (!killed)
        {
            ProcessPoisoned();
        }
        if (killed) return;

        int blockedTurns = GetStatusEffectTurns(StatusEffectEnum.Blocked);
        bool blocked = blockedTurns > 0;
        if (blocked && GetOwner() == player) MessageDisplayNoUI.ShowMessage(hex, this, "Blocked", Color.red);

        bool halted = HasStatusEffect(StatusEffectEnum.Halted);
        if (halted && GetOwner() == player) MessageDisplayNoUI.ShowMessage(hex, this, "Halted", Color.yellow);

        if (blocked)
        {
            moved = GetMaxMovement();
            hasActionedThisTurn = true;
            lastPlayedActionClassNameThisTurn = null;
            lastPlayedActionNameThisTurn = null;
            lastPlayedCardSpriteNameThisTurn = null;
        }
        else if (halted)
        {
            int maxMovement = GetMaxMovement();
            int haltedPenalty = Mathf.Max(1, Mathf.FloorToInt(maxMovement * 0.5f));
            moved = Mathf.Min(maxMovement, haltedPenalty);
            hasActionedThisTurn = false;
            lastPlayedActionClassNameThisTurn = null;
            lastPlayedActionNameThisTurn = null;
            lastPlayedCardSpriteNameThisTurn = null;
        }
        else
        {
            moved = 0;
            hasActionedThisTurn = false;
            lastPlayedActionClassNameThisTurn = null;
            lastPlayedActionNameThisTurn = null;
            lastPlayedCardSpriteNameThisTurn = null;
        }

        if (!blocked && HasStatusEffect(StatusEffectEnum.Fear) && !IsArmyCommander() && UnityEngine.Random.Range(0, 2) == 0)
        {
            hasActionedThisTurn = true;
            if (GetOwner() == player)
            {
                MessageDisplayNoUI.ShowMessage(hex, this, "Fear prevents action", Color.red);
            }
        }

        if (blockedTurns > 0)
        {
            blockedTurns = Mathf.Max(0, blockedTurns - 1);
            statusEffectTurns[StatusEffectEnum.Blocked] = blockedTurns;
            if (blockedTurns == 0)
            {
                statusEffects?.Remove(StatusEffectEnum.Blocked);
                ResetStatusSpecialState(StatusEffectEnum.Blocked);
            }
        }

        if (HasStatusEffect(StatusEffectEnum.Encouraged) && GetOwner() == player)
        {
            MessageDisplayNoUI.ShowMessage(hex, this,  "Encouraged", Color.green);
        }

        if (HasStatusEffect(StatusEffectEnum.RefusingDuels) && GetOwner() == player)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, "Refusing duels", Color.yellow);
        }
        if (HasStatusEffect(StatusEffectEnum.DuelSupremacy) && GetOwner() == player)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, "Wins challenged duels", Color.cyan);
        }
        if (!blocked && halted && GetOwner() == player)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, "Movement reduced", Color.yellow);
        }

        foreach (StatusEffectEnum effect in Enum.GetValues(typeof(StatusEffectEnum)))
        {
            if (effect == StatusEffectEnum.Blocked) continue;
            int turns = GetStatusEffectTurns(effect);
            if (turns <= 0) continue;

            turns = Mathf.Max(0, turns - 1);
            statusEffectTurns[effect] = turns;
            if (turns == 0)
            {
                statusEffects?.Remove(effect);
                ResetStatusSpecialState(effect);
            }
        }
        TickDoubledByTurns();
        StoreReachableHexes();
        StoreRelevantHexes();
        RefreshSelectedCharacterIconIfSelected();
        RefreshActionsIfSelected();
    }

    public StatusSnapshot CaptureStatusSnapshot()
    {
        if (statusEffectTurns == null || statusEffectTurns.Count == 0)
        {
            InitializeStatusEffects();
        }

        return new StatusSnapshot
        {
            statusEffectTurns = new Dictionary<StatusEffectEnum, int>(statusEffectTurns),
            moved = moved,
            hasActionedThisTurn = hasActionedThisTurn,
            isEmbarked = isEmbarked,
            burningForestTroopLossPending = burningForestTroopLossPending,
            poisonedFearTriggered = poisonedFearTriggered,
            temporaryActionDifficultyReductionClassName = temporaryActionDifficultyReductionClassName,
            temporaryActionDifficultyReductionValue = temporaryActionDifficultyReductionValue,
            temporaryActionDifficultyReductionTurns = temporaryActionDifficultyReductionTurns,
            temporaryActionDifficultyReductionHex = temporaryActionDifficultyReductionHex
        };
    }

    public void RestoreStatusSnapshot(StatusSnapshot snapshot)
    {
        InitializeStatusEffects();
        if (snapshot.statusEffectTurns != null)
        {
            foreach (var kv in snapshot.statusEffectTurns)
            {
                statusEffectTurns[kv.Key] = Mathf.Max(0, kv.Value);
            }
        }

        statusEffects = statusEffectTurns.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        moved = snapshot.moved;
        hasActionedThisTurn = snapshot.hasActionedThisTurn;
        isEmbarked = snapshot.isEmbarked;
        burningForestTroopLossPending = snapshot.burningForestTroopLossPending;
        poisonedFearTriggered = snapshot.poisonedFearTriggered;
        temporaryActionDifficultyReductionClassName = snapshot.temporaryActionDifficultyReductionClassName;
        temporaryActionDifficultyReductionValue = snapshot.temporaryActionDifficultyReductionValue;
        temporaryActionDifficultyReductionTurns = snapshot.temporaryActionDifficultyReductionTurns;
        temporaryActionDifficultyReductionHex = snapshot.temporaryActionDifficultyReductionHex;
    }

    public void DisbandArmy(bool showMessage = true)
    {
        if (army == null || !IsArmyCommander()) return;

        if (hex != null)
        {
            if (hex.armies.Contains(army)) hex.armies.Remove(army);
            hex.RedrawCharacters();
            hex.RedrawArmies();
        }

        if (showMessage)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} disbanded their army", Color.yellow);
        }

        army = null;
        RefreshSelectedCharacterIconIfSelected();
    }

    public virtual Leader GetOwner()
    {
        if(killed) return null;
        if (!owner && this is Leader) return this as Leader;
        
        return owner;
    }

    public string GetHoverText(bool withAlignment, bool withCharInfo, bool withLevels, bool withArmy, bool withColor, bool withHealth = true)
    {
        List<string> result = new() { };
        if (withColor) result.Add($"<color={colors.GetHexColorByName(alignment.ToString())}>");
        if(withAlignment) result.Add($"<sprite name=\"{alignment}\">");
        result.Add($"{characterName}");
        if (withHealth) result.Add(GetHealthHoverText());
        if (withCharInfo)
        {
            if (commander > 0) result.Add($"<sprite name=\"commander\">{(withLevels ? "[" + GetCommander().ToString() + "]" : "")}");
            if (agent > 0) result.Add($"<sprite name=\"agent\">{(withLevels ? "[" + GetAgent().ToString() + "]" : "")}");
            if (emmissary > 0) result.Add($"<sprite name=\"emmissary\">{(withLevels ? "[" + GetEmmissary().ToString() + "]" : "")}");
            if (mage > 0) result.Add($"<sprite name=\"mage\">{(withLevels ? "[" + GetMage().ToString() + "]" : "")}");
        }

        if (withArmy && GetArmy() != null) result.Add(GetArmy().GetHoverText());
        if (withColor) result.Add("</color>");
        return string.Join("", result);
    }

    public string GetHealthHoverText()
    {
        string healthColor = "#ff4d4d";
        string noHealthColor = "#000000";
        StringBuilder sb = new(" ");
        const int bars = 4;
        for(int i=0;i<bars;i++)
        {
            string color = noHealthColor;
            if(health >= Mathf.FloorToInt(100 / (bars-i))) color = healthColor;
            sb.Append($"<color={color}>|</color>");  
        }

        return sb.ToString();
    }

    public MovementType GetMovementType()
    {
        return army == null ? MovementType.Character : army.GetMovementType();
    }

    public int GetMaxMovement()
    {
        if (HasStatusEffect(StatusEffectEnum.Frozen))
        {
            return 0;
        }

        MovementType movementType = army == null ? MovementType.Character : army.GetMovementType();
        bool isInWater = hex != null && hex.IsWaterTerrain();
        int baseMovement = movementType switch
        {
            MovementType.ArmyCommander => AdjustMovementByRace(FindFirstObjectByType<Game>().armyMovement, isInWater),
            MovementType.ArmyCommanderCavalryOnly => AdjustMovementByRace(FindFirstObjectByType<Game>().cavalryMovement, isInWater),
            _ => AdjustMovementByRace(FindFirstObjectByType<Game>().characterMovement, isInWater)
        };

        if (HasStatusEffect(StatusEffectEnum.Haste))
        {
            baseMovement += 2;
        }

        return Mathf.Max(0, baseMovement);
    }

    private int AdjustMovementByRace(int baseMovement, bool isInWater)
    {
        if (isInWater) return baseMovement;

        return race switch
        {
            RacesEnum.Hobbit => Mathf.Min(baseMovement, 3),
            RacesEnum.Dwarf => Mathf.Min(baseMovement, 4),
            _ => baseMovement
        };
    }

    public bool IsArmyCommander()
    {
        return army != null && army.commander == this && !killed && army.GetSize() > 0 && !army.killed;
    }

    public int GetMovementLeft()
    {
        return Mathf.Max(0, GetMaxMovement() - moved);
    }

    public void CreateArmy(TroopsTypeEnum troopsType, int amount, bool startingArmy, int ws = 0)
    {
        army = new Army(this, troopsType, amount, startingArmy, ws);
        hex.armies.Add(army);

        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} just hired an army of <sprite name=\"{troopsType.ToString().ToLower()}\"/>[{amount}]", Color.green);
        hex.RedrawCharacters();
        hex.RedrawArmies();
        RefreshSelectedCharacterIconIfSelected();
    }

    public Army GetArmy()
    {
        if (!IsArmyCommander()) return null;
        return army;
    }

    virtual public void Killed(Leader killedBy, bool onlyMark=false)
    {
        bool redrawArmies = false;
        if (IsArmyCommander() && !army.killed) {
            army.Killed(killedBy, onlyMark);
            redrawArmies = true;
        }
        if(!onlyMark)
        {
            if(GetOwner().controlledCharacters.Contains(this)) GetOwner().controlledCharacters.Remove(this);
            if(hex.characters.Contains(this)) hex.characters.Remove(this);
            RefreshArtifactPcVisibilityForHex(hex);
        }
        health = 0;
        killed = true;
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} eliminated", Color.red);        
        hex.RedrawCharacters();
        RefreshSelectedCharacterIconIfSelected();
        if(redrawArmies) hex.RedrawArmies();
        Leader owner = GetOwner();
        if (owner != null) CharacterIcons.RefreshForHumanPlayerOf(owner);
    }

    public void RefreshSelectedCharacterIconIfSelected()
    {        
        Game game = FindAnyObjectByType<Game>();
        Board board = FindAnyObjectByType<Board>();
        if(game.IsPlayerCurrentlyPlaying() && board.selectedCharacter == this) FindFirstObjectByType<SelectedCharacterIcon>().Refresh(this);
    }
    public void RefreshActionsIfSelected()
    {        
        Game game = FindAnyObjectByType<Game>();
        Board board = FindAnyObjectByType<Board>();
        if(game.IsPlayerCurrentlyPlaying() && board.selectedCharacter == this) FindFirstObjectByType<ActionsManager>().Refresh(this);
    }

    public void Wounded(Leader woundedBy, int damage)
    {
        health -= damage;
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} wounded by {damage}", Color.red);
        Sounds.Instance?.PlayVoicePain(this);
        RefreshSelectedCharacterIconIfSelected();
        CharacterIcons.RefreshForHumanPlayerCharacter(this);
        if (health < 1) Killed(woundedBy);
    }

    public void ApplyOppositeAlignmentArtifactPenalty(Artifact artifact)
    {
        if (artifact == null || !artifact.ShouldApplyAlignmentPenalty(GetAlignment())) return;
        int damage = Artifact.OppositeAlignmentHealthPenalty;
        health = Mathf.Max(0, health - damage);
        MessageDisplayNoUI.ShowMessage(hex, this, $"-{damage} <sprite name=\"health\">", Color.red);
        Sounds.Instance?.PlayVoicePain(this);
        RefreshSelectedCharacterIconIfSelected();
        CharacterIcons.RefreshForHumanPlayerCharacter(this);
        if (health < 1) Killed(null);
    }

    public int GetArtifactActionDifficultyReduction(string actionClassName)
    {
        if (artifacts == null || artifacts.Count == 0) return 0;
        return artifacts.Sum(a => a != null ? a.GetActionDifficultyReduction(actionClassName) : 0);
    }

    public int GetTemporaryActionDifficultyReduction(string actionClassName, Hex currentHex)
    {
        if (temporaryActionDifficultyReductionTurns <= 0) return 0;
        if (string.IsNullOrWhiteSpace(temporaryActionDifficultyReductionClassName)) return 0;
        if (!string.Equals(temporaryActionDifficultyReductionClassName, actionClassName, StringComparison.OrdinalIgnoreCase)) return 0;
        if (temporaryActionDifficultyReductionHex != null && temporaryActionDifficultyReductionHex != currentHex) return 0;
        return Math.Max(0, temporaryActionDifficultyReductionValue);
    }

    public void GrantTemporaryActionDifficultyReduction(string actionClassName, int value, int turns, Hex currentHex)
    {
        temporaryActionDifficultyReductionClassName = actionClassName;
        temporaryActionDifficultyReductionValue = Math.Max(0, value);
        temporaryActionDifficultyReductionTurns = Math.Max(1, turns);
        temporaryActionDifficultyReductionHex = currentHex;
    }

    public void ConsumeTemporaryActionDifficultyReduction(string actionClassName, Hex currentHex)
    {
        if (GetTemporaryActionDifficultyReduction(actionClassName, currentHex) <= 0) return;
        temporaryActionDifficultyReductionClassName = null;
        temporaryActionDifficultyReductionValue = 0;
        temporaryActionDifficultyReductionTurns = 0;
        temporaryActionDifficultyReductionHex = null;
    }

    public bool IsImmuneToNegativeEnvironmentalCards()
    {
        if (artifacts == null || artifacts.Count == 0) return false;
        return artifacts.Any(a => a != null && a.GrantsEnvironmentalImmunity());
    }

    public int GetArmySuccessfulAttackBurningChancePercent()
    {
        if (artifacts == null || artifacts.Count == 0) return 0;
        return artifacts.Sum(a => a != null ? a.GetArmySuccessfulAttackBurningChancePercent() : 0);
    }

    public bool HidesOccupiedPcWithArtifact()
    {
        if (artifacts == null || artifacts.Count == 0) return false;
        return artifacts.Any(a => a != null && a.HidesOccupiedPcWhilePresent());
    }

    public static void RefreshArtifactPcVisibilityForHex(Hex hex)
    {
        if (hex == null) return;

        PC pc = hex.GetPCData();
        if (pc == null) return;

        bool shouldHide = false;
        if (hex.characters != null)
        {
            for (int i = 0; i < hex.characters.Count; i++)
            {
                Character occupant = hex.characters[i];
                if (occupant != null && !occupant.killed && occupant.HidesOccupiedPcWithArtifact())
                {
                    shouldHide = true;
                    break;
                }
            }
        }

        if (pc.artifactOccupancyHidden == shouldHide) return;

        pc.SetArtifactOccupancyHidden(shouldHide);
        hex.RefreshVisibilityRendering();
    }

    private void TickDoubledByTurns()
    {
        if (doubledByTurns == null || doubledByTurns.Count == 0) return;

        List<Leader> keys = doubledByTurns.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            Leader spyLeader = keys[i];
            int turns = Mathf.Max(0, doubledByTurns[spyLeader] - 1);
            if (turns <= 0)
            {
                doubledByTurns.Remove(spyLeader);
                doubledBy.Remove(spyLeader);
            }
            else
            {
                doubledByTurns[spyLeader] = turns;
            }
        }
    }

    public void Doubled(Leader doubledBy, int turns = -1)
    {
        if (doubledBy == null) return;
        if (!this.doubledBy.Contains(doubledBy)) this.doubledBy.Add(doubledBy);

        if (turns > 0)
        {
            if (doubledByTurns.TryGetValue(doubledBy, out int existing))
            {
                doubledByTurns[doubledBy] = Mathf.Max(existing, turns);
            }
            else
            {
                doubledByTurns[doubledBy] = turns;
            }
        }
        else
        {
            doubledByTurns.Remove(doubledBy);
        }

        MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} doubled by {doubledBy.characterName}", Color.green);
    }
    public void Undouble(Leader doubledBy)
    {
        this.doubledBy.Remove(doubledBy);
        doubledByTurns.Remove(doubledBy);
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} undoubled by {doubledBy.characterName}", Color.green);
    }

    public int GetBaseCommander()
    {
        return commander;
    }

    public int GetBaseAgent()
    {
        return agent;
    }

    public int GetBaseEmmissary()
    {
        return emmissary;
    }

    public int GetBaseMage()
    {
        return mage;
    }

    public int GetCommander()
    {
        int total = commander + artifacts.FindAll(x => x.commanderBonus > 0).Sum(x => x.commanderBonus);
        total = ApplyDespairPenalty(total);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetAgent()
    {
        int total = agent + artifacts.FindAll(x => x.agentBonus > 0).Sum(x => x.agentBonus);
        total = ApplyDespairPenalty(total);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetEmmissary()
    {
        int total = emmissary + artifacts.FindAll(x => x.emmissaryBonus > 0).Sum(x => x.emmissaryBonus);
        total = ApplyDespairPenalty(total);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetMage()
    {
        int total = mage + artifacts.FindAll(x => x.mageBonus > 0).Sum(x => x.mageBonus);
        if (HasStatusEffect(StatusEffectEnum.ArcaneInsight)) total += 1;
        total = ApplyDespairPenalty(total);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public void SetCommander(int level)
    {
        commander = Mathf.Clamp(level, 0, MAX_SKILL_LEVEL);
    }

    public void SetAgent(int level)
    {
        agent = Mathf.Clamp(level, 0, MAX_SKILL_LEVEL);
    }

    public void SetEmmissary(int level)
    {
        emmissary = Mathf.Clamp(level, 0, MAX_SKILL_LEVEL);
    }

    public void SetMage(int level)
    {
        mage = Mathf.Clamp(level, 0, MAX_SKILL_LEVEL);
    }


    public void AddCommander(int level)
    {
        commander = Mathf.Clamp(commander + level, 0, MAX_SKILL_LEVEL);
    }

    public void AddAgent(int level)
    {
        agent = Mathf.Clamp(agent + level, 0, MAX_SKILL_LEVEL);
    }

    public void AddEmmissary(int level)
    {
        emmissary = Mathf.Clamp(emmissary + level, 0, MAX_SKILL_LEVEL);
    }

    public void AddMage(int level)
    {
        mage = Mathf.Clamp(mage + level, 0, MAX_SKILL_LEVEL);
    }

    public void Heal(int health)
    {
        int previousHealth = this.health;
        this.health = Mathf.Min(100, this.health + Mathf.Max(0, health));
        int healedAmount = Mathf.Max(0, this.health - previousHealth);
        bool curedPoison = HasStatusEffect(StatusEffectEnum.Poisoned);
        if (curedPoison)
        {
            ClearStatusEffect(StatusEffectEnum.Poisoned);
        }

        if (healedAmount > 0)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} heals by {healedAmount}", Color.green);
        }
        if (curedPoison)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} is cured of Poison.", Color.green);
        }

        if (healedAmount > 0 || curedPoison)
        {
            RefreshSelectedCharacterIconIfSelected();
            CharacterIcons.RefreshForHumanPlayerCharacter(this);
        }
    }

    private void ApplyArtifactPassiveEffects()
    {
        if (artifacts == null || artifacts.Count == 0) return;

        for (int i = 0; i < artifacts.Count; i++)
        {
            Artifact artifact = artifacts[i];
            if (artifact == null) continue;

            if (artifact.HidesOccupiedPcWhilePresent())
            {
                RefreshArtifactPcVisibilityForHex(hex);
            }

            if (artifact.BlocksEnemyCharactersOnHex() && hex != null && hex.characters != null)
            {
                for (int j = 0; j < hex.characters.Count; j++)
                {
                    Character target = hex.characters[j];
                    if (target == null || target == this || target.killed) continue;
                    if (target.GetOwner() == GetOwner()) continue;
                    if (target.GetAlignment() == GetAlignment() && target.GetAlignment() != AlignmentEnum.neutral) continue;

                    target.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} blocks {target.characterName}.", Color.magenta);
                }
            }

            int healPerTurn = artifact.GetPassiveHealPerTurn();
            if (healPerTurn > 0)
            {
                Heal(healPerTurn);
            }

            if (artifact.RevealsHiddenEnemyPcOnOccupiedHex())
            {
                RevealHiddenEnemyPcOnCurrentHex(artifact);
            }

            int hopeChance = artifact.GetHopeChancePerTurnPercent();
            if (hopeChance > 0 && UnityEngine.Random.Range(0, 100) < hopeChance)
            {
                ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} grants Hope.", Color.green);
            }

            if (artifact.GrantsHasteAtSea()
                && hex != null
                && hex.IsWaterTerrain())
            {
                ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} grants Haste at sea.", Color.green);
            }

            int selfCleanseChance = artifact.GetSelfFearAndDespairCleanseChancePerTurnPercent();
            if (selfCleanseChance > 0 && UnityEngine.Random.Range(0, 100) < selfCleanseChance)
            {
                bool clearedFear = HasStatusEffect(StatusEffectEnum.Fear);
                bool clearedDespair = HasStatusEffect(StatusEffectEnum.Despair);
                if (clearedFear) ClearStatusEffect(StatusEffectEnum.Fear);
                if (clearedDespair) ClearStatusEffect(StatusEffectEnum.Despair);

                if (clearedFear || clearedDespair)
                {
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} lifts Fear and Despair.", Color.green);
                }
            }

            int encouragedChance = artifact.GetEncouragedChancePerTurnPercent();
            if (encouragedChance > 0 && UnityEngine.Random.Range(0, 100) < encouragedChance)
            {
                Encourage(1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} grants Courage.", Color.green);
            }

            int blockedSelfChance = artifact.GetBlockedSelfChancePerTurnPercent();
            if (blockedSelfChance > 0 && UnityEngine.Random.Range(0, 100) < blockedSelfChance)
            {
                ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} causes Sleep.", Color.yellow);
            }

            int freePeopleNonMenHaltChance = artifact.GetFreePeopleNonMenHaltChancePerTurnPercent();
            if (freePeopleNonMenHaltChance > 0
                && GetAlignment() == AlignmentEnum.freePeople
                && hex != null
                && hex.characters != null
                && UnityEngine.Random.Range(0, 100) < freePeopleNonMenHaltChance)
            {
                for (int j = 0; j < hex.characters.Count; j++)
                {
                    Character target = hex.characters[j];
                    if (target == null || target == this || target.killed) continue;
                    if (target.GetOwner() == GetOwner()) continue;
                    if (target.GetAlignment() == GetAlignment() && target.GetAlignment() != AlignmentEnum.neutral) continue;
                    if (IsManRace(target.race)) continue;

                    target.Halt(1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} halts {target.characterName}.", Color.green);
                }
            }

            int forestHiddenChance = artifact.GetForestHiddenChancePerTurnPercent();
            if (forestHiddenChance > 0
                && hex != null
                && hex.terrainType == TerrainEnum.forest
                && UnityEngine.Random.Range(0, 100) < forestHiddenChance)
            {
                Hide(1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} shrouds {characterName}.", Color.green);
            }

            int pcMoraleChance = artifact.GetAlliedPcMoraleChancePerTurnPercent();
            if (pcMoraleChance > 0
                && hex != null
                && UnityEngine.Random.Range(0, 100) < pcMoraleChance)
            {
                PC pc = hex.GetPC();
                if (pc != null && pc.owner != null)
                {
                    AlignmentEnum pcAlignment = pc.owner.GetAlignment();
                    bool alliedPc = pc.owner == GetOwner()
                        || (pcAlignment != AlignmentEnum.neutral && pcAlignment == GetAlignment());
                    if (alliedPc && pc.loyalty < 100)
                    {
                        pc.IncreaseLoyalty(5, this);
                    }
                }
            }

            int fearChance = artifact.GetHexEnemyFearChancePerTurnPercent();
            if (fearChance > 0 && UnityEngine.Random.Range(0, 100) < fearChance)
            {
                Character fearTarget = FindEnemyForArtifactStatusPulse();
                if (fearTarget != null)
                {
                    fearTarget.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} spreads Fear to {fearTarget.characterName}.", Color.magenta);
                }
            }

            int despairChance = artifact.GetHexEnemyDespairChancePerTurnPercent();
            if (despairChance > 0 && UnityEngine.Random.Range(0, 100) < despairChance)
            {
                Character despairTarget = FindEnemyForArtifactStatusPulse();
                if (despairTarget != null)
                {
                    despairTarget.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} spreads Despair to {despairTarget.characterName}.", Color.magenta);
                }
            }

            int selfDespairChance = artifact.GetSelfDespairChancePerTurnPercent();
            if (selfDespairChance > 0 && UnityEngine.Random.Range(0, 100) < selfDespairChance)
            {
                ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} brings Despair.", Color.magenta);
            }

            int randomHexRevealChance = artifact.GetRandomHexRevealChancePerTurnPercent();
            if (randomHexRevealChance > 0 && UnityEngine.Random.Range(0, 100) < randomHexRevealChance)
            {
                Leader artifactOwner = GetOwner();
                Board board = FindFirstObjectByType<Board>();
                if (artifactOwner != null && board != null)
                {
                    List<Hex> allHexes = board.GetHexes();
                    List<Hex> eligibleHexes = allHexes
                        .Where(h => h != null && !h.IsScoutedBy(artifactOwner))
                        .ToList();

                    List<Hex> source = eligibleHexes.Count > 0 ? eligibleHexes : allHexes.Where(h => h != null).ToList();
                    if (source.Count > 0)
                    {
                        Hex revealedHex = source[UnityEngine.Random.Range(0, source.Count)];
                        revealedHex.Reveal(artifactOwner);
                        revealedHex.RefreshVisibilityRendering();
                        MessageDisplayNoUI.ShowMessage(revealedHex, this, $"{artifact.artifactName} reveals a distant hex.", Color.green);
                    }
                }
            }

            int burningChance = artifact.GetHexEnemyBurningChancePerTurnPercent();
            if (burningChance > 0 && UnityEngine.Random.Range(0, 100) < burningChance)
            {
                Character burningTarget = FindEnemyForArtifactStatusPulse();
                if (burningTarget != null)
                {
                    burningTarget.ApplyStatusEffect(StatusEffectEnum.Burning, 1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} burns {burningTarget.characterName}.", Color.red);
                }
            }

            int haltChance = artifact.GetHexEnemyHaltChancePerTurnPercent();
            if (haltChance > 0 && UnityEngine.Random.Range(0, 100) < haltChance)
            {
                Character haltTarget = FindEnemyForArtifactStatusPulse();
                if (haltTarget != null)
                {
                    haltTarget.Halt(1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} halts {haltTarget.characterName}.", Color.yellow);
                }
            }

            int poisonChance = artifact.GetHexEnemyPoisonChancePerTurnPercent();
            if (poisonChance > 0 && UnityEngine.Random.Range(0, 100) < poisonChance)
            {
                Character poisonTarget = FindEnemyForArtifactStatusPulse();
                if (poisonTarget != null)
                {
                    poisonTarget.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} poisons {poisonTarget.characterName}.", Color.magenta);
                }
            }

            int mountsChance = artifact.GetMountsChancePerTurnPercent();
            if (mountsChance > 0 && UnityEngine.Random.Range(0, 100) < mountsChance)
            {
                Leader artifactOwner = GetOwner();
                if (artifactOwner != null)
                {
                    artifactOwner.AddMounts(1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} provides +1 <sprite name=\"mounts\"/>.", Color.green);
                }
            }

            int hasteChance = artifact.GetHasteChancePerTurnPercent();
            if (hasteChance > 0 && UnityEngine.Random.Range(0, 100) < hasteChance)
            {
                ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} grants Haste.", Color.green);
            }

            int goldChance = artifact.GetGoldChancePerTurnPercent();
            if (goldChance > 0 && UnityEngine.Random.Range(0, 100) < goldChance)
            {
                Leader artifactOwner = GetOwner();
                if (artifactOwner != null)
                {
                    artifactOwner.AddGold(1);
                    MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} provides +1 <sprite name=\"gold\"/>.", Color.green);
                }
            }
        }
    }

    private Character FindEnemyForArtifactStatusPulse()
    {
        if (hex == null || hex.characters == null) return null;

        return hex.characters.FirstOrDefault(ch =>
            ch != null
            && ch != this
            && !ch.killed
            && ch.GetOwner() != GetOwner()
            && (ch.GetAlignment() != GetAlignment() || ch.GetAlignment() == AlignmentEnum.neutral));
    }

    private void RevealHiddenEnemyPcOnCurrentHex(Artifact artifact)
    {
        if (artifact == null || hex == null) return;

        PC pc = hex.GetPC();
        if (pc == null || pc.owner == null) return;
        if (pc.owner == GetOwner()) return;

        AlignmentEnum ownerAlignment = pc.owner.GetAlignment();
        if (ownerAlignment == GetAlignment() && ownerAlignment != AlignmentEnum.neutral) return;
        if (!pc.isHidden) return;

        pc.SetTemporaryReveal(1);
        hex.RefreshVisibilityRendering();
        MessageDisplayNoUI.ShowMessage(hex, this, $"{artifact.artifactName} reveals {pc.pcName}.", Color.green);
    }

    private static int GetNormalizedStatusTurns(StatusEffectEnum effect, int turns)
    {
        turns = Mathf.Max(1, turns);
        return effect switch
        {
            StatusEffectEnum.Burning => Mathf.Max(3, turns),
            StatusEffectEnum.Poisoned => Mathf.Max(5, turns),
            StatusEffectEnum.MorgulTouch => Mathf.Max(7, turns),
            _ => turns
        };
    }

    private static bool IsManRace(RacesEnum race)
    {
        return race == RacesEnum.Common
            || race == RacesEnum.Dunedain
            || race == RacesEnum.Southron
            || race == RacesEnum.Easterling;
    }

    private static bool IsBlockedByEncouraged(StatusEffectEnum effect)
    {
        return effect == StatusEffectEnum.Fear
            || effect == StatusEffectEnum.Despair
            || effect == StatusEffectEnum.Halted
            || effect == StatusEffectEnum.Blocked;
    }

    private void ClearSuppressedStatusesIfEncouraged()
    {
        if (!HasStatusEffect(StatusEffectEnum.Encouraged)) return;
        ClearStatusEffect(StatusEffectEnum.Fear);
        ClearStatusEffect(StatusEffectEnum.Despair);
        ClearStatusEffect(StatusEffectEnum.Halted);
        ClearStatusEffect(StatusEffectEnum.Blocked);
    }

    private void ResetStatusSpecialState(StatusEffectEnum effect)
    {
        if (effect == StatusEffectEnum.Burning)
        {
            burningForestTroopLossPending = false;
        }
        else if (effect == StatusEffectEnum.Poisoned)
        {
            poisonedFearTriggered = false;
        }
    }

    private int ApplyDespairPenalty(int total)
    {
        if (!HasStatusEffect(StatusEffectEnum.Despair) || HasStatusEffect(StatusEffectEnum.Hope)) return total;
        if (total <= 0) return 0;
        if (total > 1) total -= 1;
        return Mathf.Clamp(total, 1, MAX_SKILL_LEVEL);
    }

    private void ProcessBurning()
    {
        if (!HasStatusEffect(StatusEffectEnum.Burning)) return;

        ApplyStatusDamage(5, "Burning");
        if (killed) return;

        if (!burningForestTroopLossPending || !IsArmyCommander() || hex == null || hex.terrainType != TerrainEnum.forest) return;
        Army commandedArmy = GetArmy();
        if (commandedArmy == null || commandedArmy.killed || commandedArmy.GetSize(true) < 1) return;

        TroopsTypeEnum? lostTroop = commandedArmy.RemoveRandomTroop();
        if (lostTroop.HasValue)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName}'s burning army loses 1 <sprite name=\"{lostTroop.Value.ToString().ToLower()}\"/> in the forest.", Color.red);
        }
        burningForestTroopLossPending = false;
    }

    private void ProcessPoisoned()
    {
        if (!HasStatusEffect(StatusEffectEnum.Poisoned)) return;

        ApplyStatusDamage(5, "Poisoned");
        if (killed) return;

        if (poisonedFearTriggered || GetStatusEffectTurns(StatusEffectEnum.Poisoned) > 3) return;
        ApplyStatusEffect(StatusEffectEnum.Fear, 1);
        poisonedFearTriggered = true;
        MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} succumbs to Poison and gains Fear.", Color.magenta);
    }

    private void ProcessMorgulTouch()
    {
        if (!HasStatusEffect(StatusEffectEnum.MorgulTouch)) return;

        health -= 10;
        MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} suffers 10 damage from Morgul Touch.", Color.magenta);
        RefreshSelectedCharacterIconIfSelected();
        CharacterIcons.RefreshForHumanPlayerCharacter(this);

        if (health > 0) return;

        Leader sauron = FindSauronLeader();
        if (this is Leader || !ConvertToNazgulServant(sauron))
        {
            Killed(this is Leader ? GetOwner() : sauron);
        }
    }

    private void ApplyStatusDamage(int damage, string sourceName)
    {
        health = Mathf.Max(0, health - Mathf.Max(0, damage));
        MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} takes {damage} damage from {sourceName}.", Color.red);
        Sounds.Instance?.PlayVoicePain(this);
        RefreshSelectedCharacterIconIfSelected();
        CharacterIcons.RefreshForHumanPlayerCharacter(this);
        if (health < 1)
        {
            Killed(this is Leader ? GetOwner() : null);
        }
    }

    private Leader FindSauronLeader()
    {
        Leader[] leaders = FindObjectsByType<Leader>(FindObjectsSortMode.None);
        for (int i = 0; i < leaders.Length; i++)
        {
            Leader leader = leaders[i];
            if (leader != null && !leader.killed && string.Equals(leader.characterName, "Sauron", StringComparison.OrdinalIgnoreCase))
            {
                return leader;
            }
        }

        return null;
    }

    private bool ConvertToNazgulServant(Leader sauron)
    {
        if (sauron == null || this is Leader) return false;

        Leader oldOwner = GetOwner();
        if (oldOwner != null && oldOwner.controlledCharacters.Contains(this))
        {
            oldOwner.controlledCharacters.Remove(this);
        }

        if (!sauron.controlledCharacters.Contains(this))
        {
            sauron.controlledCharacters.Add(this);
        }

        owner = sauron;
        alignment = sauron.GetAlignment();
        race = RacesEnum.Nazgul;
        health = 100;
        startingCharacter = false;
        isPlayerControlled = FindFirstObjectByType<Game>()?.player == sauron;
        ClearStatusEffect(StatusEffectEnum.MorgulTouch);

        hex?.RedrawCharacters();
        hex?.RedrawArmies();
        CharacterIcons.RefreshForHumanPlayerOf(oldOwner);
        CharacterIcons.RefreshForHumanPlayerOf(sauron);
        RefreshSelectedCharacterIconIfSelected();
        RefreshActionsIfSelected();
        MessageDisplayNoUI.ShowMessage(hex, this, $"{characterName} becomes a Nazgul and joins Sauron.", Color.magenta);
        return true;
    }

    public List<Artifact> GetTransferableArtifacts()
    {
        return artifacts.Where(x => x.transferable).ToList();
    }

    public void StoreReachableHexes()
    {
        reachableHexes = FindFirstObjectByType<HexPathRenderer>().FindAllHexesInRange(this);
    }

    public void StoreRelevantHexes()
    {
        Game game = FindFirstObjectByType<Game>();
        Board board = FindFirstObjectByType<Board>();
        // Pre-allocate exactly 190 elements for maximum efficiency
        List<Hex> relevantHexes = new(MAX_RELEVANT_HEXES);

        // Use direct access to source collections with index-based insertion
        // var inRangeHexes = hexPathRenderer.FindAllHexesInRange(c);

        var artifactHexes = board.hexesWithArtifacts;
        var characterHexes = board.hexesWithCharacters;
        var pcHexes = board.hexesWithPCs;

        // Add items directly to pre-sized list using index
        //for (int i = 0; i < inRangeHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
        //    relevantHexes.Add(inRangeHexes[i]);

        for (int i = 0; i < artifactHexes.Count && relevantHexes.Count < MAX_RELEVANT_HEXES; i++)
            relevantHexes.Add(artifactHexes[i]);

        for (int i = 0; i < characterHexes.Count && relevantHexes.Count < MAX_RELEVANT_HEXES; i++)
            relevantHexes.Add(characterHexes[i]);

        for (int i = 0; i < pcHexes.Count && relevantHexes.Count < MAX_RELEVANT_HEXES; i++)
            relevantHexes.Add(pcHexes[i]);

        // Fill remaining slots with null (if any)
        int remainingHexes = MAX_RELEVANT_HEXES - relevantHexes.Count;
        for (int i = 0; i < remainingHexes; i++)
            relevantHexes.Add(null);

        Assert.IsTrue(relevantHexes.Count == MAX_RELEVANT_HEXES, "Relevant hexes list size mismatch!");
        this.relevantHexes = relevantHexes;
    }
}
