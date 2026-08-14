using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// Utility AI: the parameter registry, tunable weights, and per-card parameter
// profiles every AI decision is built from. There is no separate "advisor"
// concept — a card relates to the system purely by which named UtilityAI
// parameters it boosts (utilityParameters below), and an HTN leaf relates to
// it purely by which parameters it prefers (HTNPrimitiveTask.PreferredParameters).
// A card and a branch align when their parameter lists overlap; nothing is
// tagged or categorized beyond that.
//
// Edited via Window > RetroLOTR > AI Widget.
// ---------------------------------------------------------------------------

[Serializable]
public class UtilityWeightEntry
{
    public string key = string.Empty;
    public float value;
}

[Serializable]
public class ActionUtilityParameterModifier
{
    // Must be one of UtilityAIParameters.Known. Empty entries are ignored.
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
public class UtilityConfigData
{
    public List<UtilityWeightEntry> weights = new();
    public List<CardParameterProfile> cardProfiles = new();
}

// One row per printed card — the unit every Card Board authoring choice is
// keyed against. deckId+cardId is the stable identity (see
// UtilityAI.BuildCardProfileKey for why reference/injected cards resolve to
// their template's deckId+cardId instead of their own). Two cards that
// happen to share an action class each get their own independent row here —
// the AI Widget's Card Board tab has a "duplicate to sibling cards" action to
// seed one from another, but nothing at runtime ever shares a row.
[Serializable]
public class CardParameterProfile
{
    public string deckId = string.Empty;
    public int cardId;

    // Display-only — never read for lookups, so a stale value (e.g. after a
    // card rename) can't silently break anything.
    public string cardName = string.Empty;
    public string actionClass = string.Empty;

    // Flat score adjustment applied whenever the AI scores this action;
    // lets an action be prioritized over other cards regardless of situation.
    public float scoreBonus;
    // Per-action formula composition: true = leave that term out of the score.
    public bool ignoreSituation;
    // Explicit card-side composition of named UtilityAI parameters — the only
    // way a card relates to the utility system. These entries are also shown
    // and edited in the AI Widget's Card Board tab.
    public List<ActionUtilityParameterModifier> utilityParameters = new();
}

public class UtilityWeightDefinition
{
    public readonly string key;
    public readonly float defaultValue;
    public readonly string description;

    public UtilityWeightDefinition(string key, float defaultValue, string description)
    {
        this.key = key;
        this.defaultValue = defaultValue;
        this.description = description;
    }
}

// The complete public vocabulary of named, directly-observable situational readings.
// Values are direct observations computed by UtilityAIContext; they are never
// inferred from a card, and every card-specific contribution is authored via
// CardParameterProfile.utilityParameters. The "Militaristic."/"Economic."/etc.
// prefixes are purely a naming convention for grouping related readings — there
// is no enum or type behind them, just string namespacing.
public static class UtilityAIParameters
{
    public const string MilitaristicEnemyPressure = "Militaristic.EnemyPressure";
    public const string MilitaristicMilitaryEdge = "Militaristic.MilitaryEdge";
    public const string EconomicLiquidWealth = "Economic.LiquidWealth";
    public const string DiplomaticIndirectSafety = "Diplomatic.IndirectSafety";
    public const string IntelligenceEnemyCharacter = "Intelligence.EnemyCharacter";
    public const string IntelligenceIndirectSafety = "Intelligence.IndirectSafety";
    public const string ArtifactsArtifactScarcity = "Artifacts.ArtifactScarcity";
    public const string ArtifactsArtifactTransfer = "Artifacts.ArtifactTransfer";
    public const string ArtifactsEnemyPressure = "Artifacts.EnemyPressure";
    public const string ArtifactsHiddenArtifacts = "Artifacts.HiddenArtifacts";
    public const string ArtifactsMageStrength = "Artifacts.MageStrength";
    public const string DiplomaticEnemyPressure = "Diplomatic.EnemyPressure";
    public const string DiplomaticEmissaryStrength = "Diplomatic.EmissaryStrength";
    public const string IntelligenceEnemyPressure = "Intelligence.EnemyPressure";
    public const string IntelligenceAgentStrength = "Intelligence.AgentStrength";
    public const string IntelligenceExplorationNeed = "Intelligence.ExplorationNeed";
    // Logistics: reposition our own side (renamed from the old, unsplit "Movement") plus healing.
    public const string LogisticsReachNpc = "Logistics.ReachNpc";
    public const string LogisticsInterceptEnemy = "Logistics.InterceptEnemy";
    public const string LogisticsReachEnemyCharacter = "Logistics.ReachEnemyCharacter";
    public const string LogisticsHealingNeed = "Logistics.HealingNeed";

