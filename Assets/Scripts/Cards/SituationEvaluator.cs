using System.Collections.Generic;

public static class SituationEvaluator
{
    // Priority order — first two wins when more than 2 situations are active.
    private static readonly CardSituationEnum[] Priority =
    {
        CardSituationEnum.ArmyAtEnemyPC,
        CardSituationEnum.AgentAtEnemyPC,
        CardSituationEnum.EmmissaryAtEnemyPC,
        CardSituationEnum.ArmyAtHexWithEnemyArmyAndNoPC,
        CardSituationEnum.AgentAtHexWithEnemyCharacter,
        CardSituationEnum.EmmissaryAtHexWithEnemyCharacter,
        CardSituationEnum.MageAtHexWithEnemyCharacter,
        CardSituationEnum.MageAtArtifactHex,
        CardSituationEnum.CommanderAtOwnPC,
        CardSituationEnum.EmmissaryAtOwnPC,
        CardSituationEnum.AgentAtOwnPC,
        CardSituationEnum.ArmyAtFriendlyPC,
    };

    public static List<CardSituationEnum> GetActiveSituations(Character character, Hex hex)
    {
        var active = new List<CardSituationEnum>();
        if (character == null || hex == null || character.killed) return active;

        bool hasArmy      = character.IsArmyCommander();
        bool hasAgent     = character.GetAgent() > 0;
        bool hasEmmissary = character.GetEmmissary() > 0;
        bool hasMage      = character.GetMage() > 0;
        bool hasCommander = character.GetCommander() > 0;

        PC pc = hex.GetPC();
        bool atOwnPC      = pc != null && IsOwnPC(pc, character);
        bool atFriendlyPC = pc != null && IsFriendlyPC(pc, character);
        bool atEnemyPC    = pc != null && IsEnemyPC(pc, character);

        bool enemyCharacterOnHex = HasEnemyCharacter(hex, character);
        bool enemyArmyOnHex      = HasEnemyArmy(hex, character);
        bool hasArtifact          = hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0;

        foreach (CardSituationEnum situation in Priority)
        {
            bool matches = situation switch
            {
                CardSituationEnum.ArmyAtEnemyPC                    => hasArmy      && atEnemyPC,
                CardSituationEnum.AgentAtEnemyPC                   => hasAgent     && atEnemyPC,
                CardSituationEnum.EmmissaryAtEnemyPC               => hasEmmissary && atEnemyPC,
                CardSituationEnum.ArmyAtFriendlyPC                 => hasArmy      && atFriendlyPC,
                CardSituationEnum.EmmissaryAtOwnPC                 => hasEmmissary && atOwnPC,
                CardSituationEnum.AgentAtOwnPC                     => hasAgent     && atOwnPC,
                CardSituationEnum.ArmyAtHexWithEnemyArmyAndNoPC   => hasArmy      && enemyArmyOnHex && pc == null,
                CardSituationEnum.AgentAtHexWithEnemyCharacter     => hasAgent     && enemyCharacterOnHex,
                CardSituationEnum.EmmissaryAtHexWithEnemyCharacter => hasEmmissary && enemyCharacterOnHex,
                CardSituationEnum.MageAtHexWithEnemyCharacter      => hasMage      && enemyCharacterOnHex,
                CardSituationEnum.MageAtArtifactHex                => hasMage      && hasArtifact,
                CardSituationEnum.CommanderAtOwnPC                 => hasCommander && atOwnPC,
                _                                                   => false
            };

            if (matches) active.Add(situation);
        }

        return active;
    }

    private static bool IsOwnPC(PC pc, Character character)
        => pc.owner != null && pc.owner == character.GetOwner();

    private static bool IsFriendlyPC(PC pc, Character character)
    {
        if (pc.owner == null) return false;
        if (pc.owner == character.GetOwner()) return true;
        AlignmentEnum pcAlignment  = pc.owner.GetAlignment();
        AlignmentEnum myAlignment  = character.GetAlignment();
        return myAlignment != AlignmentEnum.neutral && pcAlignment == myAlignment;
    }

    private static bool IsEnemyPC(PC pc, Character character)
    {
        if (pc.owner == null) return false;
        if (pc.owner == character.GetOwner()) return false;
        AlignmentEnum pcAlignment = pc.owner.GetAlignment();
        AlignmentEnum myAlignment = character.GetAlignment();
        return pcAlignment == AlignmentEnum.neutral || myAlignment == AlignmentEnum.neutral || pcAlignment != myAlignment;
    }

    private static bool HasEnemyCharacter(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader owner = character.GetOwner();
        AlignmentEnum myAlignment = character.GetAlignment();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            Leader otherOwner = other.GetOwner();
            if (otherOwner == null || otherOwner == owner) continue;
            AlignmentEnum otherAlignment = other.GetAlignment();
            if (otherAlignment == AlignmentEnum.neutral || myAlignment == AlignmentEnum.neutral || otherAlignment != myAlignment)
                return true;
        }
        return false;
    }

    private static bool HasEnemyArmy(Hex hex, Character character)
    {
        if (hex.armies == null) return false;
        Leader owner = character.GetOwner();
        AlignmentEnum myAlignment = character.GetAlignment();
        foreach (Army army in hex.armies)
        {
            if (army == null || army.killed) continue;
            Character cmd = army.GetCommander();
            if (cmd == null || cmd.killed) continue;
            Leader cmdOwner = cmd.GetOwner();
            if (cmdOwner == null || cmdOwner == owner) continue;
            AlignmentEnum cmdAlignment = cmd.GetAlignment();
            if (cmdAlignment == AlignmentEnum.neutral || myAlignment == AlignmentEnum.neutral || cmdAlignment != myAlignment)
                return true;
        }
        return false;
    }
}
