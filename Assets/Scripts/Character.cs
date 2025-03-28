using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("Character Starting Data")]
    [SerializeField] private BiomeConfig characterBiome;

    [Header("Metadata")]
    public bool startingCharacter;

    [Header("Current allegiance")]
    [HideIf("startingCharacter")]
    public string characterName;
    public AlignmentEnum alignment;
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
    public List<Hex> reachableHexes;

    [Header("Spionage")]
    public List<Leader> doubledBy;
    
    [Header("Artifacts")]
    public List<Artifact> artifacts;

    [Header("Army")]
    [SerializeField]
    private Army army = null;

    void Awake()
    {
        army = null;
        doubledBy = new();
        reachableHexes = new();
        killed = false;
        startingCharacter = true;
    }

    public void Initialize(Leader owner, AlignmentEnum alignment, Hex hex, string characterName, bool startingCharacter = false, bool generateStartingArmy = false)
    {
        MessageDisplay.ShowMessage($"Character {characterName} starts serving {owner.GetOwner().characterName}", Color.green);
        this.characterName = characterName;
        owner.GetOwner().controlledCharacters.Add(this);
        this.owner = owner.GetOwner();
        this.alignment = alignment;
        hasActionedThisTurn = false;
        hasMovedThisTurn = false;
        isEmbarked = false;
        army = null;
        this.hex = hex;
        hex.characters.Add(this);
        this.startingCharacter = startingCharacter;

        if (startingCharacter && (owner.GetBiome().startingArmySize > 0 || owner.GetBiome().startingWarships > 0))
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

        // ADD RL IN CASE IT'S NOT THERE
        if(owner != FindFirstObjectByType<Game>().player && gameObject.GetComponent<StrategyGameAgent>() == null) gameObject.AddComponent<StrategyGameAgent>();
    }

    public AlignmentEnum GetAlignment()
    {
        return owner != null? owner.GetAlignment() : alignment;
    }

    public void NewTurn()
    {
        
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

    virtual public void Killed(Leader killedBy)
    {
        if (IsArmyCommander() && !army.killed) army.Killed(killedBy);
        if(GetOwner().controlledCharacters.Contains(this)) GetOwner().controlledCharacters.Remove(this);
        if(hex.characters.Contains(this)) hex.characters.Remove(this);
        health = 0;
        killed = true;
        MessageDisplay.ShowMessage($"{characterName} died due to wounds", Color.red);
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

    public StrategyGameAgent GetAI()
    {
        return gameObject.GetComponent<StrategyGameAgent>();
    }

}