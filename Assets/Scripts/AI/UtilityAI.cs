using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// Serialized form of the advisor tuning: scoring weights AIContext uses when
// an advisor ranks a character's playable actions, plus per-action advisor
// ownership overrides (which advisor an action class belongs to).
// Edited via Window > RetroLOTR > AI Widget > Advisors.
// ---------------------------------------------------------------------------

[Serializable]
public class AdvisorWeightEntry
{
    public string key = string.Empty;
    public float value;
}

[Serializable]
public class ActionUtilityParameterModifier
{
    // Must be one of AIUtilityParameters.Known. Empty entries are ignored.
    public string parameter = string.Empty;
    // The named utility value is multiplied by this, then bonus is added.
    public float multiplier = 1f;
    public float bonus;
}

// Which score terms an action opts out of. Default (false) = full formula.
[Serializable]
public struct ActionScoreFlags
{
    public bool ignoreSituation;

    public bool AnySet => ignoreSituation;
}

[Serializable]
public class AdvisorConfigData
{
    public List<AdvisorWeightEntry> weights = new();
    public List<CardAdvisorProfile> cardProfiles = new();
}

// One row per printed card — the unit every Card Board authoring choice is
// keyed against. deckId+cardId is the stable identity (see
// AIAdvisorConfig.BuildCardProfileKey for why reference/injected cards resolve
// to their template's deckId+cardId instead of their own). Two cards that
// happen to share an action class each get their own independent row here —
// the AI Widget's Card Board tab has a "duplicate to sibling cards" action to
// seed one from another, but nothing at runtime ever shares a row.
[Serializable]
public class CardAdvisorProfile
{
    public string deckId = string.Empty;
    public int cardId;

    // Display-only — never read for lookups, so a stale value (e.g. after a
    // card rename) can't silently break anything.
    public string cardName = string.Empty;
    public string actionClass = string.Empty;

    // Empty = keep the advisor coded on the action class.
    public string advisor = string.Empty;
    // Flat score adjustment applied whenever the AI scores this action;
    // lets an action be prioritized over its advisor's other cards.
    public float scoreBonus;
    // Per-action formula composition: true = leave that term out of the score.
    public bool ignoreSituation;
    // Explicit card-side composition of named Advisor utility parameters. These
    // entries are also shown and edited in the AI Widget's Card Board tab.
    public List<ActionUtilityParameterModifier> utilityParameters = new();
}

public class AdvisorWeightDefinition
{
    public readonly string key;
    public readonly float defaultValue;
    public readonly string description;

    public AdvisorWeightDefinition(string key, float defaultValue, string description)
    {
        this.key = key;
        this.defaultValue = defaultValue;
        this.description = description;
    }
}

// The complete public vocabulary shared by Advisors, HTN, and card profiles.
// Values are direct observations from AIContext; they are never inferred from
// a card, and every card-specific contribution is authored in AdvisorConfig.
public static class AIUtilityParameters
{
    public const string MilitaristicEnemyPressure = "Militaristic.EnemyPressure";
    public const string MilitaristicMilitaryEdge = "Militaristic.MilitaryEdge";
    public const string EconomicLiquidWealth = "Economic.LiquidWealth";
    public const string DiplomaticIndirectSafety = "Diplomatic.IndirectSafety";
    public const string IntelligenceEnemyCharacter = "Intelligence.EnemyCharacter";
    public const string IntelligenceIndirectSafety = "Intelligence.IndirectSafety";
    public const string MagicArtifactScarcity = "Magic.ArtifactScarcity";
    public const string MagicArtifactTransfer = "Magic.ArtifactTransfer";
    public const string MagicEnemyPressure = "Magic.EnemyPressure";
    public const string MagicHiddenArtifacts = "Magic.HiddenArtifacts";
    public const string MagicMageStrength = "Magic.MageStrength";
    public const string DiplomaticEnemyPressure = "Diplomatic.EnemyPressure";
    public const string DiplomaticEmissaryStrength = "Diplomatic.EmissaryStrength";
    public const string IntelligenceEnemyPressure = "Intelligence.EnemyPressure";
    public const string IntelligenceAgentStrength = "Intelligence.AgentStrength";
    // Logistics: reposition our own side (renamed from the old, unsplit "Movement") plus healing.
    public const string LogisticsReachNpc = "Logistics.ReachNpc";
    public const string LogisticsInterceptEnemy = "Logistics.InterceptEnemy";
    public const string LogisticsReachEnemyCharacter = "Logistics.ReachEnemyCharacter";
    public const string LogisticsHealingNeed = "Logistics.HealingNeed";

