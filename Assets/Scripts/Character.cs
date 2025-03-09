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
    public Army army;
    public bool hasMovedThisTurn;
    public bool hasActionedThisTurn;
    // public List<Spell>;
    // public List<Artifact>;

    public void Initialize(Leader owner, AlignmentEnum alignment )
    {
        this.owner = owner;
        characterName = gameObject.name.ToLower();
        this.alignment = alignment;
        army = null;
        hasActionedThisTurn = false;
        hasMovedThisTurn = false;
    }


    public AlignmentEnum GetAlignment()
    {
        return alignment;
    }

    public void NewTurn()
    {
        
    }
    public virtual Leader GetOwner()
    {
        if (!owner && this is Leader) return this as Leader;
        
        return owner;
    }
}
