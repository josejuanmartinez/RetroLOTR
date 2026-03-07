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
    public string artifactDescription;
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
        List<string> sb = new() {$"<sprite name=\"{GetSpriteString()}\"><u>{artifactName}</u>"};
        if (artifactDescription.Trim().Length > 0) sb.Add($"<br>{artifactDescription}");  
        
        List<string> sbDetails = new();
        if(commanderBonus>0) sbDetails.Add($"+{commanderBonus}<sprite name=\"commander\">");
        if(agentBonus>0) sbDetails.Add($"+{agentBonus}<sprite name=\"agent\">");
        if(emmissaryBonus>0) sbDetails.Add($"+{emmissaryBonus}<sprite name=\"emmissary\">");
        if(mageBonus>0) sbDetails.Add($"+{mageBonus}<sprite name=\"mage\">");
        if(bonusAttack>0) sbDetails.Add($"+{bonusAttack} to attack");
        if(bonusDefense>0) sbDetails.Add($"+{bonusDefense} to defense");
        if(!transferable) sbDetails.Add("non-transferable");
        string sbDetailStr = string.Join(", ", sbDetails);
        if(sbDetailStr.Length > 0) sb.Add($"<br>{sbDetailStr}");
        return string.Join("", sb);
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
