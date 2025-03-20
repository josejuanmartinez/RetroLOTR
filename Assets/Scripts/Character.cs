using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Character : MonoBehaviour
{
    public string characterName;
    public AlignmentEnum alignment;
    public Leader owner;
    public Hex hex;

    [SerializeField] int commander = 0;
    [SerializeField] int agent = 0;
    [SerializeField] int emmissary = 0;
    [SerializeField] int mage = 0;

    public int health = 100;
    public int moved = 0;

    public bool hasMovedThisTurn;
    public bool hasActionedThisTurn;
    public bool isEmbarked;

    public List<Leader> doubledBy;
    public List<Artifact> artifacts;

    [SerializeField]
    private Army army = null;

    public bool killed = false;

    void Awake()
    {
        characterName = gameObject.name;
        doubledBy = new();
    }

    public void Initialize(Leader owner, AlignmentEnum alignment, Hex hex, bool startsWithDefaultAmy = false, string characterName = null )
    {
        this.owner = owner;
        this.alignment = alignment;
        hasActionedThisTurn = false;
        hasMovedThisTurn = false;
        isEmbarked = false;
        this.army = null;
        this.hex = hex;
        hex.characters.Add(this);
        if (startsWithDefaultAmy)
        {
            if(owner.biome.startingArmySize > 0 || owner.biome.startingWarships > 0)
            {
                CreateArmy(owner.biome.preferedTroopType, owner.biome.startingArmySize, owner.biome.startingWarships);
            }
        }
        this.owner.controlledCharacters.Add(this);
        if (characterName != null) this.characterName = characterName;
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
        return army != null && army.commander != null && army.GetSize() > 0;
    }

    public int GetMovementLeft()
    {
        return Mathf.Max(0, (GetMaxMovement() - moved));
    }

    public void CreateArmy(TroopsTypeEnum troopsType, int amount, int ws = 0)
    {
        army = new Army(this, troopsType, amount, ws);
        hex.armies.Add(army);
    }

    public Army GetArmy()
    {
        if (!IsArmyCommander()) return null;
        return army;
    }

    virtual public void Killed(Leader killedBy)
    {
        health = 0;
        killed = true;
        if (IsArmyCommander()) army.Killed();
        GetOwner().controlledCharacters.Remove(this);
        hex.characters.Remove(this);
    }

    public void Wounded(Leader woundedBy, int damage)
    {
        health -= damage;
        if (health < 1) Killed(woundedBy);
    }

    public void Doubled(Leader doubledBy)
    {
        this.doubledBy.Add(doubledBy);
    }
    public void Undouble(Leader doubledBy)
    {
        this.doubledBy.Remove(doubledBy);
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
}