    // Disruption: deny/debuff the enemy (halt, block, negative status) — the other half of the
    // old "Movement" split.
    public const string DisruptionEnemyPressure = "Disruption.EnemyPressure";

    // Target-quality signals (distinct from the proximity/strength terms above): "is there a
    // specific, good target nearby right now", not just "is an advisor generically busy".
    public const string DiplomaticEnemyPcOpportunity = "Diplomatic.EnemyPcOpportunity";
    public const string DiplomaticOwnPcLoyaltyRisk = "Diplomatic.OwnPcLoyaltyRisk";
    public const string IntelligenceEnemyPcVulnerability = "Intelligence.EnemyPcVulnerability";
    public const string IntelligenceHighValueEnemyCharacter = "Intelligence.HighValueEnemyCharacter";

    // Second wave: closes the remaining gaps in each advisor's stated purpose (spells for
    // Magic, fortification for Militaristic, NPL recruitment for Diplomatic) rather than just
    // proximity/strength math.
    public const string MagicSpellOpportunity = "Magic.SpellOpportunity";
    public const string MilitaristicOwnPcFortificationNeed = "Militaristic.OwnPcFortificationNeed";
    public const string DiplomaticNplRecruitment = "Diplomatic.NplRecruitment";

    // Same proximity-to-an-undefended-own-PC signal as MilitaristicOwnPcFortificationNeed above
    // (identical formula), under a distinct name so root.danger.pick's fortify and conscript
    // leaves can each target their own card family (FortifyPC vs. ConscriptArmy/TrainArmy/Block)
    // via PreferredParameters independently of one another.
    public const string MilitaristicOwnPcDefenderNeed = "Militaristic.OwnPcDefenderNeed";

    // Third wave: per-material stockpile balancing for Economic. One Insufficient/Surplus pair
    // per tradeable ProducesEnum material, each driving that material's own Buy{X}/Sell{X}
    // cards. Gold has no Buy/Sell card of its own — its Insufficient/Surplus instead bias every
    // Sell{X}/Buy{X} card respectively (sell anything to raise cash; spend excess cash on
    // anything), reusing the existing Economy.CriticalBelow/StableBelow thresholds rather than
    // inventing new ones.
    public const string EconomicMithrilInsufficient = "Economic.MithrilInsufficient";
    public const string EconomicMithrilSurplus = "Economic.MithrilSurplus";
    public const string EconomicSteelInsufficient = "Economic.SteelInsufficient";
    public const string EconomicSteelSurplus = "Economic.SteelSurplus";
    public const string EconomicIronInsufficient = "Economic.IronInsufficient";
    public const string EconomicIronSurplus = "Economic.IronSurplus";
    public const string EconomicMountsInsufficient = "Economic.MountsInsufficient";
    public const string EconomicMountsSurplus = "Economic.MountsSurplus";
    public const string EconomicTimberInsufficient = "Economic.TimberInsufficient";
    public const string EconomicTimberSurplus = "Economic.TimberSurplus";
    public const string EconomicLeatherInsufficient = "Economic.LeatherInsufficient";
    public const string EconomicLeatherSurplus = "Economic.LeatherSurplus";
    public const string EconomicGoldInsufficient = "Economic.GoldInsufficient";
    public const string EconomicGoldSurplus = "Economic.GoldSurplus";

    public static readonly IReadOnlyList<string> Known = new[]
    {
        MilitaristicEnemyPressure, MilitaristicMilitaryEdge, EconomicLiquidWealth,
        DiplomaticIndirectSafety,
        IntelligenceEnemyCharacter, IntelligenceIndirectSafety,
        MagicArtifactScarcity, MagicArtifactTransfer, MagicEnemyPressure, MagicHiddenArtifacts, MagicMageStrength,
        DiplomaticEnemyPressure, DiplomaticEmissaryStrength, IntelligenceEnemyPressure, IntelligenceAgentStrength,
        LogisticsReachNpc, LogisticsInterceptEnemy, LogisticsReachEnemyCharacter, LogisticsHealingNeed, DisruptionEnemyPressure,
        DiplomaticEnemyPcOpportunity, DiplomaticOwnPcLoyaltyRisk, IntelligenceEnemyPcVulnerability, IntelligenceHighValueEnemyCharacter,
        MagicSpellOpportunity, MilitaristicOwnPcFortificationNeed, DiplomaticNplRecruitment, MilitaristicOwnPcDefenderNeed,
        EconomicMithrilInsufficient, EconomicMithrilSurplus, EconomicSteelInsufficient, EconomicSteelSurplus,
        EconomicIronInsufficient, EconomicIronSurplus, EconomicMountsInsufficient, EconomicMountsSurplus,
        EconomicTimberInsufficient, EconomicTimberSurplus, EconomicLeatherInsufficient, EconomicLeatherSurplus,
        EconomicGoldInsufficient, EconomicGoldSurplus
    };

