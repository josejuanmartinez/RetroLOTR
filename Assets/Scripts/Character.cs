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

    [SerializeField]
    private Army army = null;
    // public List<Spell>;
    // public List<Artifact>;
    void Awake()
    {
        characterName = gameObject.name;
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
        return army;
    }

    public void Killed(Leader killedBy)
    {
        
        if ((this as PlayableLeader) != null)
        {
            PlayableLeader playable = this as PlayableLeader;
            playable.health = 0;
            playable.killed = true;

            if (playable == FindFirstObjectByType<Game>().player == playable)
            {
                FindFirstObjectByType<Game>().EndGame();
                return;
            }
            foreach (Character character in GetOwner().controlledCharacters)
            {
                if (character == playable)
                {
                    if(character.IsArmyCommander()) character.army.Killed();
                    hex.characters.Remove(character);
                    continue;
                }
                character.owner = killedBy;
                if (character.IsArmyCommander()) character.army.commander = playable;
                character.alignment = killedBy.alignment;
                killedBy.controlledCharacters.Add(character);
            }

            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = killedBy;
                killedBy.controlledPcs.Add(pc);
                killedBy.visibleHexes.Add(pc.hex);
            }

            playable.controlledCharacters = new System.Collections.Generic.List<Character>();
            playable.controlledPcs = new System.Collections.Generic.List<PC>();
            playable.visibleHexes = new System.Collections.Generic.List<Hex>();

            killedBy.leatherAmount += playable.leatherAmount;
            killedBy.mountsAmount += playable.mountsAmount;
            killedBy.timberAmount += playable.timberAmount;
            killedBy.ironAmount += playable.ironAmount;
            killedBy.mithrilAmount += playable.mithrilAmount;
            killedBy.goldAmount += playable.goldAmount;

            playable.leatherAmount = 0;
            playable.mountsAmount = 0;
            playable.timberAmount = 0;
            playable.ironAmount = 0;
            playable.mithrilAmount = 0;
            playable.goldAmount = 0;

            FindFirstObjectByType<Game>().competitors.Remove(playable);

            enabled = false;
        }
        else if ((this as NonPlayableLeader) != null)
        {
            NonPlayableLeader nonPlayable = (this as NonPlayableLeader);
            nonPlayable.health = 1;
            Character nonPlayableLeaderAsCharacter = gameObject.AddComponent<Character>();
            foreach (Character character in GetOwner().controlledCharacters)
            {
                if (character == nonPlayable)
                {                    
                    nonPlayableLeaderAsCharacter.Initialize(killedBy, killedBy.alignment);
                    continue;
                }
                character.owner = killedBy;
                if(character.IsArmyCommander()) character.army.commander = nonPlayableLeaderAsCharacter;
                character.alignment = killedBy.alignment;
                killedBy.controlledCharacters.Add(character);
            }

            foreach (PC pc in GetOwner().controlledPcs)
            {
                pc.owner = killedBy;
                killedBy.controlledPcs.Add(pc);
                killedBy.visibleHexes.Add(pc.hex);
            }
            nonPlayable.controlledCharacters = new System.Collections.Generic.List<Character>();
            nonPlayable.controlledPcs = new System.Collections.Generic.List<PC>();
            nonPlayable.visibleHexes = new System.Collections.Generic.List<Hex>();

            killedBy.leatherAmount += nonPlayable.leatherAmount;
            killedBy.mountsAmount += nonPlayable.mountsAmount;
            killedBy.timberAmount += nonPlayable.timberAmount;
            killedBy.ironAmount += nonPlayable.ironAmount;
            killedBy.mithrilAmount += nonPlayable.mithrilAmount;
            killedBy.goldAmount += nonPlayable.goldAmount;

            nonPlayable.leatherAmount = 0;
            nonPlayable.mountsAmount = 0;
            nonPlayable.timberAmount = 0;
            nonPlayable.ironAmount = 0;
            nonPlayable.mithrilAmount = 0;
            nonPlayable.goldAmount = 0;

            nonPlayable.killed = true;

            FindFirstObjectByType<Game>().npcs.Remove(nonPlayable);

            enabled = false;
        }
        else
        {
            if (IsArmyCommander()) army.Killed();
            GetOwner().controlledCharacters.Remove(this);
            hex.characters.Remove(this);
        }
    }
}