    // Disruption: deny/debuff the enemy (halt, block, negative status) — the other half of the
    // old "Movement" split.
    public const string DisruptionEnemyPressure = "Disruption.EnemyPressure";

    // Target-quality signals (distinct from the proximity/strength terms above): "is there a
    // specific, good target nearby right now", not just "is this category of response generically busy".
    public const string DiplomaticEnemyPcOpportunity = "Diplomatic.EnemyPcOpportunity";
    public const string DiplomaticOwnPcLoyaltyRisk = "Diplomatic.OwnPcLoyaltyRisk";
    public const string IntelligenceEnemyPcVulnerability = "Intelligence.EnemyPcVulnerability";
    public const string IntelligenceHighValueEnemyCharacter = "Intelligence.HighValueEnemyCharacter";

    // Second wave: closes remaining gaps (fortification for Militaristic, NPL recruitment for
    // Diplomatic) rather than just proximity/strength math. Spells are NOT gated through a
    // Magic/Artifacts-wide "is any spell castable" signal — each spell card is tagged with
    // whichever domain parameter its actual effect serves (see UtilityAI.json), so it surfaces
    // under that domain's own branch instead of being bucketed as "Artifacts" regardless of effect.
    public const string MilitaristicOwnPcFortificationNeed = "Militaristic.OwnPcFortificationNeed";
    public const string DiplomaticNplRecruitment = "Diplomatic.NplRecruitment";

    // Same proximity-to-an-undefended-own-PC signal as MilitaristicOwnPcFortificationNeed above
    // (identical formula), under a distinct name so root.danger.pick's fortify and conscript
    // leaves can each target their own card family (FortifyPC vs. ConscriptArmy/TrainArmy/Block)
    // via PreferredParameters independently of one another.
    public const string MilitaristicOwnPcDefenderNeed = "Militaristic.OwnPcDefenderNeed";

    // Win-probability signals: this character's estimated personal-combat score margin against
    // the best eligible opponent sharing its hex (Duel.EstimateDuelScore / BattleOfSongs.
    // EstimateSongScore, self minus opponent) — signed, so a losing matchup contributes
    // negatively to the card's own score instead of just failing to help, and the HTN only
    // proactively routes into a duel when the margin clears a safety-margin threshold (see
    // HTNRegistry.Militaristic.DuelOpportunityReady / SongDuelOpportunityReady).
    public const string MilitaristicDuelAdvantage = "Militaristic.DuelAdvantage";
    public const string MilitaristicSongDuelAdvantage = "Militaristic.SongDuelAdvantage";

    // Board-wide (not proximity-based) count of same-alignment NonPlayableLeaders this leader
    // could still recruit at all (NonPlayableLeader.joined == false) — distinct from
    // DiplomaticNplRecruitment, which is "is there one specific eligible target nearby right now".
    public const string DiplomaticNplScarcity = "Diplomatic.NplScarcity";