    public static bool IsKnown(string parameter) => !string.IsNullOrWhiteSpace(parameter)
        && Known.Contains(parameter, StringComparer.OrdinalIgnoreCase);
}

public static class AIAdvisorConfig
{
    public const string ResourcePath = "AI/AdvisorConfig";

    public static class Keys
    {
        public const string HTNBiasBonus = "Global.HTNBiasBonus";
        // Extra nudge (on top of HTNBiasBonus) for a card whose own utility-parameter profile
        // already uses one of the active HTN leaf's PreferredParameters — "this card's authored
        // profile already targets this exact situation", not just "this card's advisor matches".
        public const string HTNSituationBonus = "Global.HTNSituationBonus";

        // Single axis: Leader.goldAmount + resources held valued at current market sell
        // price (AIContext.CalculateLiquidWealth) — this game has no passive per-turn
        // income of any kind, so there is no second "income" axis to threshold against.
        public const string EconomyCriticalBelow = "Economy.CriticalBelow";
        public const string EconomyWeakBelow = "Economy.WeakBelow";
        public const string EconomyStableBelow = "Economy.StableBelow";

        public const string EnemyProximityMax = "Targeting.EnemyProximityMax";
        public const string NeutralTargetExtraDistance = "Targeting.NeutralTargetExtraDistance";

        public const string NoArmyPenalty = "Militaristic.NoArmyPenalty";
        public const string FarTargetPenalty = "Militaristic.FarTargetPenalty";
        public const string MilitaristicViabilityThreshold = "Militaristic.ViabilityThreshold";
        public const string OutmatchedStrengthRatio = "Militaristic.OutmatchedStrengthRatio";

        public const string IntelligenceOutmatchedBonus = "Intelligence.OutmatchedBonus";
        public const string EnemyCharacterProximityMax = "Intelligence.EnemyCharacterProximityMax";
        public const string IntelligenceViabilityThreshold = "Intelligence.ViabilityThreshold";

        public const string ArtifactScarcityWeight = "Magic.ArtifactScarcityWeight";
        public const string MagicViabilityThreshold = "Magic.ViabilityThreshold";

        public const string DiplomaticOutmatchedBonus = "Diplomatic.OutmatchedBonus";
        public const string NpcProximityMax = "Diplomatic.NpcProximityMax";
        public const string DiplomaticViabilityThreshold = "Diplomatic.ViabilityThreshold";

        public const string LogisticsProximityMax = "Logistics.ProximityMax";
        public const string LogisticsDistancePenaltyPerHex = "Logistics.DistancePenaltyPerHex";
        public const string LogisticsViabilityThreshold = "Logistics.ViabilityThreshold";

        public const string DisruptionViabilityThreshold = "Disruption.ViabilityThreshold";
        public const string DisruptionEnemyPressureThreshold = "Disruption.EnemyPressureThreshold";

        public const string DiplomaticIndirectSafetyThreshold = "Diplomatic.IndirectSafetyThreshold";
        public const string IntelligenceEnemyCharacterThreshold = "Intelligence.EnemyCharacterThreshold";
        public const string IntelligenceIndirectSafetyThreshold = "Intelligence.IndirectSafetyThreshold";
        public const string MagicArtifactScarcityThreshold = "Magic.ArtifactScarcityThreshold";
        public const string MagicArtifactTransferThreshold = "Magic.ArtifactTransferThreshold";
        public const string MagicEnemyPressureThreshold = "Magic.EnemyPressureThreshold";
        public const string LogisticsReachNpcThreshold = "Logistics.ReachNpcThreshold";
        public const string LogisticsInterceptEnemyThreshold = "Logistics.InterceptEnemyThreshold";
        public const string LogisticsReachEnemyCharacterThreshold = "Logistics.ReachEnemyCharacterThreshold";
        public const string LogisticsHealingNeedHealthBelow = "Logistics.HealingNeedHealthBelow";
        public const string LogisticsHealingNeedThreshold = "Logistics.HealingNeedThreshold";
        public const string MagicHiddenArtifactsWeight = "Magic.HiddenArtifactsWeight";
        public const string MagicMageStrengthWeight = "Magic.MageStrengthWeight";
        public const string DiplomaticEnemyPressureWeight = "Diplomatic.EnemyPressureWeight";
        public const string DiplomaticEmissaryStrengthWeight = "Diplomatic.EmissaryStrengthWeight";
        public const string IntelligenceEnemyPressureWeight = "Intelligence.EnemyPressureWeight";
        public const string IntelligenceAgentStrengthWeight = "Intelligence.AgentStrengthWeight";

