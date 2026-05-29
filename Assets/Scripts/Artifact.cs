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

    // Legacy passive system — kept for backward compatibility with runtime-created artifacts
    public string passiveEffectId = "";
    public int passiveEffectValue = 0;

    public bool transferable = true;
    public string spriteString = "";

    // ---- New deterministic stat fields ----
    public int healPerTurn = 0;
    public int movementBonus = 0;
    public bool ignoreTerrainMovementPenalty = false;
    public bool grantsHasteAtSea = false;
    public int autoScoutRadius = 0;
    public int detectionEvasion = 0;

    public string attackBonusVsRace = "";
    public int attackBonusVsRaceValue = 0;
    public string attackBonusVsTroopType = "";
    public int attackBonusVsTroopTypeValue = 0;

    public string defenseBonusVsRace = "";
    public int defenseBonusVsRaceValue = 0;
    public string defenseBonusVsTroopType = "";
    public int defenseBonusVsTroopTypeValue = 0;

    public int armyAttackStrengthBonus = 0;
    public int armyDefenseStrengthBonus = 0;
    public int enemyArmyDefensePenaltySameHex = 0;

    public int recruitBonusMenAtArms = 0;
    public int scryAreaBonus = 0;
    public int scryArtifactBonus = 0;

    public string negativeStatusImmunity = "";
    public int negativeStatusDurationReduction = 0;
    public int negativeStatusDamageReduction = 0;
    public int positiveStatusDurationBonus = 0;
    public int positiveStatusEffectBonus = 0;

    public bool grantsEnvironmentalImmunity = false;
    // ----------------------------------------

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
        List<string> sb = new() {$"<sprite name=\"{GetSpriteString()}\">{artifactName}"};

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
        if (commanderBonus > 0) details.Add($"+{commanderBonus}<sprite name=\"commander\">");
        if (agentBonus > 0) details.Add($"+{agentBonus}<sprite name=\"agent\">");
        if (emmissaryBonus > 0) details.Add($"+{emmissaryBonus}<sprite name=\"emmissary\">");
        if (mageBonus > 0) details.Add($"+{mageBonus}<sprite name=\"mage\">e");
        if (bonusAttack > 0) details.Add($"+{bonusAttack} attack");
        if (bonusDefense > 0) details.Add($"+{bonusDefense} defense");

        // New deterministic stats
        if (healPerTurn > 0) details.Add($"heals {healPerTurn} each turn");
        if (movementBonus > 0) details.Add($"+{movementBonus} movement");
        if (ignoreTerrainMovementPenalty) details.Add("ignores terrain movement penalties");
        if (grantsHasteAtSea) details.Add("grants Haste at sea");
        if (autoScoutRadius > 0) details.Add($"auto-scouts radius {autoScoutRadius}");
        if (detectionEvasion > 0) details.Add($"+{detectionEvasion * 10}% harder to detect");

        if (!string.IsNullOrWhiteSpace(attackBonusVsRace) && attackBonusVsRaceValue > 0)
            details.Add($"+{attackBonusVsRaceValue} attack vs {attackBonusVsRace}");
        if (!string.IsNullOrWhiteSpace(attackBonusVsTroopType) && attackBonusVsTroopTypeValue > 0)
            details.Add($"+{attackBonusVsTroopTypeValue} attack vs {attackBonusVsTroopType}");

        if (!string.IsNullOrWhiteSpace(defenseBonusVsRace) && defenseBonusVsRaceValue > 0)
            details.Add($"+{defenseBonusVsRaceValue} defense vs {defenseBonusVsRace}");
        if (!string.IsNullOrWhiteSpace(defenseBonusVsTroopType) && defenseBonusVsTroopTypeValue > 0)
            details.Add($"+{defenseBonusVsTroopTypeValue} defense vs {defenseBonusVsTroopType}");

        if (armyAttackStrengthBonus > 0) details.Add($"+{armyAttackStrengthBonus} army attack");
        if (armyDefenseStrengthBonus > 0) details.Add($"+{armyDefenseStrengthBonus} army defense");
        if (enemyArmyDefensePenaltySameHex > 0) details.Add($"-{enemyArmyDefensePenaltySameHex} enemy army defense in same hex");

        if (recruitBonusMenAtArms > 0) details.Add($"+{recruitBonusMenAtArms} men-at-arms recruited");
        if (scryAreaBonus > 0) details.Add($"+{scryAreaBonus} Scry Area range");
        if (scryArtifactBonus > 0) details.Add($"+{scryArtifactBonus} Find Artifact");

        if (!string.IsNullOrWhiteSpace(negativeStatusImmunity))
            details.Add($"immune to <sprite name=\"{negativeStatusImmunity.ToLower()}\">{negativeStatusImmunity}");
        if (negativeStatusDurationReduction > 0) details.Add($"-{negativeStatusDurationReduction} negative status duration");
        if (negativeStatusDamageReduction > 0) details.Add($"-{negativeStatusDamageReduction} negative status damage");
        if (positiveStatusDurationBonus > 0) details.Add($"+{positiveStatusDurationBonus} positive status duration");
        if (positiveStatusEffectBonus > 0) details.Add($"+{positiveStatusEffectBonus} positive status healing");

        if (grantsEnvironmentalImmunity) details.Add("immune to negative environmental cards");

        if (!transferable) details.Add("non-transferable");
        return details;
    }

    // ---- Typed getters (new fields only) ----

    public int GetHealPerTurn()
    {
        return Math.Max(0, healPerTurn);
    }

    public int GetMovementBonus()
    {
        return Math.Max(0, movementBonus);
    }

    public bool GetIgnoreTerrainMovementPenalty()
    {
        return ignoreTerrainMovementPenalty;
    }

    public int GetAutoScoutRadius()
    {
        return Math.Max(0, autoScoutRadius);
    }

    public int GetDetectionEvasion()
    {
        return Math.Max(0, detectionEvasion);
    }

    public int GetAttackBonusVsRace(RacesEnum race)
    {
        if (attackBonusVsRaceValue > 0 && string.Equals(attackBonusVsRace, race.ToString(), StringComparison.OrdinalIgnoreCase))
            return attackBonusVsRaceValue;
        return 0;
    }

    public int GetAttackBonusVsTroopType(TroopsTypeEnum troopType)
    {
        if (attackBonusVsTroopTypeValue > 0 && string.Equals(attackBonusVsTroopType, troopType.ToString(), StringComparison.OrdinalIgnoreCase))
            return attackBonusVsTroopTypeValue;
        return 0;
    }

    public int GetDefenseBonusVsRace(RacesEnum race)
    {
        if (defenseBonusVsRaceValue > 0 && string.Equals(defenseBonusVsRace, race.ToString(), StringComparison.OrdinalIgnoreCase))
            return defenseBonusVsRaceValue;
        return 0;
    }

    public int GetDefenseBonusVsTroopType(TroopsTypeEnum troopType)
    {
        if (defenseBonusVsTroopTypeValue > 0 && string.Equals(defenseBonusVsTroopType, troopType.ToString(), StringComparison.OrdinalIgnoreCase))
            return defenseBonusVsTroopTypeValue;
        return 0;
    }

    public int GetRecruitBonusMenAtArms()
    {
        return Math.Max(0, recruitBonusMenAtArms);
    }

    public int GetScryAreaBonus()
    {
        return Math.Max(0, scryAreaBonus);
    }

    public int GetScryArtifactBonus()
    {
        return Math.Max(0, scryArtifactBonus);
    }

    public bool GetNegativeStatusImmunity(StatusEffectEnum effect)
    {
        return !string.IsNullOrWhiteSpace(negativeStatusImmunity)
            && string.Equals(negativeStatusImmunity, effect.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public int GetNegativeStatusDurationReduction()
    {
        return Math.Max(0, negativeStatusDurationReduction);
    }

    public int GetNegativeStatusDamageReduction()
    {
        return Math.Max(0, negativeStatusDamageReduction);
    }

    public int GetPositiveStatusDurationBonus()
    {
        return Math.Max(0, positiveStatusDurationBonus);
    }

    public int GetPositiveStatusEffectBonus()
    {
        return Math.Max(0, positiveStatusEffectBonus);
    }

    // ---- Remaining typed getters ----

    public int GetActionDifficultyReduction(string actionClassName)
    {
        if (scryArtifactBonus > 0 && string.Equals(actionClassName, FindArtifact.ActionRef, StringComparison.OrdinalIgnoreCase))
            return scryArtifactBonus;
        return 0;
    }

    public int GetArmyAttackStrengthBonus()
    {
        return Math.Max(0, armyAttackStrengthBonus);
    }

    public int GetArmyDefenseStrengthBonus()
    {
        return Math.Max(0, armyDefenseStrengthBonus);
    }

    public int GetEnemyArmyDefensePenaltySameHex()
    {
        return Math.Max(0, enemyArmyDefensePenaltySameHex);
    }

    public bool GrantsEnvironmentalImmunity()
    {
        return grantsEnvironmentalImmunity;
    }

    public bool GrantsHasteAtSea()
    {
        return grantsHasteAtSea;
    }

    public Artifact Clone()
    {
        if (this == null) return null;
        return new Artifact
        {
            artifactName = this.artifactName,
            hidden = this.hidden,
            alignment = this.alignment,
            commanderBonus = this.commanderBonus,
            agentBonus = this.agentBonus,
            emmissaryBonus = this.emmissaryBonus,
            mageBonus = this.mageBonus,
            bonusAttack = this.bonusAttack,
            bonusDefense = this.bonusDefense,
            passiveEffectId = this.passiveEffectId,
            passiveEffectValue = this.passiveEffectValue,
            transferable = this.transferable,
            spriteString = this.spriteString,
            healPerTurn = this.healPerTurn,
            movementBonus = this.movementBonus,
            ignoreTerrainMovementPenalty = this.ignoreTerrainMovementPenalty,
            grantsHasteAtSea = this.grantsHasteAtSea,
            autoScoutRadius = this.autoScoutRadius,
            detectionEvasion = this.detectionEvasion,
            attackBonusVsRace = this.attackBonusVsRace,
            attackBonusVsRaceValue = this.attackBonusVsRaceValue,
            attackBonusVsTroopType = this.attackBonusVsTroopType,
            attackBonusVsTroopTypeValue = this.attackBonusVsTroopTypeValue,
            defenseBonusVsRace = this.defenseBonusVsRace,
            defenseBonusVsRaceValue = this.defenseBonusVsRaceValue,
            defenseBonusVsTroopType = this.defenseBonusVsTroopType,
            defenseBonusVsTroopTypeValue = this.defenseBonusVsTroopTypeValue,
            armyAttackStrengthBonus = this.armyAttackStrengthBonus,
            armyDefenseStrengthBonus = this.armyDefenseStrengthBonus,
            enemyArmyDefensePenaltySameHex = this.enemyArmyDefensePenaltySameHex,
            recruitBonusMenAtArms = this.recruitBonusMenAtArms,
            scryAreaBonus = this.scryAreaBonus,
            scryArtifactBonus = this.scryArtifactBonus,
            negativeStatusImmunity = this.negativeStatusImmunity,
            negativeStatusDurationReduction = this.negativeStatusDurationReduction,
            negativeStatusDamageReduction = this.negativeStatusDamageReduction,
            positiveStatusDurationBonus = this.positiveStatusDurationBonus,
            positiveStatusEffectBonus = this.positiveStatusEffectBonus,
            grantsEnvironmentalImmunity = this.grantsEnvironmentalImmunity
        };
    }
}
