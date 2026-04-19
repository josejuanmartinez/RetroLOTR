using System;
using System.Collections.Generic;

[Serializable]
public class ArtifactCollection
{
	public List<Artifact> artifacts = new ();
}


[Serializable]
public class Artifact
{
    public string artifactName;
    public bool hidden = false; 
    public AlignmentEnum alignment = AlignmentEnum.neutral;
    public int commanderBonus = 0;
    public int agentBonus = 0;
    public int emmissaryBonus = 0;
    public int mageBonus = 0;
    public int bonusAttack = 0;
    public int bonusDefense = 0;
    public string passiveEffectId = "";
    public int passiveEffectValue = 0;
    public bool transferable = true;
    public string spriteString = "";

    public const int OppositeAlignmentHealthPenalty = 10;
    
    public string GetSpriteString()
    {
        return spriteString != "" ? spriteString : "artifact";
    }

    public bool IsOppositeAlignment(AlignmentEnum bearerAlignment)
    {
        if (alignment == AlignmentEnum.neutral || bearerAlignment == AlignmentEnum.neutral) return false;
        return alignment != bearerAlignment;
    }

    public bool ShouldApplyAlignmentPenalty(AlignmentEnum bearerAlignment)
    {
        return hidden && IsOppositeAlignment(bearerAlignment);
    }

    public string GetHoverText()
    {
        List<string> sb = new() {$"<sprite name=\"{GetSpriteString()}\">{GetSpriteString()}<u>{artifactName}</u>"};
        
        List<string> sbDetails = BuildMechanicalDetails();
        if (sbDetails.Count > 0)
        {
            sb.Add($"<br>{string.Join(", ", sbDetails)}");
        }

        return string.Join("", sb);
    }

    private List<string> BuildMechanicalDetails()
    {
        List<string> details = new();
        if (commanderBonus > 0) details.Add($"+{commanderBonus}<sprite name=\"commander\">commander");
        if (agentBonus > 0) details.Add($"+{agentBonus}<sprite name=\"agent\">agent");
        if (emmissaryBonus > 0) details.Add($"+{emmissaryBonus}<sprite name=\"emmissary\">emmissary");
        if (mageBonus > 0) details.Add($"+{mageBonus}<sprite name=\"mage\">mage");
        if (bonusAttack > 0) details.Add($"+{bonusAttack} attack");
        if (bonusDefense > 0) details.Add($"+{bonusDefense} defense");

        string passiveDetail = GetPassiveEffectDescription();
        if (!string.IsNullOrWhiteSpace(passiveDetail))
        {
            details.Add(passiveDetail);
        }

        if (!transferable) details.Add("non-transferable");
        return details;
    }

    private string GetPassiveEffectDescription()
    {
        if (string.IsNullOrWhiteSpace(passiveEffectId)) return null;

        return passiveEffectId switch
        {
            "HealPerTurn" => $"heals {Math.Max(0, passiveEffectValue)} each turn",
            "FindArtifactDifficultyReduction" => $"-{Math.Max(0, passiveEffectValue)} difficulty to Find Artifact",
            "ArmyAttackStrengthBonus" => $"+{Math.Max(0, passiveEffectValue)} army attack",
            "ArmyDefenseStrengthBonus" => $"+{Math.Max(0, passiveEffectValue)} army defense",
            "EnemyArmyDefensePenaltySameHex" => $"-{Math.Max(0, passiveEffectValue)} enemy army defense in same hex",
            "ArmySuccessfulAttackBurningChance" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance to inflict Burning after a successful army attack",
            "EnvironmentalImmunity" => "immune to negative environmental cards",
            "RevealHiddenEnemyPcOnOccupiedHex" => "reveals a hidden enemy PC on the occupied hex",
            "HopeChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to gain Hope",
            "ForestHiddenChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to become Hidden in forests",
            "AlliedPcMoraleChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to raise allied PC loyalty",
            "HexEnemyFearChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Fear on an enemy in the hex",
            "HexEnemyDespairChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Despair on an enemy in the hex",
            "HexEnemyFearAndDespairChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Fear and Despair on an enemy in the hex",
            "SelfDespairChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to suffer Despair",
            "ArkenstoneGoldAndDespair" => "+10% gold chance each turn, but 5% chance each turn to suffer Despair",
            "RandomHexRevealChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to reveal a random hex",
            "HasteAtSea" => "grants Haste at sea",
            "SelfFearAndDespairCleanseChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to cleanse Fear and Despair",
            "HexEnemyBurningChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Burning on an enemy in the hex",
            "HexEnemyHaltChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Halted on an enemy in the hex",
            "HexEnemyPoisonChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to inflict Poisoned on an enemy in the hex",
            "MountsChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to gain +1 <sprite name=\"mounts\">",
            "HasteChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to gain Haste",
            "GoldChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to gain +1 <sprite name=\"gold\">",
            "HideOccupiedPcWhilePresent" => "hides the occupied PC while present",
            "BlockEnemyCharactersOnHex" => "blocks enemy characters on this hex",
            "EncouragedChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to gain Courage",
            "LiquourCourageAndSleep" => "15% chance each turn to gain Courage, 5% chance each turn to fall asleep",
            "BlockedSelfChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to become Blocked",
            "FreePeopleNonMenHaltChancePerTurn" => $"{Math.Clamp(passiveEffectValue, 0, 100)}% chance each turn to Halt non-Man Free People enemies in the hex",
            _ => HumanizePassiveEffectId(passiveEffectId, passiveEffectValue)
        };
    }