        // Target-quality signals: is there a specific good target nearby, not just "is this
        // advisor generically busy". Each pairs a "what counts as a target" threshold with a
        // proximity-falloff window, following the *ProximityMax / *Threshold convention above.
        public const string DiplomaticEnemyPcLoyaltyBelow = "Diplomatic.EnemyPcLoyaltyBelow";
        public const string DiplomaticEnemyPcOpportunityProximityMax = "Diplomatic.EnemyPcOpportunityProximityMax";
        public const string DiplomaticEnemyPcOpportunityThreshold = "Diplomatic.EnemyPcOpportunityThreshold";

        public const string DiplomaticOwnPcLoyaltyBelow = "Diplomatic.OwnPcLoyaltyBelow";
        public const string DiplomaticOwnPcLoyaltyRiskProximityMax = "Diplomatic.OwnPcLoyaltyRiskProximityMax";
        public const string DiplomaticOwnPcLoyaltyRiskThreshold = "Diplomatic.OwnPcLoyaltyRiskThreshold";

        public const string IntelligenceEnemyPcDefenseBelow = "Intelligence.EnemyPcDefenseBelow";
        public const string IntelligenceEnemyPcVulnerabilityProximityMax = "Intelligence.EnemyPcVulnerabilityProximityMax";
        public const string IntelligenceEnemyPcVulnerabilityThreshold = "Intelligence.EnemyPcVulnerabilityThreshold";

        public const string IntelligenceHighValueSkillAtLeast = "Intelligence.HighValueSkillAtLeast";
        public const string IntelligenceHighValueEnemyCharacterProximityMax = "Intelligence.HighValueEnemyCharacterProximityMax";
        public const string IntelligenceHighValueEnemyCharacterThreshold = "Intelligence.HighValueEnemyCharacterThreshold";

        // Second wave: Magic gets a spell-casting signal (previously artifact-only), Militaristic
        // gets a fortification-need signal (previously combat-only), Diplomatic gets an
        // NPL-recruitment-eligibility signal (previously discovery-only). None of these are
        // folded into GetAdvisorViability — same precedent as Magic.ArtifactTransfer: available
        // as a direct utility parameter and an HTN "Ready" predicate for sub-branching once
        // already in that advisor's territory, without widening the outer viability gate.
        public const string MagicSpellOpportunityThreshold = "Magic.SpellOpportunityThreshold";

        public const string MilitaristicOwnPcDefenseBelow = "Militaristic.OwnPcDefenseBelow";
        public const string MilitaristicOwnPcFortificationProximityMax = "Militaristic.OwnPcFortificationProximityMax";
        public const string MilitaristicOwnPcFortificationNeedThreshold = "Militaristic.OwnPcFortificationNeedThreshold";

        public const string DiplomaticNplRecruitmentProximityMax = "Diplomatic.NplRecruitmentProximityMax";
        public const string DiplomaticNplRecruitmentThreshold = "Diplomatic.NplRecruitmentThreshold";

        // Third wave: per-material stockpile thresholds (Leader's own resource amounts, not
        // market stock). Starting points only, not balance-tuned — Mithril is tighter since
        // it's the rarest material (StoresManager.MithrilSellValue/ReferenceStock mark it so).
        public const string EconomicMithrilInsufficientBelow = "Economic.MithrilInsufficientBelow";
        public const string EconomicMithrilSurplusAbove = "Economic.MithrilSurplusAbove";
        public const string EconomicSteelInsufficientBelow = "Economic.SteelInsufficientBelow";
        public const string EconomicSteelSurplusAbove = "Economic.SteelSurplusAbove";
        public const string EconomicIronInsufficientBelow = "Economic.IronInsufficientBelow";
        public const string EconomicIronSurplusAbove = "Economic.IronSurplusAbove";
        public const string EconomicMountsInsufficientBelow = "Economic.MountsInsufficientBelow";
        public const string EconomicMountsSurplusAbove = "Economic.MountsSurplusAbove";
        public const string EconomicTimberInsufficientBelow = "Economic.TimberInsufficientBelow";
        public const string EconomicTimberSurplusAbove = "Economic.TimberSurplusAbove";
        public const string EconomicLeatherInsufficientBelow = "Economic.LeatherInsufficientBelow";
        public const string EconomicLeatherSurplusAbove = "Economic.LeatherSurplusAbove";
    }

