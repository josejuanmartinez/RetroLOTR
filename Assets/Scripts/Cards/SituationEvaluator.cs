using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SituationPriorityData
{
    public List<string> situations = new();
}

public static class SituationEvaluator
{
    // Resources path (no extension) of the user-authored priority order.
    // Edited via Window > RetroLOTR > AI Widget > Situations.
    public const string PriorityResourcePath = "AI/SituationPriority";

    // Default priority order — used when no authored order exists, and to
    // append situations missing from an older authored file.
    private static readonly CardSituationEnum[] DefaultPriority =
    {
        // Underground travel is always offered when standing on an underground hex.
        CardSituationEnum.CharacterAtUndergroundHex,
        CardSituationEnum.ArmyAtEnemyPC,
        CardSituationEnum.AgentAtEnemyPC,
        CardSituationEnum.EmmissaryAtEnemyPC,
        CardSituationEnum.MageAtEnemyPC,
        CardSituationEnum.ArmyAtEnemyPCorEnemyArmy,
        CardSituationEnum.ArmyAtHexWithEnemyArmyAndNoPC,
        CardSituationEnum.AgentAtHexWithEnemyCharacter,
        CardSituationEnum.AgentAtHexWithFriendlyCharacter,
        CardSituationEnum.EmmissaryAtHexWithEnemyCharacter,
        CardSituationEnum.EnemyMageAtHex,
        CardSituationEnum.MageAtHexWithEnemyCharacter,
        CardSituationEnum.MageAtHexWithFriendlyCharacter,
        CardSituationEnum.MageAtArtifactHex,
        CardSituationEnum.EnemyCharacterAtHex,
        CardSituationEnum.EnemyCharacterAtHexNoArmyCommander,
        CardSituationEnum.CommanderAtOwnPC,
        CardSituationEnum.MageAtOwnPC,
        CardSituationEnum.EmmissaryAtOwnPC,
        CardSituationEnum.EmmissaryAtFriendlyPC,
        CardSituationEnum.AgentAtOwnPC,
        CardSituationEnum.ArmyAtFriendlyPC,
        CardSituationEnum.AgentWithHostage,
        CardSituationEnum.EnemyAgentWithMyHostageAtHex,
        CardSituationEnum.OwnDoubledCharacterByEnemyAtHex,
        CardSituationEnum.FriendlyCharacterAtHex,
    };

    private static CardSituationEnum[] loadedPriority;

    public static IReadOnlyList<CardSituationEnum> GetDefaultPriority() => DefaultPriority;

    // Priority order — first two wins when more than 2 situations are active.
    public static IReadOnlyList<CardSituationEnum> GetPriority()
    {
        loadedPriority ??= LoadPriority();
        return loadedPriority;
    }

    // Clears the cached order so the next query re-reads the authored file.
    public static void ReloadPriority() => loadedPriority = null;

    private static CardSituationEnum[] LoadPriority()
    {
        List<CardSituationEnum> order = new();
        HashSet<CardSituationEnum> seen = new();

        TextAsset asset = Resources.Load<TextAsset>(PriorityResourcePath);
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
        {
            SituationPriorityData data = null;
            try { data = JsonUtility.FromJson<SituationPriorityData>(asset.text); }
            catch (Exception e) { Debug.LogWarning($"SituationEvaluator: could not parse {PriorityResourcePath}.json — using default priority. {e.Message}"); }

            if (data?.situations != null)
            {
                foreach (string name in data.situations)
                {
                    if (Enum.TryParse(name, true, out CardSituationEnum situation)
                        && situation != CardSituationEnum.None
                        && seen.Add(situation))
                    {
                        order.Add(situation);
                    }
                }
            }
        }

        // Append situations the authored file does not know about yet.
        foreach (CardSituationEnum situation in DefaultPriority)
        {
            if (seen.Add(situation)) order.Add(situation);
        }

        return order.ToArray();
    }

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

        bool friendlyCharacterOnHex         = HasFriendlyCharacter(hex, character);
        bool enemyCharacterOnHex            = HasEnemyCharacter(hex, character);
        bool enemyCharacterNoCommanderOnHex = HasEnemyCharacterNoArmyCommander(hex, character);
        bool enemyMageOnHex                 = HasEnemyMage(hex, character);
        bool enemyArmyOnHex                 = HasEnemyArmy(hex, character);
        bool hasArtifact                    = hex.hiddenObjects != null && hex.hiddenObjects.Count > 0;
        bool hasHostage                     = character.kidnappedCharacters != null && character.kidnappedCharacters.Any(r => r != null && r.character != null && !r.character.killed);
        bool enemyHoldsMyHostage            = HasEnemyWithMyHostage(hex, character);
        bool ownCharDoubledByEnemy          = HasOwnCharacterDoubledByEnemy(hex, character);

