using System;
using System.Collections.Generic;
using System.Linq;

// ---------------------------------------------------------------------------
// Named predicate vocabulary the authored Strategies can reference — used for
// both Method preconditions and PrimitiveTask preconditions/completion
// conditions (one dictionary, two roles).
//
// Most non-Economic, non-Global predicates are a threshold on one of
// UtilityAIContext's named viability methods (GetMilitaristicViability,
// GetIntelligenceViability, etc.) — a coarse aggregate of the same GetUtilityParameter
// terms a card's own utilityParameters profile can opt into individually. There is no
// separate hand-coded sensing layer and no "advisor" tag: a card relates to the system
// purely by which named parameters it boosts, and these viability aggregates exist only
// as a coarser gate for "is this whole category of response worth considering at all."
// Change a weight (e.g. Militaristic.NoArmyPenalty) and both card scores AND the HTN
// condition that reads it move together.
//
// Keys are namespaced by the parameter group they're about, the same dotted-group
// convention UtilityAI.Keys already uses for scoring weights — Global.* holds the two
// category-agnostic predicates. Every entry carries a plain-English Description so the
// Strategies tab (and the widget's Card Board tab) can show what a condition actually
// means without anyone having to read this file.
//
// Add entries here to make new predicates available in the AI Widget dropdowns.
// ---------------------------------------------------------------------------

public class HTNPredicateDefinition
{
    public readonly string Key;
    public readonly string Description;
    public readonly Func<UtilityAIContext, CharacterBlackboard, bool> Predicate;