    // Third wave: per-material stockpile balancing. One Insufficient/Surplus pair per tradeable
    // ProducesEnum material, each driving that material's own Buy{X}/Sell{X} cards. Gold has no
    // Buy/Sell card of its own — its Insufficient/Surplus instead bias every Sell{X}/Buy{X} card
    // respectively (sell anything to raise cash; spend excess cash on anything), reusing the
    // existing Economy.CriticalBelow/StableBelow thresholds rather than inventing new ones.
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
        ArtifactsArtifactScarcity, ArtifactsArtifactTransfer, ArtifactsEnemyPressure, ArtifactsHiddenArtifacts, ArtifactsMageStrength,
        DiplomaticEnemyPressure, DiplomaticEmissaryStrength, IntelligenceEnemyPressure, IntelligenceAgentStrength, IntelligenceExplorationNeed,
        LogisticsReachNpc, LogisticsInterceptEnemy, LogisticsReachEnemyCharacter, LogisticsHealingNeed, DisruptionEnemyPressure,
        DiplomaticEnemyPcOpportunity, DiplomaticOwnPcLoyaltyRisk, IntelligenceEnemyPcVulnerability, IntelligenceHighValueEnemyCharacter,
        MilitaristicOwnPcFortificationNeed, DiplomaticNplRecruitment, MilitaristicOwnPcDefenderNeed,
        MilitaristicDuelAdvantage, MilitaristicSongDuelAdvantage, DiplomaticNplScarcity,
        EconomicMithrilInsufficient, EconomicMithrilSurplus, EconomicSteelInsufficient, EconomicSteelSurplus,
        EconomicIronInsufficient, EconomicIronSurplus, EconomicMountsInsufficient, EconomicMountsSurplus,
        EconomicTimberInsufficient, EconomicTimberSurplus, EconomicLeatherInsufficient, EconomicLeatherSurplus,
        EconomicGoldInsufficient, EconomicGoldSurplus
    };

    public static bool IsKnown(string parameter) => !string.IsNullOrWhiteSpace(parameter)
        && Known.Contains(parameter, StringComparer.OrdinalIgnoreCase);

    public static bool IsMovementOnly(string parameter) =>
        string.Equals(parameter, IntelligenceExplorationNeed, StringComparison.OrdinalIgnoreCase);
}

public enum EconomyStatus
{
    Critical = 0,
    Weak = 1,
    Stable = 2,
    Surplus = 3
}

public static class UtilityAI
{
    public const string ResourcePath = "AI/UtilityAI";

    public static class Keys
    {
        // Bonus for a card whose own utilityParameters list shares a parameter with the HTN
        // leaf's currently-active PreferredParameters — the only mechanism that ties a card to
        // "what the current strategy is about". Replaces the old two-tier
        // advisor-tag-match/parameter-match split (there is no tag to match anymore, only
        // parameters), so its default folds both of the old bonuses into one.
        public const string HTNBiasBonus = "Global.HTNBiasBonus";

        // Large negative bias applied to any Environmental-type card's score, discouraging the
        // AI from playing them often — decays linearly to 0 once EnvironmentalPenaltyDecayTurns
        // turns have passed since this leader last played one (see
        // UtilityAIContext.GetEnvironmentalPenaltyScore / Leader.lastEnvironmentalCardPlayedTurn).
        public const string EnvironmentalPenalty = "Global.EnvironmentalPenalty";
        public const string EnvironmentalPenaltyDecayTurns = "Global.EnvironmentalPenaltyDecayTurns";

        // Single axis: Leader.goldAmount + resources held valued at current market sell
        // price (UtilityAIContext.CalculateLiquidWealth) — this game has no passive per-turn
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

        public const string ArtifactScarcityWeight = "Artifacts.ArtifactScarcityWeight";
        public const string ArtifactsViabilityThreshold = "Artifacts.ViabilityThreshold";

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
        public const string ArtifactsArtifactScarcityThreshold = "Artifacts.ArtifactScarcityThreshold";
        public const string ArtifactsArtifactTransferThreshold = "Artifacts.ArtifactTransferThreshold";
        public const string ArtifactsEnemyPressureThreshold = "Artifacts.EnemyPressureThreshold";
        public const string LogisticsReachNpcThreshold = "Logistics.ReachNpcThreshold";
        public const string LogisticsInterceptEnemyThreshold = "Logistics.InterceptEnemyThreshold";
        public const string LogisticsReachEnemyCharacterThreshold = "Logistics.ReachEnemyCharacterThreshold";
        public const string LogisticsHealingNeedHealthBelow = "Logistics.HealingNeedHealthBelow";
        public const string LogisticsHealingNeedThreshold = "Logistics.HealingNeedThreshold";
        public const string ArtifactsHiddenArtifactsWeight = "Artifacts.HiddenArtifactsWeight";
        public const string ArtifactsMageStrengthWeight = "Artifacts.MageStrengthWeight";
        public const string DiplomaticEnemyPressureWeight = "Diplomatic.EnemyPressureWeight";
        public const string DiplomaticEmissaryStrengthWeight = "Diplomatic.EmissaryStrengthWeight";
        public const string IntelligenceEnemyPressureWeight = "Intelligence.EnemyPressureWeight";
        public const string IntelligenceAgentStrengthWeight = "Intelligence.AgentStrengthWeight";

