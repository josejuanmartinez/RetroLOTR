public class PlayableLeader : Leader
{
    override public void Killed(Leader killedBy)
    {
        PlayableLeader playable = this;
        playable.health = 0;
        playable.killed = true;

        if (playable == FindFirstObjectByType<Game>().player == playable)
        {
            FindFirstObjectByType<Game>().EndGame(false);
            return;
        }
        foreach (Character character in GetOwner().controlledCharacters)
        {
            if (character == playable)
            {
                if (character.IsArmyCommander()) character.GetArmy().Killed(killedBy);
                hex.characters.Remove(character);
                continue;
            }
            character.owner = killedBy;
            if (character.IsArmyCommander()) character.GetArmy().commander = playable;
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
    new public void NewTurn()
    {
        base.NewTurn();
    }
}