        foreach (CardSituationEnum situation in GetPriority())
        {
            bool matches = situation switch
            {
                CardSituationEnum.CharacterAtUndergroundHex          => !hasArmy     && hex.IsUnderground(),
                CardSituationEnum.ArmyAtEnemyPC                      => hasArmy      && atEnemyPC,
                CardSituationEnum.AgentAtEnemyPC                     => hasAgent     && atEnemyPC,
                CardSituationEnum.EmmissaryAtEnemyPC                 => hasEmmissary && atEnemyPC,
                CardSituationEnum.MageAtEnemyPC                      => hasMage      && atEnemyPC,
                CardSituationEnum.ArmyAtFriendlyPC                   => hasArmy      && atFriendlyPC,
                CardSituationEnum.EmmissaryAtOwnPC                   => hasEmmissary && atOwnPC,
                CardSituationEnum.EmmissaryAtFriendlyPC              => hasEmmissary && atFriendlyPC,
                CardSituationEnum.AgentAtOwnPC                       => hasAgent     && atOwnPC,
                CardSituationEnum.ArmyAtHexWithEnemyArmyAndNoPC     => hasArmy      && enemyArmyOnHex && pc == null,
                CardSituationEnum.ArmyAtEnemyPCorEnemyArmy          => hasArmy      && (atEnemyPC || enemyArmyOnHex),
                CardSituationEnum.AgentAtHexWithEnemyCharacter       => hasAgent     && enemyCharacterOnHex,
                CardSituationEnum.AgentAtHexWithFriendlyCharacter    => hasAgent     && friendlyCharacterOnHex,
                CardSituationEnum.EmmissaryAtHexWithEnemyCharacter   => hasEmmissary && enemyCharacterOnHex,
                CardSituationEnum.EnemyMageAtHex                     => hasMage      && enemyMageOnHex,
                CardSituationEnum.MageAtHexWithEnemyCharacter        => hasMage      && enemyCharacterOnHex,
                CardSituationEnum.MageAtHexWithFriendlyCharacter     => hasMage      && friendlyCharacterOnHex,
                CardSituationEnum.MageAtArtifactHex                  => hasMage      && hasArtifact,
                CardSituationEnum.CommanderAtOwnPC                   => hasCommander && atOwnPC,
                CardSituationEnum.MageAtOwnPC                        => hasMage      && atOwnPC,
                CardSituationEnum.AgentWithHostage                   => hasAgent     && hasHostage,
                CardSituationEnum.EnemyAgentWithMyHostageAtHex       => enemyHoldsMyHostage,
                CardSituationEnum.OwnDoubledCharacterByEnemyAtHex    => ownCharDoubledByEnemy,
                CardSituationEnum.FriendlyCharacterAtHex             => friendlyCharacterOnHex,
                CardSituationEnum.EnemyCharacterAtHex                => enemyCharacterOnHex,
                CardSituationEnum.EnemyCharacterAtHexNoArmyCommander => enemyCharacterNoCommanderOnHex,
                _                                                     => false
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

    private static bool HasFriendlyCharacter(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader owner = character.GetOwner();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            if (other.GetOwner() == owner) return true;
        }
        return false;
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

    private static bool HasEnemyCharacterNoArmyCommander(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader owner = character.GetOwner();
        AlignmentEnum myAlignment = character.GetAlignment();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            if (other.IsArmyCommander()) continue;
            Leader otherOwner = other.GetOwner();
            if (otherOwner == null || otherOwner == owner) continue;
            AlignmentEnum otherAlignment = other.GetAlignment();
            if (otherAlignment == AlignmentEnum.neutral || myAlignment == AlignmentEnum.neutral || otherAlignment != myAlignment)
                return true;
        }
        return false;
    }

    private static bool HasEnemyMage(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader owner = character.GetOwner();
        AlignmentEnum myAlignment = character.GetAlignment();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            if (other.IsHidden() || other.IsRefusingDuels()) continue;
            if (other.GetMage() < 1) continue;
            Leader otherOwner = other.GetOwner();
            if (otherOwner == null || otherOwner == owner) continue;
            AlignmentEnum otherAlignment = other.GetAlignment();
            if (otherAlignment == AlignmentEnum.neutral || myAlignment == AlignmentEnum.neutral || otherAlignment != myAlignment)
                return true;
        }
        return false;
    }

    private static bool HasEnemyWithMyHostage(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader myOwner = character.GetOwner();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            if (other.GetOwner() == myOwner) continue;
            if (other.kidnappedCharacters == null) continue;
            if (other.kidnappedCharacters.Any(r => r != null && r.character != null && !r.character.killed && r.character.GetOwner() == myOwner))
                return true;
        }
        return false;
    }

    private static bool HasOwnCharacterDoubledByEnemy(Hex hex, Character character)
    {
        if (hex.characters == null) return false;
        Leader myOwner = character.GetOwner();
        foreach (Character other in hex.characters)
        {
            if (other == null || other == character || other.killed) continue;
            if (other.GetOwner() != myOwner) continue;
            if (other.doubledBy != null && other.doubledBy.Any(l => l != null && l != myOwner))
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