        // Target-quality signals: is there a specific good target nearby, not just "is this
        // category generically busy". Each pairs a "what counts as a target" threshold with a
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

        // Second wave: Militaristic gets a fortification-need signal (previously combat-only),
        // Diplomatic gets an NPL-recruitment-eligibility signal (previously discovery-only).
        // None of these are folded into a viability aggregate — available as a direct utility
        // parameter and an HTN "Ready" predicate for sub-branching, without widening any outer gate.
        public const string MilitaristicOwnPcDefenseBelow = "Militaristic.OwnPcDefenseBelow";
        public const string MilitaristicOwnPcFortificationProximityMax = "Militaristic.OwnPcFortificationProximityMax";
        public const string MilitaristicOwnPcFortificationNeedThreshold = "Militaristic.OwnPcFortificationNeedThreshold";

        public const string DiplomaticNplRecruitmentProximityMax = "Diplomatic.NplRecruitmentProximityMax";
        public const string DiplomaticNplRecruitmentThreshold = "Diplomatic.NplRecruitmentThreshold";

        // Situations-first restructure: the HTN's highest-priority danger tier reads a raw
        // (unfaded) distance instead of a continuous proximity score — "is the enemy right on
        // top of me", not "how strong is proximity pressure overall".
        public const string ImmediateDangerDistance = "Targeting.ImmediateDangerDistance";

        // Win-probability gating: root.offense now requires an explicit favorable strength
        // ratio (UtilityAIContext.GetArmyWinRatio) instead of a fuzzy viability sum, so the HTN
        // never routes into an Attack strategy while outnumbered.
        public const string MilitaristicMinWinRatioToAttack = "Militaristic.MinWinRatioToAttack";

        // Duel/BattleOfSongs are never proactively sought out by the HTN unless the margin
        // (UtilityAIParameters.MilitaristicDuelAdvantage/SongDuelAdvantage) clears a comfortable
        // edge, not just a positive one — Duel.ResolveDuel's near-tie case is a coinflip.
        public const string MilitaristicDuelSafetyMargin = "Militaristic.DuelSafetyMargin";
        public const string MilitaristicSongDuelSafetyMargin = "Militaristic.SongDuelSafetyMargin";

        // Diplomacy near/mid distance banding: splits the existing continuous
        // DiplomaticNplRecruitment/DiplomaticEnemyPcOpportunity proximity signals into two
        // discrete HTN priority tiers instead of one fading score.
        public const string DiplomaticNplNearDistance = "Diplomatic.NplNearDistance";
        public const string DiplomaticNplMidDistance = "Diplomatic.NplMidDistance";
        public const string DiplomaticEnemyPcOpportunityNearDistance = "Diplomatic.EnemyPcOpportunityNearDistance";
        public const string DiplomaticEnemyPcOpportunityMidDistance = "Diplomatic.EnemyPcOpportunityMidDistance";

        // Board-wide NPL scarcity: how few same-alignment, not-yet-joined NonPlayableLeaders
        // count as "few remain" (feeds DiplomaticNplScarcity), and the standard *Threshold gate
        // for the derived "Ready" predicate.
        public const string DiplomaticLowNplsCountAtMost = "Diplomatic.LowNplsCountAtMost";
        public const string DiplomaticNplScarcityThreshold = "Diplomatic.NplScarcityThreshold";

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

