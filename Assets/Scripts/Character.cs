using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public string characterName;
    public AlignmentEnum alignment;
    public Leader owner;
    public Hex hex;

    public int commander = 0;
    public int agent = 0;
    public int emmissary = 0;
    public int mage = 0;
    public int health = 100;
    public int moved = 0;

    public bool hasMovedThisTurn;
    public bool hasActionedThisTurn;
    public bool isEmbarked;

    public List<Leader> doubledBy;
    public List<Artifact> artifacts;

    [SerializeField]
    private Army army = null;
    // public List<Spell>;
    // public List<Artifact>;

    public bool killed = false;

    void Awake()
    {
        characterName = gameObject.name;
        doubledBy = new();
    }

    public void Initialize(Leader owner, AlignmentEnum alignment, string characterName = null )
    {
        this.owner = owner;
        this.alignment = alignment;
        hasActionedThisTurn = false;
        hasMovedThisTurn = false;
        isEmbarked = false;
        army = null;
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
}