    public static readonly IReadOnlyList<AdvisorWeightDefinition> KnownWeights = new List<AdvisorWeightDefinition>
    {
        new(Keys.HTNBiasBonus, 4f, "Flat score bonus for cards whose advisor matches the HTN strategy's currently-active task."),
        new(Keys.HTNSituationBonus, 3f, "Extra score bonus (stacks with HTNBiasBonus) for a card whose own utility-parameter profile already uses one of the active HTN leaf's preferred parameters — a smaller, tie-breaking nudge on top of the underlying situational math, not the primary signal."),

        // Fresh thresholds against a fresh metric (liquid wealth) — these are a starting
        // point, not tuned against real playtested economies. Expect to retune.
        new(Keys.EconomyCriticalBelow, 20f, "Economy is Critical when gold + resource net worth (at current market price) is below this."),
        new(Keys.EconomyWeakBelow, 60f, "Economy is Weak (not Critical) when gold + resource net worth is below this."),
        new(Keys.EconomyStableBelow, 150f, "Economy is Stable (not Critical or Weak) when gold + resource net worth is below this; above it, Surplus."),

        new(Keys.EnemyProximityMax, 10f, "Enemy-proximity bonus at distance 0; fades by 1 per hex."),
        new(Keys.NeutralTargetExtraDistance, 2f, "Neutral targets count as this many hexes farther away."),

        new(Keys.NoArmyPenalty, -4f, "Militaristic score adjustment when the character leads no army."),
        new(Keys.FarTargetPenalty, 1.5f, "Militaristic penalty when the enemy target is more than 1 hex away."),
        new(Keys.MilitaristicViabilityThreshold, 0f, "HTN switches to a Militaristic strategy once Militaristic's viability (enemy proximity + army edge, same terms as its situational score above) crosses this."),
        new(Keys.OutmatchedStrengthRatio, 1.1f, "The nearest enemy must be at least this many times my army's strength (0 while leading no army) to count as \"outmatched\" — the single definition Militaristic.Danger and the Intelligence/Diplomatic outmatched bonuses below all read."),

        new(Keys.IntelligenceOutmatchedBonus, 3f, "Intelligence bonus when the army is outmatched (indirect approach)."),
        new(Keys.EnemyCharacterProximityMax, 6f, "Intelligence bonus when an enemy character is at distance 0; fades by 1 per hex."),
        new(Keys.IntelligenceViabilityThreshold, 0f, "HTN switches to an Intelligence strategy once Intelligence's viability (same terms as its situational score above) crosses this."),

        new(Keys.ArtifactScarcityWeight, 2f, "Magic bonus scale for how few artifacts the nation owns (0..1 scarcity times this)."),
        new(Keys.MagicViabilityThreshold, 0f, "HTN switches to a Magic strategy once Magic's viability (same terms as its situational score above) crosses this."),

        new(Keys.DiplomaticOutmatchedBonus, 2f, "Diplomatic bonus when the army is outmatched (indirect approach)."),
        new(Keys.NpcProximityMax, 10f, "Diplomatic bonus when an unrevealed NPC is at distance 0; fades by 1 per hex."),
        new(Keys.DiplomaticViabilityThreshold, 0f, "HTN switches to a Diplomatic strategy once Diplomatic's viability (same terms as its situational score above) crosses this."),

        new(Keys.LogisticsProximityMax, 8f, "Logistics bonus at distance 0 from the preferred destination."),
        new(Keys.LogisticsDistancePenaltyPerHex, 2f, "How fast the Logistics destination bonus fades per hex of distance."),
        new(Keys.LogisticsViabilityThreshold, 0f, "HTN switches to a Logistics strategy once Logistics's viability (same terms as its situational score above) crosses this."),

        new(Keys.DisruptionViabilityThreshold, 0f, "HTN switches to a Disruption strategy once Disruption's viability (enemy proximity — is there someone nearby to halt/block/debuff) crosses this."),

        new(Keys.DiplomaticIndirectSafetyThreshold, 0f, "HTN threshold for direct Diplomatic.IndirectSafety."),
        new(Keys.IntelligenceEnemyCharacterThreshold, 0f, "HTN threshold for direct Intelligence.EnemyCharacter."),
        new(Keys.IntelligenceIndirectSafetyThreshold, 0f, "HTN threshold for direct Intelligence.IndirectSafety."),
        new(Keys.MagicArtifactScarcityThreshold, 0f, "HTN threshold for direct Magic.ArtifactScarcity."),
        new(Keys.MagicArtifactTransferThreshold, 0f, "HTN threshold for direct Magic.ArtifactTransfer."),
        new(Keys.MagicEnemyPressureThreshold, 0f, "HTN threshold for direct Magic.EnemyPressure."),
        new(Keys.LogisticsReachNpcThreshold, 0f, "HTN threshold for direct Logistics.ReachNpc."),
        new(Keys.LogisticsInterceptEnemyThreshold, 0f, "HTN threshold for direct Logistics.InterceptEnemy."),
        new(Keys.LogisticsReachEnemyCharacterThreshold, 0f, "HTN threshold for direct Logistics.ReachEnemyCharacter."),
        new(Keys.LogisticsHealingNeedHealthBelow, 70f, "An allied character in this hex counts as needing healing when their health is below this (Character.health, 0-100)."),
        new(Keys.LogisticsHealingNeedThreshold, 0f, "HTN threshold for direct Logistics.HealingNeed (count of wounded allies in this hex)."),
        new(Keys.DisruptionEnemyPressureThreshold, 0f, "HTN threshold for direct Disruption.EnemyPressure."),
        new(Keys.MagicHiddenArtifactsWeight, 1f, "Magic viability per hidden artifact still on the map."),
        new(Keys.MagicMageStrengthWeight, 0.5f, "Magic viability per total active Mage level under this leader."),
        new(Keys.DiplomaticEnemyPressureWeight, 1f, "Diplomatic viability multiplier for enemy proximity."),
        new(Keys.DiplomaticEmissaryStrengthWeight, 0.5f, "Diplomatic viability per total active Emissary level under this leader."),
        new(Keys.IntelligenceEnemyPressureWeight, 1f, "Intelligence viability multiplier for enemy proximity."),
        new(Keys.IntelligenceAgentStrengthWeight, 0.5f, "Intelligence viability per total active Agent level under this leader."),

        new(Keys.DiplomaticEnemyPcLoyaltyBelow, 50f, "An enemy-owned PC counts as an influence-out opportunity when its loyalty is below this (PC.loyalty, 0-100)."),
        new(Keys.DiplomaticEnemyPcOpportunityProximityMax, 10f, "Diplomatic.EnemyPcOpportunity bonus at distance 0 from the nearest qualifying enemy PC; fades by 1 per hex."),
        new(Keys.DiplomaticEnemyPcOpportunityThreshold, 0f, "HTN threshold for direct Diplomatic.EnemyPcOpportunity."),

        new(Keys.DiplomaticOwnPcLoyaltyBelow, 40f, "One of this leader's own PCs counts as an influence-up risk when its loyalty is below this."),
        new(Keys.DiplomaticOwnPcLoyaltyRiskProximityMax, 10f, "Diplomatic.OwnPcLoyaltyRisk bonus at distance 0 from the nearest at-risk own PC; fades by 1 per hex."),
        new(Keys.DiplomaticOwnPcLoyaltyRiskThreshold, 0f, "HTN threshold for direct Diplomatic.OwnPcLoyaltyRisk."),

        new(Keys.IntelligenceEnemyPcDefenseBelow, 5f, "An enemy-owned PC counts as a sabotage/theft target when its PC.GetDefense() is below this."),
        new(Keys.IntelligenceEnemyPcVulnerabilityProximityMax, 8f, "Intelligence.EnemyPcVulnerability bonus at distance 0 from the nearest qualifying enemy PC; fades by 1 per hex."),
        new(Keys.IntelligenceEnemyPcVulnerabilityThreshold, 0f, "HTN threshold for direct Intelligence.EnemyPcVulnerability."),

        // 6, not 4: card-authored skill requirements across the deck range 1-5 per role (median
        // ~1-2), so a single near-maxed specialist alone would already clear a threshold of 4 —
        // too low a bar for "distinctly high-value". 6 requires either two solid skills or one
        // skill pushed further than any single card demands, i.e. a genuine multi-talented target.
        new(Keys.IntelligenceHighValueSkillAtLeast, 6f, "An enemy character counts as a high-value assassination/kidnap target when their Commander+Agent+Emmissary+Mage sum is at least this."),
        new(Keys.IntelligenceHighValueEnemyCharacterProximityMax, 8f, "Intelligence.HighValueEnemyCharacter bonus at distance 0 from the nearest qualifying enemy character; fades by 1 per hex."),
        new(Keys.IntelligenceHighValueEnemyCharacterThreshold, 0f, "HTN threshold for direct Intelligence.HighValueEnemyCharacter."),

        new(Keys.MagicSpellOpportunityThreshold, 0f, "HTN threshold for direct Magic.SpellOpportunity (count of currently-playable Spell actions)."),

        new(Keys.MilitaristicOwnPcDefenseBelow, 6f, "One of this leader's own PCs counts as needing fortification when its PC.GetDefense() is below this."),
        new(Keys.MilitaristicOwnPcFortificationProximityMax, 10f, "Militaristic.OwnPcFortificationNeed bonus at distance 0 from the nearest under-fortified own PC; fades by 1 per hex."),
        new(Keys.MilitaristicOwnPcFortificationNeedThreshold, 0f, "HTN threshold for direct Militaristic.OwnPcFortificationNeed."),

        new(Keys.DiplomaticNplRecruitmentProximityMax, 10f, "Diplomatic.NplRecruitment bonus at distance 0 from the nearest NPL capital eligible for StateAllegiance (AFriendOrThree) recruitment; fades by 1 per hex."),
        new(Keys.DiplomaticNplRecruitmentThreshold, 0f, "HTN threshold for direct Diplomatic.NplRecruitment."),

        // Third wave: per-material stockpile thresholds against Leader's own resource amounts
        // (not market stock). Starting points only, not balance-tuned. Mithril is tighter since
        // it's the rarest material (StoresManager.MithrilSellValue=7, ReferenceStock=10 vs. 25
        // for the rest).
        new(Keys.EconomicMithrilInsufficientBelow, 5f, "Economic.MithrilInsufficient rises when stored mithril falls below this."),
        new(Keys.EconomicMithrilSurplusAbove, 15f, "Economic.MithrilSurplus rises when stored mithril exceeds this."),
        new(Keys.EconomicSteelInsufficientBelow, 10f, "Economic.SteelInsufficient rises when stored steel falls below this."),
        new(Keys.EconomicSteelSurplusAbove, 30f, "Economic.SteelSurplus rises when stored steel exceeds this."),
        new(Keys.EconomicIronInsufficientBelow, 10f, "Economic.IronInsufficient rises when stored iron falls below this."),
        new(Keys.EconomicIronSurplusAbove, 30f, "Economic.IronSurplus rises when stored iron exceeds this."),
        new(Keys.EconomicMountsInsufficientBelow, 10f, "Economic.MountsInsufficient rises when stored mounts falls below this."),
        new(Keys.EconomicMountsSurplusAbove, 30f, "Economic.MountsSurplus rises when stored mounts exceeds this."),
        new(Keys.EconomicTimberInsufficientBelow, 10f, "Economic.TimberInsufficient rises when stored timber falls below this."),
        new(Keys.EconomicTimberSurplusAbove, 30f, "Economic.TimberSurplus rises when stored timber exceeds this."),
        new(Keys.EconomicLeatherInsufficientBelow, 10f, "Economic.LeatherInsufficient rises when stored leather falls below this."),
        new(Keys.EconomicLeatherSurplusAbove, 30f, "Economic.LeatherSurplus rises when stored leather exceeds this."),
    };

