using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class Character : MonoBehaviour
{
    public static int MAX_RELEVANT_HEXES = Game.MAX_CHARACTERS + Game.MAX_ARTIFACTS + Game.MAX_PCS;

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

    [Header("Statuses")]
    [SerializeField] private bool isHalted = false;
    [SerializeField] private int encouragedTurns = -1; // 0 means this turn is still encouraged

    private BiomeConfig characterBiome;

    private Colors colors;
    private bool awaken = false;

    void Awake()
    {
        army = null;
        doubledBy = new();
        reachableHexes = new();
        killed = false;
        awaken = true;
        colors = FindFirstObjectByType<Colors>();
    }
    public void InitializeFromBiome(Leader leader, Hex hex, BiomeConfig characterBiome)
    {
        if (!awaken) Awake();
        this.characterBiome = characterBiome;
        bool isLeader = characterBiome is LeaderBiomeConfig;
        Initialize(leader, characterBiome.alignment, hex, characterBiome.characterName, characterBiome.commander, characterBiome.agent, characterBiome.emmissary, characterBiome.mage, characterBiome.race);
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
        RacesEnum race)
    {
        if (!awaken) Awake();

        string ownerName = "";
        if (owner != null && owner.characterName != null) ownerName = owner.characterName;
        if (ownerName.Trim() == "") ownerName = "themselves";
        MessageDisplay.ShowMessage($"Character {characterName} starts serving {ownerName}", Color.green);
        this.characterName = characterName;
        this.commander = commander;
        this.agent = agent;
        this.emmissary = emmissary;
        this.mage = mage;
        this.alignment = alignment;
        this.race = race;
        this.startingCharacter = true;

        owner.GetOwner().controlledCharacters.Add(this);
        this.owner = owner.GetOwner();
        hasActionedThisTurn = false;
        moved = 0;
        isEmbarked = false;
        army = null;
        this.hex = hex;
        hex.characters.Add(this);

        if (this == owner && (owner.GetBiome().startingArmySize > 0 || owner.GetBiome().startingWarships > 0))
        {
            CreateArmy(owner.GetBiome().preferedTroopType, owner.GetBiome().startingArmySize, startingCharacter, owner.GetBiome().startingWarships);
        }
        else
        {
            if (GetOwner() is not PlayableLeader) return;
            
            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != GetOwner()).ToList().ForEach(x =>
            {
                x.CheckCharacterConditions(GetOwner());
            });
        }
    }

    public AlignmentEnum GetAlignment()
    {
        return owner != null? owner.GetAlignment() : alignment;
    }

    public void Pass()
    {
        CharacterAction action = FindFirstObjectByType<ActionsManager>().DEFAULT;
        action.Initialize(this);
        action.Execute();
    }

    public void Halt()
    {
        isHalted = true;
    }

    public void Encourage(int turns = 1)
    {
        encouragedTurns += turns;
    }

    public bool IsEncouraged()
    {
        return encouragedTurns > -1;
    }

    public void NewTurn()
    {
        Debug.Log($"New turn for {characterName} {(isPlayerControlled? "[PLAYER]": "[AI]")}");
        // STATUSES
        // HALT
        if (isHalted && GetOwner() == FindFirstObjectByType<Game>().player) MessageDisplay.ShowMessage("Halted", Color.red);
        moved = isHalted ? GetMaxMovement() : 0;
        hasActionedThisTurn = isHalted;
        isHalted = false;
        // COURAGE
        encouragedTurns = Mathf.Max(encouragedTurns - 1, -1);
        if (IsEncouraged() && GetOwner() == FindFirstObjectByType<Game>().player) MessageDisplay.ShowMessage("Encouraged", Color.green);
        // STATUS EFFECTS (TODO)
        StoreReachableHexes();
        StoreRelevantHexes();
    }

    public virtual Leader GetOwner()
    {
        if (!owner && this is Leader) return this as Leader;
        
        return owner;
    }

    public string GetHoverText(bool withAlignment, bool withCharInfo, bool withLevels, bool withArmy, bool withColor)
    {
        List<string> result = new() { };
        if (withColor) result.Add($"<color={colors.GetHexColorByName(alignment.ToString())}>");
        if(withAlignment) result.Add($"<sprite name=\"{alignment}\">");
        result.Add($"{characterName}");
        if (withCharInfo)
        {
            if (commander > 0) result.Add($"<sprite name=\"commander\">{(withLevels ? "[" + commander.ToString() + "]" : "")}");
            if (agent > 0) result.Add($"<sprite name=\"agent\">{(withLevels ? "[" + agent.ToString() + "]" : "")}");
            if (emmissary > 0) result.Add($"<sprite name=\"emmissary\">{(withLevels ? "[" + emmissary.ToString() + "]" : "")}");
            if (mage > 0) result.Add($"<sprite name=\"mage\">{(withLevels ? "[" + mage.ToString() + "]" : "")}");
        }

        if (withArmy && GetArmy() != null) result.Add(GetArmy().GetHoverText());
        if (withColor) result.Add("</color>");
        return string.Join("", result);
    }

    public MovementType GetMovementType()
    {
        return army == null ? MovementType.Character : army.GetMovementType();
    }

    public int GetMaxMovement()
    {
        MovementType movementType = army == null ? MovementType.Character : army.GetMovementType();
        switch(movementType)
        {            
            case MovementType.ArmyCommander:
                return FindFirstObjectByType<Game>().armyMovement;
            case MovementType.ArmyCommanderCavalryOnly:
                return FindFirstObjectByType<Game>().cavalryMovement;
            case MovementType.Character:
            default:
                return FindFirstObjectByType<Game>().characterMovement;            
        }
        
    }

    public bool IsArmyCommander()
    {
        return army != null && army.commander != null && !army.commander.killed && army.GetSize() > 0;
    }

    public int GetMovementLeft()
    {
        return Mathf.Max(0, (GetMaxMovement() - moved));
    }

    public void CreateArmy(TroopsTypeEnum troopsType, int amount, bool startingArmy, int ws = 0)
    {
        army = new Army(this, troopsType, amount, startingArmy, ws);
        hex.armies.Add(army);
        if(!startingArmy && GetOwner() is PlayableLeader)
        {
            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != GetOwner()).ToList().ForEach(x =>
            {
                x.CheckArmiesConditions(GetOwner());
            });
        }

        MessageDisplay.ShowMessage($"{characterName} just hired an army", Color.green);
    }

    public Army GetArmy()
    {
        if (!IsArmyCommander()) return null;
        return army;
    }

    virtual public void Killed(Leader killedBy, bool onlyMark=false)
    {
        if (IsArmyCommander() && !army.killed) army.Killed(killedBy, onlyMark);
        if(!onlyMark)
        {
            if(GetOwner().controlledCharacters.Contains(this)) GetOwner().controlledCharacters.Remove(this);
            if(hex.characters.Contains(this)) hex.characters.Remove(this);
            hex.RedrawCharacters();
        }
        health = 0;
        killed = true;
        MessageDisplay.ShowMessage($"{characterName} was eliminated", Color.red);
    }

    public void Wounded(Leader woundedBy, int damage)
    {
        health -= damage;
        MessageDisplay.ShowMessage($"{characterName} was wounded by {damage}", Color.red);
        if (health < 1) Killed(woundedBy);
    }

    public void Doubled(Leader doubledBy)
    {
        this.doubledBy.Add(doubledBy);
        MessageDisplay.ShowMessage($"{characterName} will share secrets with {doubledBy.characterName}", Color.green);
    }
    public void Undouble(Leader doubledBy)
    {
        this.doubledBy.Remove(doubledBy);
        MessageDisplay.ShowMessage($"{characterName} will no longer share secrets with {doubledBy.characterName}", Color.green);
    }

    public int GetCommander()
    {
        return commander + artifacts.FindAll(x => x.commanderBonus > 0).Sum(x => x.commanderBonus);
    }

    public int GetAgent()
    {
        return agent + artifacts.FindAll(x => x.agentBonus > 0).Sum(x => x.agentBonus);
    }

    public int GetEmmissary()
    {
        return emmissary + artifacts.FindAll(x => x.emmissaryBonus > 0).Sum(x => x.emmissaryBonus);
    }

    public int GetMage()
    {
        return mage + artifacts.FindAll(x => x.mageBonus > 0).Sum(x => x.mageBonus);
    }

    public void SetCommander(int level)
    {
        commander = Mathf.Clamp(level, 0, 5);
    }

    public void SetAgent(int level)
    {
        agent = Mathf.Clamp(level, 0, 5);
    }

    public void SetEmmissary(int level)
    {
        emmissary = Mathf.Clamp(level, 0, 5);
    }

    public void SetMage(int level)
    {
        mage = Mathf.Clamp(level, 0, 5);
    }


    public void AddCommander(int level)
    {
        commander = Mathf.Clamp(commander + level, 0, 5);
    }

    public void AddAgent(int level)
    {
        agent = Mathf.Clamp(agent + level, 0, 5);
    }

    public void AddEmmissary(int level)
    {
        emmissary = Mathf.Clamp(emmissary + level, 0, 5);
    }

    public void AddMage(int level)
    {
        mage = Mathf.Clamp(mage + level, 0, 5);
    }

    public void Heal(int health)
    {
        this.health = Mathf.Min(100, this.health + health);
        MessageDisplay.ShowMessage($"{characterName} heals by {health}", Color.green);
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

    public void MoveTo(Hex newHex)
    {

    }
}