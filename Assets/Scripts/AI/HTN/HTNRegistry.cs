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
    public static readonly IReadOnlyList<HTNPredicateDefinition> KnownPredicates = new List<HTNPredicateDefinition>
    {
        new("Global.Always", "Always true. Use for an unconditional fallback branch (usually the last Method under a CompoundTask).",
            (ctx, bb) => true),
        new("Global.Never", "Always false. Use on a PrimitiveTask that should never auto-advance — it only ever leaves via a higher-priority interrupt or its own precondition breaking.",
            (ctx, bb) => false),

        new("Economic.NeedsHelp", "True when the economy is Critical or Weak (see the Economic weight group for the exact thresholds).",
            (ctx, bb) => ctx.NeedsEconomicHelp),
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

        new("Intelligence.Viable", "True when Intelligence's viability (enemy-character proximity + poor-economy/outmatched bonuses — the same weights shown in the Intelligence group of the Advisors tab) is above Intelligence.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Intelligence) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceViabilityThreshold)),

        new("Magic.Viable", "True when Magic's viability (artifact scarcity + enemy proximity — the same weights shown in the Magic group of the Advisors tab) is above Magic.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Magic) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicViabilityThreshold)),

        new("Movement.Viable", "True when Movement's viability (proximity to the preferred destination — the same weights shown in the Movement group of the Advisors tab) is above Movement.ViabilityThreshold.",
            (ctx, bb) => ctx.GetAdvisorViability(AdvisorType.Movement) > AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MovementViabilityThreshold)),
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