    public static readonly IReadOnlyList<UtilityWeightDefinition> KnownWeights = new List<UtilityWeightDefinition>
    {
        new(Keys.HTNBiasBonus, 5f, "Score bonus for a card whose own utility-parameter profile shares a parameter with the HTN strategy's currently-active leaf's PreferredParameters — the sole mechanism biasing card choice toward the active branch."),

        new(Keys.EnvironmentalPenalty, -25f, "Score penalty applied to any Environmental-type card, at full strength the turn after this leader last played one — decays to 0 over EnvironmentalPenaltyDecayTurns turns."),
        new(Keys.EnvironmentalPenaltyDecayTurns, 6f, "Number of turns after playing an environmental card before EnvironmentalPenalty fully decays back to 0."),

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

        new(Keys.ArtifactScarcityWeight, 2f, "Artifacts bonus scale for how few artifacts the nation owns (0..1 scarcity times this)."),
        new(Keys.ArtifactsViabilityThreshold, 0f, "HTN switches to an Artifacts strategy once Artifacts's viability (same terms as its situational score above) crosses this."),

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
        new(Keys.ArtifactsArtifactScarcityThreshold, 0f, "HTN threshold for direct Artifacts.ArtifactScarcity."),
        new(Keys.ArtifactsArtifactTransferThreshold, 0f, "HTN threshold for direct Artifacts.ArtifactTransfer."),
        new(Keys.ArtifactsEnemyPressureThreshold, 0f, "HTN threshold for direct Artifacts.EnemyPressure."),
        new(Keys.LogisticsReachNpcThreshold, 0f, "HTN threshold for direct Logistics.ReachNpc."),
        new(Keys.LogisticsInterceptEnemyThreshold, 0f, "HTN threshold for direct Logistics.InterceptEnemy."),
        new(Keys.LogisticsReachEnemyCharacterThreshold, 0f, "HTN threshold for direct Logistics.ReachEnemyCharacter."),
        new(Keys.LogisticsHealingNeedHealthBelow, 70f, "An allied character in this hex counts as needing healing when their health is below this (Character.health, 0-100)."),
        new(Keys.LogisticsHealingNeedThreshold, 0f, "HTN threshold for direct Logistics.HealingNeed (count of wounded allies in this hex)."),
        new(Keys.DisruptionEnemyPressureThreshold, 0f, "HTN threshold for direct Disruption.EnemyPressure."),
        new(Keys.ArtifactsHiddenArtifactsWeight, 1f, "Artifacts viability per hidden artifact still on the map."),
        new(Keys.ArtifactsMageStrengthWeight, 0.5f, "Artifacts viability per total active Mage level under this leader."),
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

        new(Keys.MilitaristicOwnPcDefenseBelow, 6f, "One of this leader's own PCs counts as needing fortification when its PC.GetDefense() is below this."),
        new(Keys.MilitaristicOwnPcFortificationProximityMax, 10f, "Militaristic.OwnPcFortificationNeed bonus at distance 0 from the nearest under-fortified own PC; fades by 1 per hex."),
        new(Keys.MilitaristicOwnPcFortificationNeedThreshold, 0f, "HTN threshold for direct Militaristic.OwnPcFortificationNeed."),

        new(Keys.DiplomaticNplRecruitmentProximityMax, 10f, "Diplomatic.NplRecruitment bonus at distance 0 from the nearest NPL capital eligible for StateAllegiance (AFriendOrThree) recruitment; fades by 1 per hex."),
        new(Keys.DiplomaticNplRecruitmentThreshold, 0f, "HTN threshold for direct Diplomatic.NplRecruitment."),

        // Situations-first restructure: immediate-danger radius, win-probability gates for
        // Offense/Duel/BattleOfSongs, Diplomacy near/mid banding, and board-wide NPL scarcity.
        // Starting points only, not balance-tuned — expect to retune via the AI Widget.
        new(Keys.ImmediateDangerDistance, 1.5f, "Raw hex distance to the nearest non-neutral enemy (or any enemy if none), combined with being outmatched, at or below which the HTN's highest-priority ImmediateDanger response fires."),
        new(Keys.MilitaristicMinWinRatioToAttack, 1.15f, "This character's army offence must be at least this many times the nearest enemy's estimated strength (UtilityAIContext.GetArmyWinRatio) for root.offense to be considered — replaces the old fuzzy Militaristic.Viable gate."),
        new(Keys.MilitaristicDuelSafetyMargin, 1.0f, "Militaristic.DuelAdvantage (this character's EstimateDuelScore minus the best eligible opponent's) must clear this before the HTN proactively routes into a Duel — a comfortable edge, not just a positive one, since Duel.ResolveDuel's near-tie case is a coinflip."),
        new(Keys.MilitaristicSongDuelSafetyMargin, 1.0f, "Same as Militaristic.DuelSafetyMargin, for Battle of Songs."),

        new(Keys.DiplomaticNplNearDistance, 3f, "Raw distance to the nearest recruitment-eligible NPL capital (Diplomatic.NplRecruitment's target) at or below which Diplomacy's near-band recruit push fires."),
        new(Keys.DiplomaticNplMidDistance, 8f, "Same, wider band."),
        new(Keys.DiplomaticEnemyPcOpportunityNearDistance, 3f, "Raw distance to the nearest low-loyalty enemy PC (Diplomatic.EnemyPcOpportunity's target) at or below which Diplomacy's near-band influence-out push fires."),
        new(Keys.DiplomaticEnemyPcOpportunityMidDistance, 8f, "Same, wider band."),

        new(Keys.DiplomaticLowNplsCountAtMost, 2f, "Diplomatic.NplScarcity input — how many same-alignment, not-yet-joined NonPlayableLeaders board-wide count as \"few remain,\" triggering a wide-radius recruit push."),
        new(Keys.DiplomaticNplScarcityThreshold, 0f, "HTN threshold for direct Diplomatic.NplScarcity."),

        // Third wave: per-material stockpile thresholds against Leader's own resource amounts
        // (not market stock) deviates from what the leader's own deck actually needs — see
        // NationBlackboard (the per-material share of the deck's total material cost) and
        // UtilityAIContext.GetResourceInsufficientScore/GetResourceSurplusScore. Each key here
        // is now a scale factor on a 0..100 percentage-point deviation, not an absolute unit
        // threshold — e.g. Insufficient = max(0, deck target share − current stockpile share) *
        // 100 * this weight. Mithril is tuned more reactive since it's the rarest material
        // (StoresManager.MithrilSellValue=7, ReferenceStock=10 vs. 25 for the rest). Starting
        // points only, not balance-tuned — use the widget's Materials scenario inputs (stockpile
        // amounts + deck target share sliders) to preview and retune by feel.
        new(Keys.EconomicMithrilInsufficientBelow, 0.3f, "Economic.MithrilInsufficient scale — how strongly a mithril stockpile-share shortfall (vs. the deck's target share) biases toward BuyMithril."),
        new(Keys.EconomicMithrilSurplusAbove, 0.3f, "Economic.MithrilSurplus scale — how strongly a mithril stockpile-share excess (vs. the deck's target share) biases toward SellMithril."),
        new(Keys.EconomicSteelInsufficientBelow, 0.15f, "Economic.SteelInsufficient scale — how strongly a steel stockpile-share shortfall (vs. the deck's target share) biases toward BuySteel."),
        new(Keys.EconomicSteelSurplusAbove, 0.15f, "Economic.SteelSurplus scale — how strongly a steel stockpile-share excess (vs. the deck's target share) biases toward SellSteel."),
        new(Keys.EconomicIronInsufficientBelow, 0.15f, "Economic.IronInsufficient scale — how strongly an iron stockpile-share shortfall (vs. the deck's target share) biases toward BuyIron."),
        new(Keys.EconomicIronSurplusAbove, 0.15f, "Economic.IronSurplus scale — how strongly an iron stockpile-share excess (vs. the deck's target share) biases toward SellIron."),
        new(Keys.EconomicMountsInsufficientBelow, 0.15f, "Economic.MountsInsufficient scale — how strongly a mounts stockpile-share shortfall (vs. the deck's target share) biases toward BuyMounts."),
        new(Keys.EconomicMountsSurplusAbove, 0.15f, "Economic.MountsSurplus scale — how strongly a mounts stockpile-share excess (vs. the deck's target share) biases toward SellMounts."),
        new(Keys.EconomicTimberInsufficientBelow, 0.15f, "Economic.TimberInsufficient scale — how strongly a timber stockpile-share shortfall (vs. the deck's target share) biases toward BuyTimber."),
        new(Keys.EconomicTimberSurplusAbove, 0.15f, "Economic.TimberSurplus scale — how strongly a timber stockpile-share excess (vs. the deck's target share) biases toward SellTimber."),
        new(Keys.EconomicLeatherInsufficientBelow, 0.15f, "Economic.LeatherInsufficient scale — how strongly a leather stockpile-share shortfall (vs. the deck's target share) biases toward BuyLeather."),
        new(Keys.EconomicLeatherSurplusAbove, 0.15f, "Economic.LeatherSurplus scale — how strongly a leather stockpile-share excess (vs. the deck's target share) biases toward SellLeather."),
    };