    private static Dictionary<string, float> defaultsByKey;
    private static Dictionary<string, float> loadedWeights;
    private static Dictionary<string, CardAdvisorProfile> loadedCardProfiles;
    private static bool loaded;

    public static void Reload()
    {
        loadedWeights = null;
        loadedCardProfiles = null;
        loaded = false;
    }

    public static float GetWeight(string key)
    {
        EnsureLoaded();
        if (loadedWeights != null && loadedWeights.TryGetValue(key, out float value)) return value;
        return GetDefaultWeight(key);
    }

    public static float GetDefaultWeight(string key)
    {
        defaultsByKey ??= KnownWeights.ToDictionary(d => d.key, d => d.defaultValue, StringComparer.OrdinalIgnoreCase);
        return defaultsByKey.TryGetValue(key, out float value) ? value : 0f;
    }

    // The advisor an action belongs to for AI decision-making: authored
    // override first, then the action's own default.
    public static AdvisorType ResolveAdvisor(CharacterAction action)
    {
        if (action == null) return AdvisorType.None;
        EnsureLoaded();
        if (TryGetProfile(action.card, out CardAdvisorProfile profile)
            && !string.IsNullOrWhiteSpace(profile.advisor)
            && Enum.TryParse(profile.advisor, true, out AdvisorType overridden))
        {
            return overridden;
        }
        return action.GetAdvisorType();
    }

