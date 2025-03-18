public class NonPlayableLeader : Leader
{
    override public void Killed(Leader killedBy)
    {
        NonPlayableLeader nonPlayable = this;
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
            if (character.IsArmyCommander()) character.GetArmy().commander = nonPlayableLeaderAsCharacter;
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

    new public void NewTurn()
    {
        base.NewTurn();
    }
}