    public HTNPredicateDefinition(string key, string description, Func<UtilityAIContext, CharacterBlackboard, bool> predicate)
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
    // here at load time, never as a hand-coded boolean property on UtilityAIContext (that would put
    // decision logic in scoring code) and never as a bespoke named alias predicate either
    // (that would just hide the same OR behind an extra layer of indirection).
    public static Func<UtilityAIContext, CharacterBlackboard, bool> Or(params Func<UtilityAIContext, CharacterBlackboard, bool>[] predicates)
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
            (ctx, bb) => ctx.GetMilitaristicViability() > UtilityAI.GetWeight(UtilityAI.Keys.MilitaristicViabilityThreshold)),

        new("Diplomatic.Viable", "True when Diplomatic's viability (NPC proximity + outmatched bonus — the same weights shown in the Diplomatic group of the Advisors tab) is above Diplomatic.ViabilityThreshold.",
            (ctx, bb) => ctx.GetDiplomaticViability() > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticViabilityThreshold)),

        new("Intelligence.Viable", "True when Intelligence's viability (enemy-character proximity + outmatched bonus — the same weights shown in the Intelligence group of the Advisors tab) is above Intelligence.ViabilityThreshold.",
            (ctx, bb) => ctx.GetIntelligenceViability() > UtilityAI.GetWeight(UtilityAI.Keys.IntelligenceViabilityThreshold)),

        new("Artifacts.Viable", "True when Artifacts's viability (artifact scarcity + enemy proximity — the same weights shown in the Artifacts group of the Advisors tab) is above Artifacts.ViabilityThreshold. Not used by the default HTN tree (superseded by Artifacts.ArtifactScarcityReady/ArtifactTransferReady) — kept for a hand-authored Strategies.json and the Advisors tab preview.",
            (ctx, bb) => ctx.GetArtifactsViability() > UtilityAI.GetWeight(UtilityAI.Keys.ArtifactsViabilityThreshold)),

        new("Logistics.Viable", "True when Logistics's viability (proximity to the preferred destination — the same weights shown in the Logistics group of the Advisors tab) is above Logistics.ViabilityThreshold.",
            (ctx, bb) => ctx.GetLogisticsViability() > UtilityAI.GetWeight(UtilityAI.Keys.LogisticsViabilityThreshold)),

        new("Disruption.Viable", "True when Disruption's viability (enemy proximity — is there someone nearby to halt/block/debuff) is above Disruption.ViabilityThreshold.",
            (ctx, bb) => ctx.GetDisruptionViability() > UtilityAI.GetWeight(UtilityAI.Keys.DisruptionViabilityThreshold)),
        // Fine-grained predicates consume direct, named Advisor parameters only.
        // Each threshold is an authored Advisor weight, so HTN has no hidden score.
        new("Diplomatic.IndirectSafetyReady", "Direct Diplomatic.IndirectSafety is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DiplomaticIndirectSafety) > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticIndirectSafetyThreshold)),
        new("Intelligence.EnemyCharacterReady", "Direct Intelligence.EnemyCharacter is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.IntelligenceEnemyCharacter) > UtilityAI.GetWeight(UtilityAI.Keys.IntelligenceEnemyCharacterThreshold)),
        new("Intelligence.IndirectSafetyReady", "Direct Intelligence.IndirectSafety is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.IntelligenceIndirectSafety) > UtilityAI.GetWeight(UtilityAI.Keys.IntelligenceIndirectSafetyThreshold)),
        new("Artifacts.ArtifactScarcityReady", "Direct Artifacts.ArtifactScarcity is above its authored threshold — gates root.artifacts.lowartifacts.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.ArtifactsArtifactScarcity) > UtilityAI.GetWeight(UtilityAI.Keys.ArtifactsArtifactScarcityThreshold)),
        new("Artifacts.ArtifactTransferReady", "Direct Artifacts.ArtifactTransfer is above its authored threshold — gates root.artifacts.surplus (\"mages have many artifacts, consolidate/protect them\").",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.ArtifactsArtifactTransfer) > UtilityAI.GetWeight(UtilityAI.Keys.ArtifactsArtifactTransferThreshold)),
        new("Artifacts.EnemyPressureReady", "Direct Artifacts.EnemyPressure is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.ArtifactsEnemyPressure) > UtilityAI.GetWeight(UtilityAI.Keys.ArtifactsEnemyPressureThreshold)),
        new("Logistics.ReachNpcReady", "Direct Logistics.ReachNpc is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.LogisticsReachNpc) > UtilityAI.GetWeight(UtilityAI.Keys.LogisticsReachNpcThreshold)),
        new("Logistics.InterceptEnemyReady", "Direct Logistics.InterceptEnemy is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.LogisticsInterceptEnemy) > UtilityAI.GetWeight(UtilityAI.Keys.LogisticsInterceptEnemyThreshold)),
        new("Logistics.ReachEnemyCharacterReady", "Direct Logistics.ReachEnemyCharacter is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.LogisticsReachEnemyCharacter) > UtilityAI.GetWeight(UtilityAI.Keys.LogisticsReachEnemyCharacterThreshold)),
        new("Logistics.HealingNeedReady", "Direct Logistics.HealingNeed is above its authored threshold — a wounded ally shares this character's hex.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.LogisticsHealingNeed) > UtilityAI.GetWeight(UtilityAI.Keys.LogisticsHealingNeedThreshold)),
        new("Disruption.EnemyPressureReady", "Direct Disruption.EnemyPressure is above its authored threshold.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DisruptionEnemyPressure) > UtilityAI.GetWeight(UtilityAI.Keys.DisruptionEnemyPressureThreshold)),

        // Target-quality predicates: gate on "a specific good target exists nearby right now",
        // not just "this advisor is generically busy" — what lets root.diplomacy/root.intelligence
        // branch into distinct sub-strategies instead of a single stub leaf.
        new("Diplomatic.EnemyPcOpportunityReady", "Direct Diplomatic.EnemyPcOpportunity is above its authored threshold — a nearby enemy-owned PC has fallen below Diplomatic.EnemyPcLoyaltyBelow, a candidate for influencing out.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DiplomaticEnemyPcOpportunity) > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticEnemyPcOpportunityThreshold)),
        new("Diplomatic.OwnPcLoyaltyRiskReady", "Direct Diplomatic.OwnPcLoyaltyRisk is above its authored threshold — one of this leader's own PCs has fallen below Diplomatic.OwnPcLoyaltyBelow and needs influencing up.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DiplomaticOwnPcLoyaltyRisk) > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticOwnPcLoyaltyRiskThreshold)),
        new("Intelligence.EnemyPcVulnerabilityReady", "Direct Intelligence.EnemyPcVulnerability is above its authored threshold — a nearby enemy-owned PC's defense has fallen below Intelligence.EnemyPcDefenseBelow, a candidate for sabotage/theft.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.IntelligenceEnemyPcVulnerability) > UtilityAI.GetWeight(UtilityAI.Keys.IntelligenceEnemyPcVulnerabilityThreshold)),
        new("Intelligence.HighValueEnemyCharacterReady", "Direct Intelligence.HighValueEnemyCharacter is above its authored threshold — a nearby enemy character's skill sum has reached Intelligence.HighValueSkillAtLeast, a candidate for assassination/kidnap.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.IntelligenceHighValueEnemyCharacter) > UtilityAI.GetWeight(UtilityAI.Keys.IntelligenceHighValueEnemyCharacterThreshold)),
        new("Intelligence.ExplorationReady", "True when this leader still has at least one unrevealed land hex. Water hexes never count as exploration targets.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.IntelligenceExplorationNeed) > 0f
                && ctx.GetTargetHexForParameter(UtilityAIParameters.IntelligenceExplorationNeed) != null),

        new("Militaristic.OwnPcFortificationNeedReady", "Direct Militaristic.OwnPcFortificationNeed is above its authored threshold — a nearby own PC's defense has fallen below Militaristic.OwnPcDefenseBelow and needs fortifying.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.MilitaristicOwnPcFortificationNeed) > UtilityAI.GetWeight(UtilityAI.Keys.MilitaristicOwnPcFortificationNeedThreshold)),
        new("Diplomatic.NplRecruitmentReady", "Direct Diplomatic.NplRecruitment is above its authored threshold — a nearby NPL capital is eligible for StateAllegiance (AFriendOrThree) recruitment right now.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DiplomaticNplRecruitment) > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticNplRecruitmentThreshold)),

        // Situations-first restructure: cross-cutting danger tiers and win-probability gates,
        // replacing the old per-domain Viable aggregates as the HTN's actual branch drivers.
        new("Global.ImmediateDanger", "True when the nearest non-neutral enemy (or any enemy if none) is within Targeting.ImmediateDangerDistance hexes AND this leader's army/PC is outmatched there — the tightest-radius, highest-priority danger response, shared by every domain's ImmediateDanger pick (Militaristic/Intelligence/PersonalCombat).",
            (ctx, bb) => ctx.IsImmediateDanger),

        new("Militaristic.OffenseWinRatioReady", "True when not in danger and this character's army offence is at least Militaristic.MinWinRatioToAttack times the nearest enemy's estimated strength (UtilityAIContext.GetArmyWinRatio) — the hard gate replacing the old fuzzy Militaristic.Viable, so a losing or marginal matchup never routes into an Attack strategy.",
            (ctx, bb) => !(ctx.IsEnemyNear && ctx.IsOutmatched) && ctx.GetArmyWinRatio() >= UtilityAI.GetWeight(UtilityAI.Keys.MilitaristicMinWinRatioToAttack)),

        new("Militaristic.DuelOpportunityReady", "True when Militaristic.DuelAdvantage (this character's Duel.EstimateDuelScore minus the best eligible opponent's) clears Militaristic.DuelSafetyMargin — a comfortable, not just positive, edge, since Duel.ResolveDuel's near-tie case is a coinflip.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.MilitaristicDuelAdvantage) >= UtilityAI.GetWeight(UtilityAI.Keys.MilitaristicDuelSafetyMargin)),
        new("Militaristic.SongDuelOpportunityReady", "Same as Militaristic.DuelOpportunityReady, for Battle of Songs (mage-vs-mage).",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.MilitaristicSongDuelAdvantage) >= UtilityAI.GetWeight(UtilityAI.Keys.MilitaristicSongDuelSafetyMargin)),

        new("Diplomatic.LowNplsReady", "True when this leader's board-wide count of same-alignment, not-yet-joined NonPlayableLeaders is at or below Diplomatic.LowNplsCountAtMost — a wide-radius recruit push regardless of proximity.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.DiplomaticNplScarcity) > UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticNplScarcityThreshold)),

        new("Diplomatic.NplsNearReady", "True when the nearest recruitment-eligible NPL capital (Diplomatic.NplRecruitment's target) is within Diplomatic.NplNearDistance hexes.",
            (ctx, bb) => ctx.NearestNplRecruitmentDistance <= UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticNplNearDistance)),
        new("Diplomatic.NplsMidReady", "Same, within the wider Diplomatic.NplMidDistance band.",
            (ctx, bb) => ctx.NearestNplRecruitmentDistance <= UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticNplMidDistance)),

        new("Diplomatic.EnemyPcOpportunityNearReady", "True when the nearest low-loyalty enemy PC (Diplomatic.EnemyPcOpportunity's target) is within Diplomatic.EnemyPcOpportunityNearDistance hexes.",
            (ctx, bb) => ctx.NearestEnemyPcOpportunityDistance <= UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticEnemyPcOpportunityNearDistance)),
        new("Diplomatic.EnemyPcOpportunityMidReady", "Same, within the wider Diplomatic.EnemyPcOpportunityMidDistance band.",
            (ctx, bb) => ctx.NearestEnemyPcOpportunityDistance <= UtilityAI.GetWeight(UtilityAI.Keys.DiplomaticEnemyPcOpportunityMidDistance)),

        // Per-material stockpile-balancing predicates: true when this leader's own stock of the
        // material is either below its Insufficient threshold or above its Surplus threshold —
        // the max(0, ...) formulas behind each parameter already are the gate, so ">0" on either
        // is the full "this material needs attention" condition, no separate cutoff needed.
        new("Economic.MithrilReady", "True when stored mithril is below Economic.MithrilInsufficientBelow or above Economic.MithrilSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicMithrilInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicMithrilSurplus) > 0f),
        new("Economic.SteelReady", "True when stored steel is below Economic.SteelInsufficientBelow or above Economic.SteelSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicSteelInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicSteelSurplus) > 0f),
        new("Economic.IronReady", "True when stored iron is below Economic.IronInsufficientBelow or above Economic.IronSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicIronInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicIronSurplus) > 0f),
        new("Economic.MountsReady", "True when stored mounts is below Economic.MountsInsufficientBelow or above Economic.MountsSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicMountsInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicMountsSurplus) > 0f),
        new("Economic.TimberReady", "True when stored timber is below Economic.TimberInsufficientBelow or above Economic.TimberSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicTimberInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicTimberSurplus) > 0f),
        new("Economic.LeatherReady", "True when stored leather is below Economic.LeatherInsufficientBelow or above Economic.LeatherSurplusAbove.",
            (ctx, bb) => ctx.GetUtilityParameter(UtilityAIParameters.EconomicLeatherInsufficient) > 0f || ctx.GetUtilityParameter(UtilityAIParameters.EconomicLeatherSurplus) > 0f),
    };

    private static readonly Dictionary<string, HTNPredicateDefinition> ByKey =
        KnownPredicates.ToDictionary(d => d.Key, d => d, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> PredicateNames =>
        KnownPredicates.Select(d => d.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool TryGetPredicate(string name, out Func<UtilityAIContext, CharacterBlackboard, bool> predicate)
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