    // Flat, user-authored score adjustment for this action (0 when unset).
    public static float GetActionScoreBonus(CharacterAction action)
    {
        if (action == null) return 0f;
        EnsureLoaded();
        return TryGetProfile(action.card, out CardAdvisorProfile profile) ? profile.scoreBonus : 0f;
    }

    // Single source of truth for how liquid wealth (gold + resources at current market
    // price — see AIContext.CalculateLiquidWealth) maps to an economy status. This game has
    // no passive per-turn income to threshold against, so there is only one axis. Thresholds
    // are editable in the AI Widget (Economic tab).
    public static EconomyStatus EvaluateEconomyStatus(float liquidWealth)
    {
        if (liquidWealth < GetWeight(Keys.EconomyCriticalBelow)) return EconomyStatus.Critical;
        if (liquidWealth < GetWeight(Keys.EconomyWeakBelow)) return EconomyStatus.Weak;
        if (liquidWealth < GetWeight(Keys.EconomyStableBelow)) return EconomyStatus.Stable;
        return EconomyStatus.Surplus;
    }

    // Which formula terms this action ignores (default = none, full formula).
    public static ActionScoreFlags GetActionScoreFlags(CharacterAction action)
    {
        if (action == null) return default;
        EnsureLoaded();
        return TryGetProfile(action.card, out CardAdvisorProfile profile)
            ? new ActionScoreFlags { ignoreSituation = profile.ignoreSituation }
            : default;
    }

