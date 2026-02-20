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
    public bool isEmbarked;
    public List<Hex> reachableHexes = new();
    public List<Hex> relevantHexes = new();

    [Header("Spionage")]
    public List<Leader> doubledBy = new();

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
    [SerializeField] private bool isHalted = false;
    [SerializeField] private int encouragedTurns = -1; // 0 means this turn is still encouraged
    [SerializeField] private int refusingDuelsTurns = 0;

    private BiomeConfig characterBiome;

    private Colors colors;
    private bool awaken = false;

    public struct StatusSnapshot
    {
        public bool isHalted;
        public int encouragedTurns;
        public int refusingDuelsTurns;
        public int moved;
        public bool hasActionedThisTurn;
        public bool isEmbarked;
    }

    void Awake()
    {
        army = null;
        doubledBy = new();
        reachableHexes = new();
        killed = false;
        awaken = true;
        colors = FindFirstObjectByType<Colors>();
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

    public void Halt()
    {
        isHalted = true;
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} halted for next turn!", Color.red);
    }

    public void Encourage(int turns = 1)
    {
        encouragedTurns += turns;
    }

    public bool IsEncouraged()
    {
        return encouragedTurns > -1;
    }

    public bool IsRefusingDuels()
    {
        return refusingDuelsTurns > 0;
    }

    public void RefuseDuels(int turns = 1)
    {
        if (turns <= 0) return;
        refusingDuelsTurns = Mathf.Max(refusingDuelsTurns, turns);
    }

    public void NewTurn()
    {
        // Debug.Log($"New turn for {characterName} {(isPlayerControlled? "[PLAYER]": "[AI]")}");
        if (health < 100)
        {
            health = Mathf.Min(100, health + 5);
        }
        // STATUSES
        // HALT
        if (isHalted && GetOwner() == FindFirstObjectByType<Game>().player) MessageDisplayNoUI.ShowMessage(hex, this,  "Halted", Color.red);
        moved = isHalted ? GetMaxMovement() : 0;
        hasActionedThisTurn = isHalted;
        isHalted = false;
        // COURAGE
        encouragedTurns = Mathf.Max(encouragedTurns - 1, -1);
        if (IsEncouraged() && GetOwner() == FindFirstObjectByType<Game>().player) MessageDisplayNoUI.ShowMessage(hex, this,  "Encouraged", Color.green);
        if (refusingDuelsTurns > 0 && GetOwner() == FindFirstObjectByType<Game>().player)
        {
            MessageDisplayNoUI.ShowMessage(hex, this, "Refusing duels", Color.yellow);
        }
        refusingDuelsTurns = Mathf.Max(0, refusingDuelsTurns - 1);
        // STATUS EFFECTS (TODO)
        StoreReachableHexes();
        StoreRelevantHexes();
        RefreshSelectedCharacterIconIfSelected();
        RefreshActionsIfSelected();
    }

    public StatusSnapshot CaptureStatusSnapshot()
    {
        return new StatusSnapshot
        {
            isHalted = isHalted,
            encouragedTurns = encouragedTurns,
            refusingDuelsTurns = refusingDuelsTurns,
            moved = moved,
            hasActionedThisTurn = hasActionedThisTurn,
            isEmbarked = isEmbarked
        };
    }

    public void RestoreStatusSnapshot(StatusSnapshot snapshot)
    {
        isHalted = snapshot.isHalted;
        encouragedTurns = snapshot.encouragedTurns;
        refusingDuelsTurns = snapshot.refusingDuelsTurns;
        moved = snapshot.moved;
        hasActionedThisTurn = snapshot.hasActionedThisTurn;
        isEmbarked = snapshot.isEmbarked;
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
        MovementType movementType = army == null ? MovementType.Character : army.GetMovementType();
        bool isInWater = hex != null && hex.IsWaterTerrain();
        switch(movementType)
        {            
            case MovementType.ArmyCommander:
                return AdjustMovementByRace(FindFirstObjectByType<Game>().armyMovement, isInWater);
            case MovementType.ArmyCommanderCavalryOnly:
                return AdjustMovementByRace(FindFirstObjectByType<Game>().cavalryMovement, isInWater);
            case MovementType.Character:
            default:
                return AdjustMovementByRace(FindFirstObjectByType<Game>().characterMovement, isInWater);            
        }
        
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

    public void Doubled(Leader doubledBy)
    {
        this.doubledBy.Add(doubledBy);
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} doubled by {doubledBy.characterName}", Color.green);
    }
    public void Undouble(Leader doubledBy)
    {
        this.doubledBy.Remove(doubledBy);
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
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetAgent()
    {
        int total = agent + artifacts.FindAll(x => x.agentBonus > 0).Sum(x => x.agentBonus);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetEmmissary()
    {
        int total = emmissary + artifacts.FindAll(x => x.emmissaryBonus > 0).Sum(x => x.emmissaryBonus);
        return Mathf.Min(MAX_SKILL_LEVEL, total);
    }

    public int GetMage()
    {
        int total = mage + artifacts.FindAll(x => x.mageBonus > 0).Sum(x => x.mageBonus);
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
        this.health = Mathf.Min(100, this.health + health);
        MessageDisplayNoUI.ShowMessage(hex, this,  $"{characterName} heals by {health}", Color.green);
        RefreshSelectedCharacterIconIfSelected();
        CharacterIcons.RefreshForHumanPlayerCharacter(this);
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
