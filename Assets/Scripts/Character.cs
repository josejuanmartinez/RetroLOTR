using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

public class Character : MonoBehaviour
{
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
    public bool hasMovedThisTurn;
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
    private bool trainingMode = false;
    public bool autoplay = false;

    private BiomeConfig characterBiome;

    private bool awaken = false;

    void Awake()
    {
        army = null;
        doubledBy = new();
        reachableHexes = new();
        killed = false;
        trainingMode = FindAnyObjectByType<Game>().trainingMode;
        autoplay = FindAnyObjectByType<Game>().autoplay;
        awaken = true;
    }
    public void InitializeFromBiome(Leader leader, Hex hex, BiomeConfig characterBiome)
    {
        if (!awaken) Awake();
        this.characterBiome = characterBiome;
        bool isLeader = characterBiome is LeaderBiomeConfig;
        Initialize(leader, characterBiome.alignment, hex, characterBiome.characterName, characterBiome.commander, characterBiome.agent, characterBiome.emmissary, characterBiome.mage);
    }

    public void Initialize(
        Leader owner, 
        AlignmentEnum alignment, 
        Hex hex, 
        string characterName,
        int commander,
        int agent,
        int emmissary,
        int mage)
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
        this.startingCharacter = true;

        owner.GetOwner().controlledCharacters.Add(this);
        this.owner = owner.GetOwner();
        hasActionedThisTurn = false;
        hasMovedThisTurn = false;
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
            hasActionedThisTurn = true;
            hasMovedThisTurn = true;
            moved = GetMaxMovement();

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

    public void NewTurn()
    {
        Debug.Log($"New turn for {characterName} {(isPlayerControlled? "[PLAYER]": "[AI]")}");
        moved = 0;
        hasMovedThisTurn = false;
        hasActionedThisTurn = false;
        StoreReachableHexes();
        StoreRelevantHexes();

        GetAI().NewTurn(isPlayerControlled, autoplay, trainingMode);
        if (!isPlayerControlled)
        {
            moved = GetMaxMovement();
            hasMovedThisTurn = true;
            hasActionedThisTurn = true;
        }
    }

    public virtual Leader GetOwner()
    {
        if (!owner && this is Leader) return this as Leader;
        
        return owner;
    }

    public MovementType GetMovementType()
    {
        return army == null ? MovementType.Character : army.GetMovementType();
    }

    public int GetMaxMovement()
    {
        MovementType movementType = army == null ? MovementType.Character : army.GetMovementType();
        return movementType == MovementType.ArmyCommanderCavalryOnly ? FindFirstObjectByType<Game>().cavalryMovement : FindFirstObjectByType<Game>().normalMovement;
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
        int maxRelevantHexes = game.maxCharacters + game.maxArtifacts + game.maxPCs;
        // Pre-allocate exactly 190 elements for maximum efficiency
        List<Hex> relevantHexes = new(maxRelevantHexes);

        // Use direct access to source collections with index-based insertion
        // var inRangeHexes = hexPathRenderer.FindAllHexesInRange(c);

        var artifactHexes = board.hexesWithArtifacts;
        var characterHexes = board.hexesWithCharacters;
        var pcHexes = board.hexesWithPCs;

        // Add items directly to pre-sized list using index
        //for (int i = 0; i < inRangeHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
        //    relevantHexes.Add(inRangeHexes[i]);

        for (int i = 0; i < artifactHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(artifactHexes[i]);

        for (int i = 0; i < characterHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(characterHexes[i]);

        for (int i = 0; i < pcHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(pcHexes[i]);

        // Fill remaining slots with null (if any)
        int remainingHexes = maxRelevantHexes - relevantHexes.Count;
        for (int i = 0; i < remainingHexes; i++)
            relevantHexes.Add(null);

        Assert.IsTrue(relevantHexes.Count == maxRelevantHexes, "Relevant hexes list size mismatch!");
        this.relevantHexes = relevantHexes;
    }

    public StrategyGameAgent GetAI()
    {
        return gameObject.GetComponentInChildren<StrategyGameAgent>();
    }

}