    private static Dictionary<string, float> defaultsByKey;
    private static Dictionary<string, float> loadedWeights;
    private static Dictionary<string, CardParameterProfile> loadedCardProfiles;
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

    // Gathers every candidate tied for the top score (within a small float-noise epsilon) and
    // returns one at random, instead of deterministically favoring whichever happened to sort
    // first — used by every "pick the best-scoring X" site (card selection, duel/song-duel
    // target selection) so the AI doesn't always break ties the same way turn after turn.
    private const float TopScoreTieEpsilon = 0.0001f;

    public static T PickRandomAmongTopScored<T>(IEnumerable<T> candidates, Func<T, float> scoreOf)
    {
        List<T> list = candidates as List<T> ?? candidates?.ToList();
        if (list == null || list.Count == 0) return default;

        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < list.Count; i++)
        {
            float score = scoreOf(list[i]);
            if (score > bestScore) bestScore = score;
        }

        List<T> tiedForBest = new();
        for (int i = 0; i < list.Count; i++)
        {
            if (Mathf.Abs(scoreOf(list[i]) - bestScore) <= TopScoreTieEpsilon) tiedForBest.Add(list[i]);
        }

        return tiedForBest[UnityEngine.Random.Range(0, tiedForBest.Count)];
    }

    // Flat, user-authored score adjustment for this action (0 when unset).
    public static float GetActionScoreBonus(CharacterAction action)
    {
        if (action == null) return 0f;
        EnsureLoaded();
        return TryGetProfile(action.card, out CardParameterProfile profile) ? profile.scoreBonus : 0f;
    }

