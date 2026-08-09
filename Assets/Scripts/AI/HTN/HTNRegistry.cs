using System;
using System.Collections.Generic;
using System.Linq;

// ---------------------------------------------------------------------------
// Named predicate vocabulary the authored Strategies can reference — used for
// both Method preconditions and PrimitiveTask preconditions/completion
// conditions (one dictionary, two roles).
//
// Every predicate besides Economic's (tiered) and Global's (unconditional) is a
// threshold on AIContext.GetAdvisorViability(advisor) — the SAME formula, using
// the SAME weights, that AIContext.ScoreAction adds as an advisor's situational
// bonus when scoring a real card. There is no separate hand-coded sensing layer:
// the Utility AI's own assessment of "how good is this advisor right now" is
// literally what drives the HTN's strategy choice. Change a weight in an
// advisor's group in the Advisors tab (e.g. Militaristic.NoArmyPenalty) and both
// the card scores AND the HTN condition that reads it move together.
//
// Keys are namespaced by the Advisor they're about, the same dotted-group
// convention AIAdvisorConfig.Keys already uses for scoring weights — Global.*
// holds the two advisor-agnostic predicates. Every entry carries a plain-English
// Description so the Strategies tab (and the Advisors tab's per-advisor profile)
// can show what a condition actually means without anyone having to read this file.
//
// Add entries here to make new predicates available in the AI Widget dropdowns.
// ---------------------------------------------------------------------------

public class HTNPredicateDefinition
{
    public readonly string Key;
    public readonly string Description;
    public readonly Func<AIContext, AIBlackboard, bool> Predicate;

    public HTNPredicateDefinition(string key, string description, Func<AIContext, AIBlackboard, bool> predicate)
    {
        Key = key;
        Description = description;
        Predicate = predicate;
    }
}

public static class HTNRegistry
{
    // Generic boolean-OR combinator over other predicates. This is what "OR" means at the HTN
    // level: a Method's own precondition can be built from more than one term — e.g.
    // "Economic.Critical OR Economic.Weak" authored directly on that Method's row — composed
    // here at load time, never as a hand-coded boolean property on AIContext (that would put
    // decision logic in scoring code) and never as a bespoke named alias predicate either
    // (that would just hide the same OR behind an extra layer of indirection).
    public static Func<AIContext, AIBlackboard, bool> Or(params Func<AIContext, AIBlackboard, bool>[] predicates)
        => (ctx, bb) => predicates.Any(p => p(ctx, bb));