    // Card-side, fully authored modifiers. No implicit action-specific utility
    // exists: a contribution can only be present if it appears in this list.
    public static IReadOnlyList<ActionUtilityParameterModifier> GetActionUtilityParameters(CharacterAction action)
    {
        if (action == null) return Array.Empty<ActionUtilityParameterModifier>();
        EnsureLoaded();
        return TryGetProfile(action.card, out CardAdvisorProfile profile) && profile.utilityParameters?.Count > 0
            ? profile.utilityParameters
            : Array.Empty<ActionUtilityParameterModifier>();
    }

    private static bool TryGetProfile(CardData card, out CardAdvisorProfile profile)
    {
        profile = null;
        string key = BuildCardProfileKey(card);
        return !string.IsNullOrEmpty(key) && loadedCardProfiles != null && loadedCardProfiles.TryGetValue(key, out profile);
    }

    // The canonical per-printed-card identity. Injected reference cards (Land/starting-PC
    // cards DeckManager auto-duplicates into every subdeck that lacks one, see
    // InjectMissingStartingPcAndLandReferences) carry their own local deckId/cardId but point
    // back at one canonical template via referenceDeckId/referenceCardId — a profile authored
    // once must resolve through that template, or it would only ever apply to the one deck
    // whose injected clone happened to share the template's numbering.
    public static string BuildCardProfileKey(CardData card)
    {
        if (card == null) return string.Empty;
        bool isReference = !string.IsNullOrWhiteSpace(card.referenceDeckId) && card.referenceCardId > 0;
        return BuildCardProfileKey(isReference ? card.referenceDeckId : card.deckId, isReference ? card.referenceCardId : card.cardId);
    }

    public static string BuildCardProfileKey(string deckId, int cardId)
        => string.IsNullOrWhiteSpace(deckId) || cardId <= 0 ? string.Empty : $"{deckId.Trim().ToLowerInvariant()}::{cardId}";

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        loadedWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        loadedCardProfiles = new Dictionary<string, CardAdvisorProfile>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

        AdvisorConfigData data = null;
        try { data = JsonUtility.FromJson<AdvisorConfigData>(asset.text); }
        catch (Exception e)
        {
            Debug.LogWarning($"AIAdvisorConfig: could not parse {ResourcePath}.json — using default weights. {e.Message}");
            return;
        }

        if (data?.weights != null)
        {
            foreach (AdvisorWeightEntry entry in data.weights)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                loadedWeights[entry.key] = entry.value;
            }
        }

        if (data?.cardProfiles != null)
        {
            foreach (CardAdvisorProfile entry in data.cardProfiles)
            {
                string key = BuildCardProfileKey(entry?.deckId, entry?.cardId ?? 0);
                if (string.IsNullOrEmpty(key)) continue;

                List<ActionUtilityParameterModifier> valid = entry.utilityParameters?
                    .Where(p => p != null && AIUtilityParameters.IsKnown(p.parameter))
                    .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                    .ToList() ?? new List<ActionUtilityParameterModifier>();

                AdvisorType parsedAdvisor = AdvisorType.None;
                bool hasAdvisorOverride = !string.IsNullOrWhiteSpace(entry.advisor) && Enum.TryParse(entry.advisor, true, out parsedAdvisor);

                loadedCardProfiles[key] = new CardAdvisorProfile
                {
                    deckId = entry.deckId,
                    cardId = entry.cardId,
                    cardName = entry.cardName,
                    actionClass = entry.actionClass,
                    advisor = hasAdvisorOverride ? parsedAdvisor.ToString() : string.Empty,
                    scoreBonus = entry.scoreBonus,
                    ignoreSituation = entry.ignoreSituation,
                    utilityParameters = valid
                };
            }
        }
    }
}