    // Single source of truth for how liquid wealth (gold + resources at current market
    // price — see UtilityAIContext.CalculateLiquidWealth) maps to an economy status. This game
    // has no passive per-turn income to threshold against, so there is only one axis.
    // Thresholds are editable in the AI Widget (Economic tab).
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
        return TryGetProfile(action.card, out CardParameterProfile profile)
            ? new ActionScoreFlags { ignoreSituation = profile.ignoreSituation }
            : default;
    }

    // Card-side, fully authored modifiers. No implicit action-specific utility
    // exists: a contribution can only be present if it appears in this list.
    public static IReadOnlyList<ActionUtilityParameterModifier> GetActionUtilityParameters(CharacterAction action)
    {
        if (action == null) return Array.Empty<ActionUtilityParameterModifier>();
        EnsureLoaded();
        return TryGetProfile(action.card, out CardParameterProfile profile) && profile.utilityParameters?.Count > 0
            ? profile.utilityParameters
            : Array.Empty<ActionUtilityParameterModifier>();
    }

    public static bool TryGetProfile(CardData card, out CardParameterProfile profile)
    {
        profile = null;
        string key = BuildCardProfileKey(card);
        EnsureLoaded();
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
        loadedCardProfiles = new Dictionary<string, CardParameterProfile>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

        UtilityConfigData data = null;
        try { data = JsonUtility.FromJson<UtilityConfigData>(asset.text); }
        catch (Exception e)
        {
            Debug.LogWarning($"UtilityAI: could not parse {ResourcePath}.json — using default weights. {e.Message}");
            return;
        }

        if (data?.weights != null)
        {
            foreach (UtilityWeightEntry entry in data.weights)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                loadedWeights[entry.key] = entry.value;
            }
        }

        if (data?.cardProfiles != null)
        {
            foreach (CardParameterProfile entry in data.cardProfiles)
            {
                string key = BuildCardProfileKey(entry?.deckId, entry?.cardId ?? 0);
                if (string.IsNullOrEmpty(key)) continue;

                List<ActionUtilityParameterModifier> valid = entry.utilityParameters?
                    .Where(p => p != null && UtilityAIParameters.IsKnown(p.parameter))
                    .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                    .ToList() ?? new List<ActionUtilityParameterModifier>();

                loadedCardProfiles[key] = new CardParameterProfile
                {
                    deckId = entry.deckId,
                    cardId = entry.cardId,
                    cardName = entry.cardName,
                    actionClass = entry.actionClass,
                    scoreBonus = entry.scoreBonus,
                    ignoreSituation = entry.ignoreSituation,
                    utilityParameters = valid
                };
            }
        }
    }
}