    public static readonly IReadOnlyList<HTNPredicateDefinition> KnownPredicates = new List<HTNPredicateDefinition>
    {
        new("Global.Always", "Always true. Use for an unconditional fallback branch (usually the last Method under a CompoundTask).",
            (ctx, bb) => true),
        new("Global.Never", "Always false. Use on a PrimitiveTask that should never auto-advance — it only ever leaves via a higher-priority interrupt or its own precondition breaking.",
            (ctx, bb) => false),

        new("Economic.Critical", "True when gold income per turn is below Economy.CriticalIncomeBelow, or stored gold is below Economy.CriticalGoldBelow.",
            (ctx, bb) => ctx.EconomyStatus == EconomyStatus.Critical),
        new("Economic.Weak", "True when not Critical, and income per turn is at or below Economy.WeakIncomeAtMost, or stored gold is below Economy.WeakGoldBelow.",
            (ctx, bb) => ctx.EconomyStatus == EconomyStatus.Weak),
        new("Economic.Stable", "True when not Critical or Weak, and income per turn is at or below Economy.StableIncomeAtMost.",
            (ctx, bb) => ctx.EconomyStatus == EconomyStatus.Stable),
        new("Economic.Surplus", "True when income per turn is comfortably above Economy.StableIncomeAtMost — the best tier.",
            (ctx, bb) => ctx.EconomyStatus == EconomyStatus.Surplus),

        new("Militaristic.EnemyNear", "True when the nearest non-neutral enemy PC or army is within Targeting.EnemyProximityMax hexes — proximity alone, independent of who would win a fight.",
            (ctx, bb) => ctx.IsEnemyNear),
        new("Militaristic.Danger", "True when an enemy is near (Militaristic.EnemyNear) AND that enemy outguns this leader's army by Militaristic.OutmatchedStrengthRatio — the same outmatched signal the Intelligence/Diplomatic outmatched bonuses already use.",
            (ctx, bb) => ctx.IsEnemyNear && ctx.IsOutmatched),
        new("Militaristic.Viable", "True when Militaristic's viability (enemy proximity + army edge — the same weights shown in the Militaristic group of the Advisors tab) is above Militaristic.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Militaristic) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MilitaristicViabilityThreshold)),

        new("Diplomatic.Viable", "True when Diplomatic's viability (NPC proximity + outmatched bonus — the same weights shown in the Diplomatic group of the Advisors tab) is above Diplomatic.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Diplomatic) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticViabilityThreshold)),

        new("Intelligence.Viable", "True when Intelligence's viability (enemy-character proximity + outmatched bonus — the same weights shown in the Intelligence group of the Advisors tab) is above Intelligence.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Intelligence) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceViabilityThreshold)),

        new("Magic.Viable", "True when Magic's viability (artifact scarcity + enemy proximity — the same weights shown in the Magic group of the Advisors tab) is above Magic.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Magic) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicViabilityThreshold)),

        new("Logistics.Viable", "True when Logistics's viability (proximity to the preferred destination — the same weights shown in the Logistics group of the Advisors tab) is above Logistics.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Logistics) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsViabilityThreshold)),

        new("Disruption.Viable", "True when Disruption's viability (enemy proximity — is there someone nearby to halt/block/debuff) is above Disruption.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Disruption) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DisruptionViabilityThreshold)),
        // Fine-grained predicates consume direct, named Advisor parameters only.
        // Each threshold is an authored Advisor weight, so HTN has no hidden score.
        new("Diplomatic.IndirectSafetyReady", "Direct Diplomatic.IndirectSafety is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.DiplomaticIndirectSafety) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticIndirectSafetyThreshold)),
        new("Intelligence.EnemyCharacterReady", "Direct Intelligence.EnemyCharacter is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.IntelligenceEnemyCharacter) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceEnemyCharacterThreshold)),
        new("Intelligence.IndirectSafetyReady", "Direct Intelligence.IndirectSafety is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.IntelligenceIndirectSafety) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceIndirectSafetyThreshold)),
        new("Magic.ArtifactScarcityReady", "Direct Magic.ArtifactScarcity is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.MagicArtifactScarcity) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicArtifactScarcityThreshold)),
        new("Magic.ArtifactTransferReady", "Direct Magic.ArtifactTransfer is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.MagicArtifactTransfer) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicArtifactTransferThreshold)),
        new("Magic.EnemyPressureReady", "Direct Magic.EnemyPressure is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.MagicEnemyPressure) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicEnemyPressureThreshold)),
        new("Logistics.ReachNpcReady", "Direct Logistics.ReachNpc is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.LogisticsReachNpc) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsReachNpcThreshold)),
        new("Logistics.InterceptEnemyReady", "Direct Logistics.InterceptEnemy is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.LogisticsInterceptEnemy) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsInterceptEnemyThreshold)),
        new("Logistics.ReachEnemyCharacterReady", "Direct Logistics.ReachEnemyCharacter is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.LogisticsReachEnemyCharacter) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsReachEnemyCharacterThreshold)),
        new("Logistics.HealingNeedReady", "Direct Logistics.HealingNeed is above its authored threshold — a wounded ally shares this character's hex.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.LogisticsHealingNeed) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsHealingNeedThreshold)),
        new("Disruption.EnemyPressureReady", "Direct Disruption.EnemyPressure is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.DisruptionEnemyPressure) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DisruptionEnemyPressureThreshold)),

        // Target-quality predicates: gate on "a specific good target exists nearby right now",
        // not just "this advisor is generically busy" — what lets root.diplomacy/root.intelligence
        // branch into distinct sub-strategies instead of a single stub leaf.
        new("Diplomatic.EnemyPcOpportunityReady", "Direct Diplomatic.EnemyPcOpportunity is above its authored threshold — a nearby enemy-owned PC has fallen below Diplomatic.EnemyPcLoyaltyBelow, a candidate for influencing out.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.DiplomaticEnemyPcOpportunity) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticEnemyPcOpportunityThreshold)),
        new("Diplomatic.OwnPcLoyaltyRiskReady", "Direct Diplomatic.OwnPcLoyaltyRisk is above its authored threshold — one of this leader's own PCs has fallen below Diplomatic.OwnPcLoyaltyBelow and needs influencing up.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.DiplomaticOwnPcLoyaltyRisk) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticOwnPcLoyaltyRiskThreshold)),
        new("Intelligence.EnemyPcVulnerabilityReady", "Direct Intelligence.EnemyPcVulnerability is above its authored threshold — a nearby enemy-owned PC's defense has fallen below Intelligence.EnemyPcDefenseBelow, a candidate for sabotage/theft.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.IntelligenceEnemyPcVulnerability) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceEnemyPcVulnerabilityThreshold)),
        new("Intelligence.HighValueEnemyCharacterReady", "Direct Intelligence.HighValueEnemyCharacter is above its authored threshold — a nearby enemy character's skill sum has reached Intelligence.HighValueSkillAtLeast, a candidate for assassination/kidnap.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.IntelligenceHighValueEnemyCharacter) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceHighValueEnemyCharacterThreshold)),

        new("Magic.SpellOpportunityReady", "Direct Magic.SpellOpportunity is above its authored threshold — at least one Spell action is currently playable by this character.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.MagicSpellOpportunity) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicSpellOpportunityThreshold)),
        new("Militaristic.OwnPcFortificationNeedReady", "Direct Militaristic.OwnPcFortificationNeed is above its authored threshold — a nearby own PC's defense has fallen below Militaristic.OwnPcDefenseBelow and needs fortifying.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.MilitaristicOwnPcFortificationNeed) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MilitaristicOwnPcFortificationNeedThreshold)),
        new("Diplomatic.NplRecruitmentReady", "Direct Diplomatic.NplRecruitment is above its authored threshold — a nearby NPL capital is eligible for StateAllegiance (AFriendOrThree) recruitment right now.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.DiplomaticNplRecruitment) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticNplRecruitmentThreshold)),

        // Per-material stockpile-balancing predicates: true when this leader's own stock of the
        // material is either below its Insufficient threshold or above its Surplus threshold —
        // the max(0, ...) formulas behind each parameter already are the gate, so ">0" on either
        // is the full "this material needs attention" condition, no separate cutoff needed.
        new("Economic.MithrilReady", "True when stored mithril is below Economic.MithrilInsufficientBelow or above Economic.MithrilSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicMithrilInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicMithrilSurplus) > 0f),
        new("Economic.SteelReady", "True when stored steel is below Economic.SteelInsufficientBelow or above Economic.SteelSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicSteelInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicSteelSurplus) > 0f),
        new("Economic.IronReady", "True when stored iron is below Economic.IronInsufficientBelow or above Economic.IronSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicIronInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicIronSurplus) > 0f),
        new("Economic.MountsReady", "True when stored mounts is below Economic.MountsInsufficientBelow or above Economic.MountsSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicMountsInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicMountsSurplus) > 0f),
        new("Economic.TimberReady", "True when stored timber is below Economic.TimberInsufficientBelow or above Economic.TimberSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicTimberInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicTimberSurplus) > 0f),
        new("Economic.LeatherReady", "True when stored leather is below Economic.LeatherInsufficientBelow or above Economic.LeatherSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(AIUtilityParameters.EconomicLeatherInsufficient) > 0f || ctx.GetUtilityParameter(AIUtilityParameters.EconomicLeatherSurplus) > 0f),
    };

    private static readonly Dictionary<string, HTNPredicateDefinition> ByKey =
        KnownPredicates.ToDictionary(d => d.Key, d => d, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> PredicateNames =>
        KnownPredicates.Select(d => d.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool TryGetPredicate(string name, out Func<AIContext, AIBlackboard, bool> predicate)
    {
        if (ByKey.TryGetValue(name ?? string.Empty, out HTNPredicateDefinition definition))
        {
            predicate = definition.Predicate;
            return true;
        }
        predicate = null;
        return false;
    }

    public static bool TryGetDescription(string name, out string description)
    {
        if (ByKey.TryGetValue(name ?? string.Empty, out HTNPredicateDefinition definition))
        {
            description = definition.Description;
            return true;
        }
        description = null;
        return false;
    }
}