    private static string HumanizePassiveEffectId(string effectId, int effectValue)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return null;

        List<char> chars = new(effectId.Length + 4);
        for (int i = 0; i < effectId.Length; i++)
        {
            char current = effectId[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(effectId[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        string label = new string(chars.ToArray()).Trim();
        return effectValue > 0 ? $"{label} ({effectValue})" : label;
    }

    public int GetPassiveHealPerTurn()
    {
        if (string.Equals(passiveEffectId, "HealPerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, passiveEffectValue);
        }

        return 0;
    }

    public int GetActionDifficultyReduction(string actionClassName)
    {
        if (string.Equals(passiveEffectId, "FindArtifactDifficultyReduction", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actionClassName, "FindArtifact", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, passiveEffectValue);
        }

        return 0;
    }

    public int GetArmyAttackStrengthBonus()
    {
        if (string.Equals(passiveEffectId, "ArmyAttackStrengthBonus", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, passiveEffectValue);
        }

        return 0;
    }

    public int GetArmyDefenseStrengthBonus()
    {
        if (string.Equals(passiveEffectId, "ArmyDefenseStrengthBonus", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, passiveEffectValue);
        }

        return 0;
    }

    public int GetEnemyArmyDefensePenaltySameHex()
    {
        if (string.Equals(passiveEffectId, "EnemyArmyDefensePenaltySameHex", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, passiveEffectValue);
        }

        return 0;
    }

    public int GetArmySuccessfulAttackBurningChancePercent()
    {
        if (string.Equals(passiveEffectId, "ArmySuccessfulAttackBurningChance", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public bool GrantsEnvironmentalImmunity()
    {
        return string.Equals(passiveEffectId, "EnvironmentalImmunity", StringComparison.OrdinalIgnoreCase);
    }

    public bool RevealsHiddenEnemyPcOnOccupiedHex()
    {
        return string.Equals(passiveEffectId, "RevealHiddenEnemyPcOnOccupiedHex", StringComparison.OrdinalIgnoreCase);
    }

    public int GetHopeChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HopeChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetForestHiddenChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "ForestHiddenChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetAlliedPcMoraleChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "AlliedPcMoraleChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHexEnemyFearChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HexEnemyFearChancePerTurn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(passiveEffectId, "HexEnemyFearAndDespairChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHexEnemyDespairChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HexEnemyDespairChancePerTurn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(passiveEffectId, "HexEnemyFearAndDespairChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetSelfDespairChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "SelfDespairChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        if (string.Equals(passiveEffectId, "ArkenstoneGoldAndDespair", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        return 0;
    }

    public int GetRandomHexRevealChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "RandomHexRevealChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public bool GrantsHasteAtSea()
    {
        return string.Equals(passiveEffectId, "HasteAtSea", StringComparison.OrdinalIgnoreCase);
    }

    public int GetSelfFearAndDespairCleanseChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "SelfFearAndDespairCleanseChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHexEnemyBurningChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HexEnemyBurningChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHexEnemyHaltChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HexEnemyHaltChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHexEnemyPoisonChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HexEnemyPoisonChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetMountsChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "MountsChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetHasteChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "HasteChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }

    public int GetGoldChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "GoldChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        if (string.Equals(passiveEffectId, "ArkenstoneGoldAndDespair", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return 0;
    }

    public bool HidesOccupiedPcWhilePresent()
    {
        return string.Equals(passiveEffectId, "HideOccupiedPcWhilePresent", StringComparison.OrdinalIgnoreCase);
    }

    public bool BlocksEnemyCharactersOnHex()
    {
        return string.Equals(passiveEffectId, "BlockEnemyCharactersOnHex", StringComparison.OrdinalIgnoreCase);
    }

    public int GetEncouragedChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "EncouragedChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        if (string.Equals(passiveEffectId, "LiquourCourageAndSleep", StringComparison.OrdinalIgnoreCase))
        {
            return 15;
        }

        return 0;
    }

    public int GetBlockedSelfChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "BlockedSelfChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        if (string.Equals(passiveEffectId, "LiquourCourageAndSleep", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        return 0;
    }

    public int GetFreePeopleNonMenHaltChancePerTurnPercent()
    {
        if (string.Equals(passiveEffectId, "FreePeopleNonMenHaltChancePerTurn", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Clamp(passiveEffectValue, 0, 100);
        }

        return 0;
    }
}
