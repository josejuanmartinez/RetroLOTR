using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AIContext
{
    private readonly Board board;
    private readonly List<AIScoredAction> scoredActions = new();
    private readonly List<ArtifactTransferCandidate> artifactTransferCandidates = new();
    private readonly HashSet<string> scoredActionKeys = new();
    private readonly Dictionary<CharacterAction, CardData> actionCardsByAction = new();
    private readonly AIContextPrecomputedData? _precomputed;
    private ResourceSnapshot preSnapshot;
    private Dictionary<PlayableLeader, int> preVictoryPoints;

    public PlayableLeader Leader { get; }
    public Character Character { get; }
    public List<CharacterAction> AvailableActions { get; }
    public EconomyStatus EconomyStatus { get; }

    private EnemyTarget closestEnemy;
    private EnemyTarget closestNonNeutralEnemy;
    private float nearestUnrevealedNpcDistance = float.MaxValue;
    private Hex nearestUnrevealedNpcHex = null;
    private float nearestEnemyCharacterDistance = float.MaxValue;
    private Hex nearestEnemyCharacterHex = null;
    private float nearestEnemyPcOpportunityDistance = float.MaxValue;
    private Hex nearestEnemyPcOpportunityHex = null;
    private float nearestOwnPcLoyaltyRiskDistance = float.MaxValue;
    private Hex nearestOwnPcLoyaltyRiskHex = null;
    private float nearestEnemyPcVulnerabilityDistance = float.MaxValue;
    private Hex nearestEnemyPcVulnerabilityHex = null;
    private float nearestHighValueEnemyCharacterDistance = float.MaxValue;
    private Hex nearestHighValueEnemyCharacterHex = null;
    private float nearestOwnPcFortificationNeedDistance = float.MaxValue;
    private Hex nearestOwnPcFortificationNeedHex = null;
    private float nearestNplRecruitmentDistance = float.MaxValue;
    private Hex nearestNplRecruitmentHex = null;
    private bool needsIndirectApproach = false;
    private float liquidWealth = 0f;
    private float nationPercentageArtifacts = 0;
    private int hiddenArtifactsRemaining;
    public CharacterAction LastChosenAction { get; private set; }
    public AdvisorType LastAdvisor { get; private set; }

    // Set by AITurnController after construction, from the HTN's currently-active
    // PrimitiveTask.TaskId, purely for AIActionLogger traceability (see BuildLogEntry).
    public string ActiveHtnTaskId { get; set; }

    // Set by AITurnController from AIBlackboard.TargetHex — the specific hex behind whichever
    // situational parameter the active primitive task prefers (see GetTargetHexForParameter).
    // GetPreferredMovementTarget() reads this first, so a character actually travels toward
    // and acts on the location that triggered its current strategy rather than only ever
    // acting on wherever it happens to already be standing.
    public Hex ActiveHtnTargetHex { get; set; }

    public AIContext(PlayableLeader leader, Character character, List<CharacterAction> availableActions, Dictionary<CharacterAction, CardData> actionCards = null, AIContextPrecomputedData? precomputed = null)
    {
        Leader = leader;
        Character = character;
        AvailableActions = availableActions ?? new List<CharacterAction>();
        if (actionCards != null)
        {
            actionCardsByAction = actionCards
                .Where(pair => pair.Key != null && pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        board = UnityEngine.Object.FindFirstObjectByType<Board>();

        _precomputed = precomputed;
        ApplyPrecomputedData(precomputed ?? AIContextDataBuilder.Build(leader, character));
        EconomyStatus = EvaluateEconomy();
        preSnapshot = CaptureSnapshot();
        preVictoryPoints = CaptureVictoryPointsSnapshot();
    }

    public async Task<bool> TryExecuteAdvisorActionAsync(AdvisorType advisor)
    {
        ResetScoringData();
        CharacterAction action = PickBestActionForAdvisor(advisor);
        if (action == null) return false;

        return await TryExecuteChosenActionAsync(action, advisor);
    }

    public async Task<bool> TryExecuteBestAvailableActionAsync()
    {
        ResetScoringData();
        CharacterAction action = AvailableActions
            .OrderByDescending(a => ScoreAction(a, AIAdvisorConfig.ResolveAdvisor(a)))
            .FirstOrDefault();

        if (action == null) return false;

        return await TryExecuteChosenActionAsync(action, AIAdvisorConfig.ResolveAdvisor(action));
    }

    // Shared tail of TryExecuteAdvisorActionAsync/TryExecuteBestAvailableActionAsync, also used
    // by AITurnController's full-deck difficulty loop once an action has already been chosen.
    public async Task<bool> TryExecuteChosenActionAsync(CharacterAction action, AdvisorType advisor)
    {
        if (action == null) return false;
        if (!PrepareActionForExecution(action)) return false;
        RecordAction(action, advisor);
        await action.Execute();
        return true;
    }

    public async Task<bool> PassAsync()
    {
        RecordAction(null, AdvisorType.None);
        await Character.Pass();
        return true;
    }

    private void ApplyPrecomputedData(AIContextPrecomputedData data)
    {
        liquidWealth = data.LiquidWealth;
        nationPercentageArtifacts = data.NationPercentageArtifacts;
        closestEnemy = data.ClosestEnemy;
        closestNonNeutralEnemy = data.ClosestNonNeutralEnemy;
        nearestUnrevealedNpcDistance = data.NearestUnrevealedNpcDistance;
        nearestUnrevealedNpcHex = data.NearestUnrevealedNpcHex;
        nearestEnemyCharacterDistance = data.NearestEnemyCharacterDistance;
        nearestEnemyCharacterHex = data.NearestEnemyCharacterHex;
        nearestEnemyPcOpportunityDistance = data.NearestEnemyPcOpportunityDistance;
        nearestEnemyPcOpportunityHex = data.NearestEnemyPcOpportunityHex;
        nearestOwnPcLoyaltyRiskDistance = data.NearestOwnPcLoyaltyRiskDistance;
        nearestOwnPcLoyaltyRiskHex = data.NearestOwnPcLoyaltyRiskHex;
        nearestEnemyPcVulnerabilityDistance = data.NearestEnemyPcVulnerabilityDistance;
        nearestEnemyPcVulnerabilityHex = data.NearestEnemyPcVulnerabilityHex;
        nearestHighValueEnemyCharacterDistance = data.NearestHighValueEnemyCharacterDistance;
        nearestHighValueEnemyCharacterHex = data.NearestHighValueEnemyCharacterHex;
        nearestOwnPcFortificationNeedDistance = data.NearestOwnPcFortificationNeedDistance;
        nearestOwnPcFortificationNeedHex = data.NearestOwnPcFortificationNeedHex;
        nearestNplRecruitmentDistance = data.NearestNplRecruitmentDistance;
        nearestNplRecruitmentHex = data.NearestNplRecruitmentHex;
        needsIndirectApproach = data.NeedsIndirectApproach;
        hiddenArtifactsRemaining = data.HiddenArtifactsRemaining;

        if (data.ArtifactTransferCandidates != null && data.ArtifactTransferCandidates.Count > 0)
        {
            artifactTransferCandidates.Clear();
            artifactTransferCandidates.AddRange(data.ArtifactTransferCandidates);
        }
    }

    private CharacterAction PickBestActionForAdvisor(AdvisorType advisor)
    {
        List<CharacterAction> matches = AvailableActions.Where(a => AIAdvisorConfig.ResolveAdvisor(a) == advisor).ToList();
        if (!matches.Any()) return null;

        return matches.OrderByDescending(a => ScoreAction(a, advisor)).First();
    }

    public float ScoreAction(CharacterAction action, AdvisorType advisor, float advisorBiasBonus = 0f, IReadOnlyList<string> preferredParameters = null)
    {
        float score = advisorBiasBonus;
        ActionScoreFlags scoreFlags = AIAdvisorConfig.GetActionScoreFlags(action);

        // Advisor world-state values are never applied merely because a card
        // belongs to an advisor. A card receives them only through its explicit
        // Card Board utility profile below. Therefore an empty profile means
        // exactly zero Advisor-utility contribution.

        // User-authored flat priority adjustment for this specific action.
        score += AIAdvisorConfig.GetActionScoreBonus(action);

        // A card can opt into any named Advisor parameter explicitly. This is
        // deliberately data-driven: there are no action-name special cases or
        // hidden per-card calculations here.
        if (!scoreFlags.ignoreSituation)
        {
            foreach (ActionUtilityParameterModifier modifier in AIAdvisorConfig.GetActionUtilityParameters(action))
            {
                score += GetUtilityParameter(modifier.parameter) * modifier.multiplier + modifier.bonus;

                // Extra nudge when this card's own authored parameter matches what the active
                // HTN leaf's situation is actually about (HTNPrimitiveTask.PreferredParameters)
                // — stronger evidence than the flat advisorBiasBonus above, which only checked
                // "same advisor" and can't distinguish e.g. root.offense.pick.attack from
                // root.offense.pick.fortify (both Militaristic).
                if (preferredParameters != null && preferredParameters.Contains(modifier.parameter, StringComparer.OrdinalIgnoreCase))
                {
                    score += AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.HTNSituationBonus);
                }
            }
        }

        RecordScoredAction(action, advisor, score);
        return score;
    }

    private float GetDistanceScore(bool allowNeutral)
    {
        EnemyTarget target = allowNeutral ? closestEnemy : closestNonNeutralEnemy;
        if (target.Hex == null && allowNeutral) target = closestNonNeutralEnemy;
        if (target.Hex == null) target = closestEnemy;

        if (target.Hex == null) return 0f;

        float effectiveDistance = target.Distance + (target.IsNeutral ? AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.NeutralTargetExtraDistance) : 0f);
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.EnemyProximityMax) - effectiveDistance);
    }

    private int ResolveCardDifficulty(CharacterAction action)
    {
        if (action == null) return 0;
        if (actionCardsByAction.TryGetValue(action, out CardData card) && card != null)
        {
            return Mathf.Max(0, card.difficulty);
        }
        return 0;
    }

    private bool PrepareActionForExecution(CharacterAction action)
    {
        if (action == null || Leader == null) return false;

        if (!actionCardsByAction.TryGetValue(action, out CardData card) || card == null)
        {
            return false;
        }

        DeckManager deckManager = DeckManager.Instance != null
            ? DeckManager.Instance
            : UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        if (deckManager == null) return false;

        if (!deckManager.TryConsumeActionCardFromFullDeck(Leader, action.GetType().Name, card, out CardData consumedCard))
        {
            return false;
        }

        consumedCard ??= card;
        action.card = consumedCard;
        action.difficulty = Mathf.Max(0, consumedCard.difficulty);
        deckManager.ApplyMapRevealForPlayedCard(Leader, consumedCard);
        Leader.RecordPlayedCard(consumedCard);
        return true;
    }

    private float GetMilitaryEdgeScore()
    {
        if (!Character.IsArmyCommander() || Character.GetArmy() == null)
            return AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.NoArmyPenalty);

        float myStrength = Character.GetArmy().GetOffence();
        EnemyTarget target = closestEnemy.Hex != null ? closestEnemy : closestNonNeutralEnemy;
        if (target.Hex == null || target.Strength <= 0) return 0f;

        float strengthDiff = myStrength - target.Strength;
        float distancePenalty = target.Distance > 1f ? AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.FarTargetPenalty) : 0f;

        if (strengthDiff < 0)
        {
            // needsIndirectApproach is decided once, authoritatively, in
            // AIContextDataBuilder (against OutmatchedStrengthRatio) — not re-derived here
            // against a different, undocumented threshold (this used to set it on any
            // deficit at all, vs. the builder's ratio-buffered definition).
            return Mathf.Max(-10f, strengthDiff / 10f - distancePenalty);
        }

        return Mathf.Clamp(strengthDiff / 20f, -5f, 8f) - distancePenalty;
    }

    private float GetDiplomaticScore()
    {
        if (nearestUnrevealedNpcDistance == float.MaxValue) return 0f;

        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.NpcProximityMax) - nearestUnrevealedNpcDistance);
    }

    private float GetNearbyEnemyCharacterScore()
    {
        if (nearestEnemyCharacterDistance == float.MaxValue) return 0f;
        // Closer enemy characters make intelligence more valuable
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.EnemyCharacterProximityMax) - nearestEnemyCharacterDistance);
    }

    // Target-quality signals: "is there a specific good target nearby right now", computed once
    // in AIContextDataBuilder against an authored qualifying threshold (loyalty/defense/skill),
    // then faded by proximity here exactly like every other *ProximityMax term in this file.
    private float GetEnemyPcOpportunityScore()
    {
        if (nearestEnemyPcOpportunityDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticEnemyPcOpportunityProximityMax) - nearestEnemyPcOpportunityDistance);
    }

    private float GetOwnPcLoyaltyRiskScore()
    {
        if (nearestOwnPcLoyaltyRiskDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticOwnPcLoyaltyRiskProximityMax) - nearestOwnPcLoyaltyRiskDistance);
    }

    private float GetEnemyPcVulnerabilityScore()
    {
        if (nearestEnemyPcVulnerabilityDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceEnemyPcVulnerabilityProximityMax) - nearestEnemyPcVulnerabilityDistance);
    }

    private float GetHighValueEnemyCharacterScore()
    {
        if (nearestHighValueEnemyCharacterDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceHighValueEnemyCharacterProximityMax) - nearestHighValueEnemyCharacterDistance);
    }

    private float GetOwnPcFortificationNeedScore()
    {
        if (nearestOwnPcFortificationNeedDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MilitaristicOwnPcFortificationProximityMax) - nearestOwnPcFortificationNeedDistance);
    }

    private float GetNplRecruitmentScore()
    {
        if (nearestNplRecruitmentDistance == float.MaxValue) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticNplRecruitmentProximityMax) - nearestNplRecruitmentDistance);
    }

    // Literal count of Spell-derived actions this character can currently play — the same
    // "is there a legal opportunity of this shape at all" gate GetArtifactTransferScore uses
    // for TransferArtifact (AvailableActions.Any(a => a is TransferArtifact)) below, just a
    // count instead of a bool so card profiles can scale by how many options exist.
    private float GetSpellOpportunityScore() => AvailableActions.Count(a => a is Spell);

    // The nearest non-neutral enemy PC/army is within Militaristic's proximity window
    // (Targeting.EnemyProximityMax) — proximity alone, independent of who would win a fight.
    // Read by HTNRegistry's Militaristic.EnemyNear/Danger predicates.
    public bool IsEnemyNear => GetDistanceScore(false) > 0f;

    // The nearest enemy target outguns this leader's army by AIContextDataBuilder's
    // OutmatchedStrengthRatio (0 strength while leading no army, so "no army" is always
    // outmatched if any enemy exists) — computed once, authoritatively, at context build time.
    // Read by HTNRegistry's Militaristic.Danger predicate and already feeds the Intelligence/
    // Diplomatic "indirect approach" bonuses below.
    public bool IsOutmatched => needsIndirectApproach;

    // World-state-only appeal of pursuing this advisor right now, independent of any specific
    // card — the same situational terms ScoreAction adds per-card above, minus the handful
    // tied to a concrete action (Intelligence's Scout Area bonus, Magic's artifact-transfer
    // search — both added separately in ScoreAction). This is what HTNRegistry's Viable
    // predicates read: the HTN's strategy choice is driven by literally the same weights and
    // formula the Utility scorer uses to pick cards, not a separate hand-coded sensing layer.
    // All terms degrade to 0 (or a fixed penalty, e.g. NoArmyPenalty) with no valid target, so
    // no separate "HasTarget" boolean is needed — a low/negative viability already means "no."
    public float GetAdvisorViability(AdvisorType advisor)
    {
        switch (advisor)
        {
            // Economic has no case here: its situational term used to be the now-removed
            // tier-reactive Economy Critical/Weak/Stable Bonus, which decided nothing —
            // falls through to default (0f). Economic cards are steered by HTNBiasBonus
            // (via root.recover) and the manual per-action bonus only.
            case AdvisorType.Militaristic:
                return GetDistanceScore(false) + GetMilitaryEdgeScore();
            case AdvisorType.Intelligence:
            {
                float viability = GetNearbyEnemyCharacterScore() + GetDistanceScore(true);
                if (needsIndirectApproach) viability += AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceOutmatchedBonus);
                viability += GetDistanceScore(true) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceEnemyPressureWeight);
                viability += GetLeaderRoleStrength(c => c.GetAgent()) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceAgentStrengthWeight);
                return viability;
            }
            case AdvisorType.Magic:
                return (1f - nationPercentageArtifacts) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.ArtifactScarcityWeight)
                    + GetDistanceScore(true)
                    + hiddenArtifactsRemaining * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicHiddenArtifactsWeight)
                    + GetLeaderRoleStrength(c => c.GetMage()) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.MagicMageStrengthWeight);
            case AdvisorType.Diplomatic:
            {
                float viability = GetDiplomaticScore() + GetDistanceScore(true);
                if (needsIndirectApproach) viability += AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticOutmatchedBonus);
                viability += GetDistanceScore(true) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticEnemyPressureWeight);
                viability += GetLeaderRoleStrength(c => c.GetEmmissary()) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticEmissaryStrengthWeight);
                return viability;
            }
            case AdvisorType.Logistics:
                return GetLogisticsProximityScore();
            case AdvisorType.Disruption:
                return GetDistanceScore(true);
            default:
                return 0f;
        }
    }

    // Direct, named utility observations. HTN predicates and card profiles both
    // call this same public API, so the inspector can always show exactly what
    // value caused a branch or score contribution.
    public float GetUtilityParameter(string parameter)
    {
        return parameter switch
        {
            AIUtilityParameters.MilitaristicEnemyPressure => GetDistanceScore(false),
            AIUtilityParameters.MilitaristicMilitaryEdge => GetMilitaryEdgeScore(),
            AIUtilityParameters.EconomicLiquidWealth => liquidWealth,
            AIUtilityParameters.DiplomaticIndirectSafety => needsIndirectApproach ? AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.DiplomaticOutmatchedBonus) : 0f,
            AIUtilityParameters.IntelligenceEnemyCharacter => GetNearbyEnemyCharacterScore(),
            AIUtilityParameters.IntelligenceIndirectSafety => needsIndirectApproach ? AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.IntelligenceOutmatchedBonus) : 0f,
            AIUtilityParameters.MagicArtifactScarcity => (1f - nationPercentageArtifacts) * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.ArtifactScarcityWeight),
            AIUtilityParameters.MagicArtifactTransfer => GetArtifactTransferScore(),
            AIUtilityParameters.MagicEnemyPressure => GetDistanceScore(true),
            AIUtilityParameters.MagicHiddenArtifacts => hiddenArtifactsRemaining,
            AIUtilityParameters.MagicMageStrength => GetLeaderRoleStrength(c => c.GetMage()),
            AIUtilityParameters.DiplomaticEnemyPressure => GetDistanceScore(true),
            AIUtilityParameters.DiplomaticEmissaryStrength => GetLeaderRoleStrength(c => c.GetEmmissary()),
            AIUtilityParameters.IntelligenceEnemyPressure => GetDistanceScore(true),
            AIUtilityParameters.IntelligenceAgentStrength => GetLeaderRoleStrength(c => c.GetAgent()),
            AIUtilityParameters.LogisticsReachNpc => GetLogisticsProximityScore(nearestUnrevealedNpcHex),
            AIUtilityParameters.LogisticsInterceptEnemy => GetLogisticsProximityScore(closestNonNeutralEnemy.Hex ?? closestEnemy.Hex),
            AIUtilityParameters.LogisticsReachEnemyCharacter => GetLogisticsProximityScore(nearestEnemyCharacterHex),
            AIUtilityParameters.LogisticsHealingNeed => GetHealingNeedScore(),
            AIUtilityParameters.DisruptionEnemyPressure => GetDistanceScore(true),
            AIUtilityParameters.DiplomaticEnemyPcOpportunity => GetEnemyPcOpportunityScore(),
            AIUtilityParameters.DiplomaticOwnPcLoyaltyRisk => GetOwnPcLoyaltyRiskScore(),
            AIUtilityParameters.IntelligenceEnemyPcVulnerability => GetEnemyPcVulnerabilityScore(),
            AIUtilityParameters.IntelligenceHighValueEnemyCharacter => GetHighValueEnemyCharacterScore(),
            AIUtilityParameters.MagicSpellOpportunity => GetSpellOpportunityScore(),
            AIUtilityParameters.MilitaristicOwnPcFortificationNeed => GetOwnPcFortificationNeedScore(),
            AIUtilityParameters.DiplomaticNplRecruitment => GetNplRecruitmentScore(),
            // Deliberately the same formula as MilitaristicOwnPcFortificationNeed above — see
            // the constant's doc comment in AdvisorConfig.cs.
            AIUtilityParameters.MilitaristicOwnPcDefenderNeed => GetOwnPcFortificationNeedScore(),
            AIUtilityParameters.EconomicMithrilInsufficient => GetResourceInsufficientScore(ProducesEnum.mithril, AIAdvisorConfig.Keys.EconomicMithrilInsufficientBelow),
            AIUtilityParameters.EconomicMithrilSurplus => GetResourceSurplusScore(ProducesEnum.mithril, AIAdvisorConfig.Keys.EconomicMithrilSurplusAbove),
            AIUtilityParameters.EconomicSteelInsufficient => GetResourceInsufficientScore(ProducesEnum.steel, AIAdvisorConfig.Keys.EconomicSteelInsufficientBelow),
            AIUtilityParameters.EconomicSteelSurplus => GetResourceSurplusScore(ProducesEnum.steel, AIAdvisorConfig.Keys.EconomicSteelSurplusAbove),
            AIUtilityParameters.EconomicIronInsufficient => GetResourceInsufficientScore(ProducesEnum.iron, AIAdvisorConfig.Keys.EconomicIronInsufficientBelow),
            AIUtilityParameters.EconomicIronSurplus => GetResourceSurplusScore(ProducesEnum.iron, AIAdvisorConfig.Keys.EconomicIronSurplusAbove),
            AIUtilityParameters.EconomicMountsInsufficient => GetResourceInsufficientScore(ProducesEnum.mounts, AIAdvisorConfig.Keys.EconomicMountsInsufficientBelow),
            AIUtilityParameters.EconomicMountsSurplus => GetResourceSurplusScore(ProducesEnum.mounts, AIAdvisorConfig.Keys.EconomicMountsSurplusAbove),
            AIUtilityParameters.EconomicTimberInsufficient => GetResourceInsufficientScore(ProducesEnum.timber, AIAdvisorConfig.Keys.EconomicTimberInsufficientBelow),
            AIUtilityParameters.EconomicTimberSurplus => GetResourceSurplusScore(ProducesEnum.timber, AIAdvisorConfig.Keys.EconomicTimberSurplusAbove),
            AIUtilityParameters.EconomicLeatherInsufficient => GetResourceInsufficientScore(ProducesEnum.leather, AIAdvisorConfig.Keys.EconomicLeatherInsufficientBelow),
            AIUtilityParameters.EconomicLeatherSurplus => GetResourceSurplusScore(ProducesEnum.leather, AIAdvisorConfig.Keys.EconomicLeatherSurplusAbove),
            AIUtilityParameters.EconomicGoldInsufficient => GetResourceInsufficientScore(ProducesEnum.gold, AIAdvisorConfig.Keys.EconomyCriticalBelow),
            AIUtilityParameters.EconomicGoldSurplus => GetResourceSurplusScore(ProducesEnum.gold, AIAdvisorConfig.Keys.EconomyStableBelow),
            _ => 0f
        };
    }

    // The specific hex behind a given situational parameter's score, where one exists — lets
    // AITurnController.AdvanceHtnStrategy turn "this parameter is what's driving the active
    // task" into an actual travel destination (stored on AIBlackboard.TargetHex), instead of
    // the parameter's score being a number with no location attached. Parameters with no
    // "go there" concept (nation-wide Economic stockpile signals; Magic/Logistics signals that
    // resolve wherever the character already is) correctly return null — GetPreferredMovementTarget
    // falls back to its own generic chase-logic in that case.
    public Hex GetTargetHexForParameter(string parameter)
    {
        return parameter switch
        {
            AIUtilityParameters.MilitaristicEnemyPressure => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.MilitaristicMilitaryEdge => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.MilitaristicOwnPcFortificationNeed => nearestOwnPcFortificationNeedHex,
            AIUtilityParameters.MilitaristicOwnPcDefenderNeed => nearestOwnPcFortificationNeedHex,
            AIUtilityParameters.DiplomaticEnemyPressure => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.DiplomaticEnemyPcOpportunity => nearestEnemyPcOpportunityHex,
            AIUtilityParameters.DiplomaticOwnPcLoyaltyRisk => nearestOwnPcLoyaltyRiskHex,
            AIUtilityParameters.DiplomaticNplRecruitment => nearestNplRecruitmentHex,
            AIUtilityParameters.IntelligenceEnemyCharacter => nearestEnemyCharacterHex,
            AIUtilityParameters.IntelligenceEnemyPressure => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.IntelligenceEnemyPcVulnerability => nearestEnemyPcVulnerabilityHex,
            AIUtilityParameters.IntelligenceHighValueEnemyCharacter => nearestHighValueEnemyCharacterHex,
            AIUtilityParameters.MagicEnemyPressure => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.LogisticsReachNpc => nearestUnrevealedNpcHex,
            AIUtilityParameters.LogisticsInterceptEnemy => closestNonNeutralEnemy.Hex ?? closestEnemy.Hex,
            AIUtilityParameters.LogisticsReachEnemyCharacter => nearestEnemyCharacterHex,
            AIUtilityParameters.DisruptionEnemyPressure => closestEnemy.Hex ?? closestNonNeutralEnemy.Hex,
            _ => null
        };
    }

    // Does this character have at least one card, anywhere in the leader's full deck, whose
    // action both resolves to the given advisor and is role-eligible for this character right
    // now (CharacterAction.IsRoleEligible — Commander/Agent/Emmissary/Mage skill gates, a
    // stable trait, not momentary affordability)? Read by HTNPlanner.Decompose to skip a
    // branch that would only ever waste its bias on a character who can never act on it —
    // deliberately role-only, not full CardData.EvaluatePlayability, since a temporarily-poor
    // but otherwise-capable character should still be considered eligible here.
    public bool HasEligibleCard(AdvisorType advisor)
    {
        if (Leader == null || Character == null) return false;

        DeckManager deckManager = DeckManager.Instance != null
            ? DeckManager.Instance
            : UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        ActionsManager actionsManager = UnityEngine.Object.FindFirstObjectByType<ActionsManager>();
        if (deckManager == null || actionsManager == null) return false;

        foreach (CardData card in deckManager.GetFullDeck(Leader))
        {
            if (card == null || card.IsEncounterCard()) continue;

            string actionRef = AITurnController.NormalizeActionRef(card.GetActionRef());
            if (string.IsNullOrWhiteSpace(actionRef)) continue;

            CharacterAction action = AITurnController.ResolveActionByRef(actionRef, actionsManager);
            if (action == null) continue;

            action.Initialize(Character, card);
            if (AIAdvisorConfig.ResolveAdvisor(action) != advisor) continue;
            if (action.IsRoleEligible(Character)) return true;
        }

        return false;
    }

    // Leader's own stockpile of a material falling below/rising above an authored threshold —
    // independent of StoresManager's market-wide supply factor (which prices Buy/Sell, not
    // whether the AI personally wants more or less of a given material).
    private float GetResourceInsufficientScore(ProducesEnum resource, string insufficientBelowKey)
    {
        if (Leader == null) return 0f;
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(insufficientBelowKey) - Leader.GetResourceAmount(resource));
    }

    private float GetResourceSurplusScore(ProducesEnum resource, string surplusAboveKey)
    {
        if (Leader == null) return 0f;
        return Mathf.Max(0f, Leader.GetResourceAmount(resource) - AIAdvisorConfig.GetWeight(surplusAboveKey));
    }

    private float GetLeaderRoleStrength(Func<Character, int> level)
    {
        return Leader?.controlledCharacters
            ?.Where(c => c != null && !c.killed)
            .Sum(c => Mathf.Max(0, level(c))) ?? 0f;
    }

    private void RecordAction(CharacterAction action, AdvisorType advisor)
    {
        LastChosenAction = action;
        LastAdvisor = advisor;
    }

    public Hex GetPreferredMovementTarget()
    {
        // Authoritative: the specific hex behind whichever situational parameter the active
        // HTN task actually prefers (see GetTargetHexForParameter), resolved once in
        // AITurnController.AdvanceHtnStrategy and carried on the blackboard/this context. This
        // is what makes the active strategy (danger, an opportunity, a target to intercept)
        // something the character actually travels to and acts on, rather than only ever
        // acting on whatever hex it happens to already be standing on.
        if (ActiveHtnTargetHex != null) return ActiveHtnTargetHex;

        // Fallback for tasks with no specific location (Economic recovery, the generic
        // fallback branch) — opportunistic chase, same as before this existed.
        // Priority: own PC needing defense -> unrevealed NPC PCs -> strongest non-neutral enemy -> any enemy -> nearest enemy character.
        if (nearestOwnPcFortificationNeedHex != null) return nearestOwnPcFortificationNeedHex;
        if (nearestUnrevealedNpcHex != null) return nearestUnrevealedNpcHex;
        if (closestNonNeutralEnemy.Hex != null) return closestNonNeutralEnemy.Hex;
        if (closestEnemy.Hex != null) return closestEnemy.Hex;
        if (nearestEnemyCharacterHex != null) return nearestEnemyCharacterHex;
        return null;
    }

    private float GetLogisticsProximityScore()
    {
        return GetLogisticsProximityScore(GetPreferredMovementTarget());
    }

    private float GetLogisticsProximityScore(Hex target)
    {
        if (target == null || Character == null || Character.hex == null) return 0f;

        float distance = Vector2.Distance(Character.hex.v2, target.v2);
        // Reward being close to the intended destination; closer hexes give larger boosts
        return Mathf.Max(0f, AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsProximityMax)
            - distance * AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsDistancePenaltyPerHex));
    }

    // Live count of wounded allies (including self) sharing this character's hex — the same
    // "no board scan needed, just look at what's already known" shape as GetSpellOpportunityScore
    // above. A raw count, like Magic.HiddenArtifacts, so card profiles choose their own scale.
    private float GetHealingNeedScore()
    {
        if (Character == null || Character.hex == null || Character.hex.characters == null) return 0f;

        float healthBelow = AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.LogisticsHealingNeedHealthBelow);
        Leader owner = Character.GetOwner();
        AlignmentEnum alignment = Character.GetAlignment();
        return Character.hex.characters.Count(c => c != null && !c.killed && c.health < healthBelow
            && (c.GetOwner() == owner || (c.GetAlignment() == alignment && alignment != AlignmentEnum.neutral)));
    }

    private float GetArtifactTransferScore()
    {
        if (Character == null || Character.hex == null) return 0f;

        // If we have precomputed candidates, reuse them and simply adjust by availability
        if (_precomputed.HasValue && _precomputed.Value.ArtifactTransferCandidates != null && _precomputed.Value.ArtifactTransferCandidates.Count > 0)
        {
            artifactTransferCandidates.Clear();
            artifactTransferCandidates.AddRange(_precomputed.Value.ArtifactTransferCandidates);
            bool canTransferCached = AvailableActions.Any(a => a is TransferArtifact);
            return canTransferCached ? Mathf.Max(0f, _precomputed.Value.BestArtifactTransferScore) : 0f;
        }

        bool canTransfer = AvailableActions.Any(a => a is TransferArtifact);
        if (!canTransfer) return 0f;

        List<CardData> transferable = Character.objects.Where(a => a != null && a.transferable).ToList();
        if (transferable.Count == 0) return 0f;

        if (board == null || board.hexes == null) return 0f;

        List<Character> friendlies = board.hexes.Values
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && ch.hex != null && ch != Character &&
                         (ch.GetOwner() == Character.GetOwner() ||
                          (ch.GetAlignment() == Character.GetAlignment() && ch.GetAlignment() != AlignmentEnum.neutral)))
            .ToList();
        if (friendlies.Count == 0) return 0f;

        artifactTransferCandidates.Clear();
        float bestScore = 0f;
        foreach (CardData art in transferable)
        {
            foreach (Character target in friendlies)
            {
                float score = 0f;
                float distance = Character.hex != null && target.hex != null
                    ? Vector2.Distance(Character.hex.v2, target.hex.v2)
                    : float.MaxValue;

                // Skill boosts help low-skill targets more
                score += art.commanderBonus > 0 ? art.commanderBonus * 2f + Mathf.Max(0, 5 - target.GetCommander()) : 0f;
                score += art.agentBonus > 0 ? art.agentBonus * 2f + Mathf.Max(0, 5 - target.GetAgent()) : 0f;
                score += art.emmissaryBonus > 0 ? art.emmissaryBonus * 2f + Mathf.Max(0, 5 - target.GetEmmissary()) : 0f;
                score += art.mageBonus > 0 ? art.mageBonus * 2f + Mathf.Max(0, 5 - target.GetMage()) : 0f;

                // Combat bonuses are more valuable on army commanders
                if (target.IsArmyCommander())
                {
                    score += art.GetAttackBonus() * 3f;
                    score += art.GetDefenseBonus() * 2f;
                }

                // Small penalty if target already excels in the boosted area
                if (art.commanderBonus > 0 && target.GetCommander() > 3) score -= 2f;
                if (art.agentBonus > 0 && target.GetAgent() > 3) score -= 2f;
                if (art.emmissaryBonus > 0 && target.GetEmmissary() > 3) score -= 2f;
                if (art.mageBonus > 0 && target.GetMage() > 3) score -= 2f;

                // Distance penalty so nearer recipients are favored
                if (distance < float.MaxValue)
                {
                    score -= distance * 2f;
                }
                else
                {
                    score -= 5f;
                }

                artifactTransferCandidates.Add(new ArtifactTransferCandidate(art.name, target.characterName, score, distance));
                bestScore = Mathf.Max(bestScore, score);
            }
        }

        // Reward scenarios where at least one good transfer exists
        return Mathf.Max(0f, bestScore / 3f);
    }

    private void RecordScoredAction(CharacterAction action, AdvisorType advisor, float score)
    {
        if (action == null) return;
        string key = $"{action.actionName}|{advisor}";
        if (scoredActionKeys.Contains(key)) return;
        scoredActionKeys.Add(key);
        float targetDistance = -1f;
        Hex preferred = GetPreferredMovementTarget();
        if (preferred != null && Character != null && Character.hex != null)
        {
            targetDistance = Vector2.Distance(Character.hex.v2, preferred.v2);
        }
        scoredActions.Add(new AIScoredAction(action.actionName, advisor.ToString(), score, targetDistance));
    }

    private void ResetScoringData()
    {
        scoredActions.Clear();
        scoredActionKeys.Clear();
        artifactTransferCandidates.Clear();
    }

    public AIActionLogEntry BuildLogEntry()
    {
        // Refresh enemy target cache after action for post-state measurements
        CacheEnemyTargets();
        ResourceSnapshot post = CaptureSnapshot();
        Hex preferred = GetPreferredMovementTarget();
        Leader owner = Character != null ? Character.GetOwner() : null;
        Army army = Character != null ? Character.GetArmy() : null;
        TargetInfo targetInfo = GetTargetInfo(preferred);
        Dictionary<PlayableLeader, int> postVictoryPoints = CaptureVictoryPointsSnapshot();
        int preVpSelf = GetVictoryPoints(preVictoryPoints, Leader);
        int postVpSelf = GetVictoryPoints(postVictoryPoints, Leader);
        return new AIActionLogEntry
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            turn = UnityEngine.Object.FindFirstObjectByType<Game>()?.turn ?? -1,
            leaderName = Leader?.characterName,
            leaderAlignment = Leader?.GetAlignment().ToString(),
            characterName = Character?.characterName,
            characterAlignment = Character?.GetAlignment().ToString(),
            armyCommander = Character != null && Character.IsArmyCommander(),
            commander = Character?.GetCommander() ?? 0,
            agent = Character?.GetAgent() ?? 0,
            emmissary = Character?.GetEmmissary() ?? 0,
            mage = Character?.GetMage() ?? 0,
            armyOffence = army != null ? army.GetOffence() : 0,
            armyDefence = army != null ? army.GetDefence() : 0,
            health = Character?.health ?? 0,
            preCommander = preSnapshot.commander,
            preAgent = preSnapshot.agent,
            preEmmissary = preSnapshot.emmissary,
            preMage = preSnapshot.mage,
            preArmyOffence = preSnapshot.armyOffence,
            preArmyDefence = preSnapshot.armyDefence,
            preHealth = preSnapshot.health,
            commanderDelta = (Character?.GetCommander() ?? 0) - preSnapshot.commander,
            agentDelta = (Character?.GetAgent() ?? 0) - preSnapshot.agent,
            emmissaryDelta = (Character?.GetEmmissary() ?? 0) - preSnapshot.emmissary,
            mageDelta = (Character?.GetMage() ?? 0) - preSnapshot.mage,
            armyOffenceDelta = (army != null ? army.GetOffence() : 0) - preSnapshot.armyOffence,
            armyDefenceDelta = (army != null ? army.GetDefence() : 0) - preSnapshot.armyDefence,
            healthDelta = (Character?.health ?? 0) - preSnapshot.health,
            goldBuffer = owner != null ? owner.goldAmount : 0,
            goldPerTurn = 0,
            leather = owner != null ? owner.leatherAmount : 0,
            timber = owner != null ? owner.timberAmount : 0,
            iron = owner != null ? owner.ironAmount : 0,
            mounts = owner != null ? owner.mountsAmount : 0,
            mithril = owner != null ? owner.mithrilAmount : 0,
            leatherPerTurn = owner != null ? owner.GetLeatherPerTurn() : 0,
            timberPerTurn = owner != null ? owner.GetTimberPerTurn() : 0,
            ironPerTurn = owner != null ? owner.GetIronPerTurn() : 0,
            mountsPerTurn = owner != null ? owner.GetMountsPerTurn() : 0,
            mithrilPerTurn = owner != null ? owner.GetMithrilPerTurn() : 0,
            preGoldBuffer = preSnapshot.gold,
            preGoldPerTurn = preSnapshot.goldPerTurn,
            preLeather = preSnapshot.leather,
            preTimber = preSnapshot.timber,
            preIron = preSnapshot.iron,
            preMounts = preSnapshot.mounts,
            preMithril = preSnapshot.mithril,
            preLeatherPerTurn = preSnapshot.leatherPerTurn,
            preTimberPerTurn = preSnapshot.timberPerTurn,
            preIronPerTurn = preSnapshot.ironPerTurn,
            preMountsPerTurn = preSnapshot.mountsPerTurn,
            preMithrilPerTurn = preSnapshot.mithrilPerTurn,
            goldDelta = (owner != null ? owner.goldAmount : 0) - preSnapshot.gold,
            leatherDelta = (owner != null ? owner.leatherAmount : 0) - preSnapshot.leather,
            timberDelta = (owner != null ? owner.timberAmount : 0) - preSnapshot.timber,
            ironDelta = (owner != null ? owner.ironAmount : 0) - preSnapshot.iron,
            mountsDelta = (owner != null ? owner.mountsAmount : 0) - preSnapshot.mounts,
            mithrilDelta = (owner != null ? owner.mithrilAmount : 0) - preSnapshot.mithril,
            goldPerTurnDelta = 0 - preSnapshot.goldPerTurn,
            leatherPerTurnDelta = (owner != null ? owner.GetLeatherPerTurn() : 0) - preSnapshot.leatherPerTurn,
            timberPerTurnDelta = (owner != null ? owner.GetTimberPerTurn() : 0) - preSnapshot.timberPerTurn,
            ironPerTurnDelta = (owner != null ? owner.GetIronPerTurn() : 0) - preSnapshot.ironPerTurn,
            mountsPerTurnDelta = (owner != null ? owner.GetMountsPerTurn() : 0) - preSnapshot.mountsPerTurn,
            mithrilPerTurnDelta = (owner != null ? owner.GetMithrilPerTurn() : 0) - preSnapshot.mithrilPerTurn,
            economyStatus = EconomyStatus.ToString(),
            needsIndirect = needsIndirectApproach,
            nationArtifactsShare = nationPercentageArtifacts,
            nearestNpcDistance = nearestUnrevealedNpcDistance,
            nearestEnemyCharacterDistance = nearestEnemyCharacterDistance,
            nearestEnemyStrength = closestEnemy.Strength,
            nearestNonNeutralStrength = closestNonNeutralEnemy.Strength,
            preNearestEnemyStrength = preSnapshot.nearestEnemyStrength,
            preNearestNonNeutralStrength = preSnapshot.nearestNonNeutralStrength,
            nearestEnemyStrengthDelta = closestEnemy.Strength - preSnapshot.nearestEnemyStrength,
            nearestNonNeutralStrengthDelta = closestNonNeutralEnemy.Strength - preSnapshot.nearestNonNeutralStrength,
            targetOwnerName = targetInfo.name,
            targetOwnerAlignment = targetInfo.alignment,
            targetOwnerType = targetInfo.type,
            preferredTargetType = preferred != null ? preferred.GetPC() != null ? "PC" : "Hex" : "None",
            preferredTarget = preferred != null ? preferred.v2 : Vector2Int.one * -1,
            preferredTargetDistance = preferred != null && Character != null && Character.hex != null ? Vector2.Distance(Character.hex.v2, preferred.v2) : -1f,
            actionName = LastChosenAction != null ? LastChosenAction.actionName : Pass.ActionRef,
            advisorType = LastAdvisor.ToString(),
            activeHtnTaskId = ActiveHtnTaskId,
            actionDifficulty = LastChosenAction != null ? ResolveCardDifficulty(LastChosenAction) : 0,
            actionGoldCost = LastChosenAction != null ? LastChosenAction.GetGoldCost() : 0,
            scoredActions = scoredActions.Select(sa => $"{sa.actionName}|{sa.advisor}|{sa.score:0.00}|{sa.targetDistance:0.00}").ToList(),
            artifactTransferCandidates = artifactTransferCandidates.Select(c => $"{c.artifactName}->{c.targetName}|{c.score:0.00}|{c.distance:0.00}").ToList(),
            victoryPointsSelfBefore = preVpSelf,
            victoryPointsSelfAfter = postVpSelf,
            victoryPointsSelfDelta = postVpSelf - preVpSelf,
            victoryPointsOpponentDeltas = BuildOpponentVpDeltas(postVictoryPoints, preVictoryPoints, Leader)
        };
    }

    private EconomyStatus EvaluateEconomy()
    {
        if (Leader == null) return EconomyStatus.Stable;
        return AIAdvisorConfig.EvaluateEconomyStatus(liquidWealth);
    }

    // Real liquid wealth: stored gold + every held resource valued at its current market
    // sell price (StoresManager — a shared, supply-driven market, so this number moves with
    // the market too, not just with what the leader holds). This game has no passive income
    // of any kind (Leader.GetXPerTurn() are all hardcoded-0 stubs, never overridden anywhere;
    // NewTurn() never grants gold or resources) — there is nothing else to measure.
    public static float CalculateLiquidWealth(Leader leader, StoresManager stores)
    {
        if (leader == null) return 0f;
        float wealth = leader.goldAmount;
        if (stores == null) return wealth;

        wealth += leader.leatherAmount * stores.GetSellPricePerUnit(ProducesEnum.leather);
        wealth += leader.mountsAmount * stores.GetSellPricePerUnit(ProducesEnum.mounts);
        wealth += leader.timberAmount * stores.GetSellPricePerUnit(ProducesEnum.timber);
        wealth += leader.ironAmount * stores.GetSellPricePerUnit(ProducesEnum.iron);
        wealth += leader.steelAmount * stores.GetSellPricePerUnit(ProducesEnum.steel);
        wealth += leader.mithrilAmount * stores.GetSellPricePerUnit(ProducesEnum.mithril);
        return wealth;
    }

    private void CacheEnemyTargets()
    {
        closestEnemy = new EnemyTarget(null, float.MaxValue, false, 0f);
        closestNonNeutralEnemy = new EnemyTarget(null, float.MaxValue, false, 0f);

        if (board == null || Character == null || Character.hex == null) return;

        IEnumerable<Hex> hexes = board.hexes != null ? board.hexes.Values : Enumerable.Empty<Hex>();

        float myStrength = Character.IsArmyCommander() && Character.GetArmy() != null ? Character.GetArmy().GetOffence() : 0f;

        foreach (Hex hex in hexes)
        {
            bool hasEnemyCharacter = hex.characters.Any(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner()));
            Leader enemyLeader = GetEnemyLeaderOnHex(hex);
            if (enemyLeader == null) continue;

            bool isNeutral = enemyLeader.GetAlignment() == AlignmentEnum.neutral;
            float distance = Vector2.Distance(Character.hex.v2, hex.v2);
            float distanceScore = distance + (isNeutral ? 2f : 0f);
            float strength = EstimateEnemyStrength(hex);

            if (distanceScore < closestEnemy.Score)
            {
                closestEnemy = new EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (!isNeutral && distance < closestNonNeutralEnemy.Distance)
            {
                closestNonNeutralEnemy = new EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (hasEnemyCharacter && distance < nearestEnemyCharacterDistance)
            {
                nearestEnemyCharacterDistance = distance;
                nearestEnemyCharacterHex = hex;
            }
        }

        EnemyTarget best = closestNonNeutralEnemy.Hex != null ? closestNonNeutralEnemy : closestEnemy;
        float outmatchedRatio = AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.OutmatchedStrengthRatio);
        if (best.Hex != null && best.Strength > myStrength * outmatchedRatio) needsIndirectApproach = true;
    }

    private void CacheNpcTargets()
    {
        if (board == null || Character == null || Character.hex == null) return;

        foreach (Hex hex in board.hexes.Values)
        {
            PC pc = hex.GetPC();
            if (pc == null) continue;
            if (pc.owner is not NonPlayableLeader npc) continue;
            if (npc.IsRevealedToLeader(GameObject.FindFirstObjectByType<Game>().currentlyPlaying)) continue;

            float distance = Vector2.Distance(Character.hex.v2, hex.v2);
            if (distance < nearestUnrevealedNpcDistance)
            {
                nearestUnrevealedNpcDistance = distance;
                nearestUnrevealedNpcHex = hex;
            }
        }
    }

    private Leader GetEnemyLeaderOnHex(Hex hex)
    {
        if (hex == null) return null;

        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner)) return pc.owner;

        Character enemyCharacter = hex.characters.FirstOrDefault(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner()));
        if (enemyCharacter != null) return enemyCharacter.GetOwner();

        return null;
    }

    private float EstimateEnemyStrength(Hex hex)
    {
        if (hex == null) return 0f;

        int strength = 0;
        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner))
        {
            strength = Mathf.Max(strength, pc.GetDefense());
        }

        if (hex.armies != null)
        {
            foreach (Army army in hex.armies)
            {
                if (army == null || army.commander == null) continue;
                if (army.commander.GetOwner() == null) continue;
                if (!IsEnemy(army.commander.GetOwner())) continue;
                strength = Mathf.Max(strength, army.GetDefence());
            }
        }

        return strength;
    }

    private float CalculateNationArtifacts()
    {
        if (Leader == null) return 0;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        int catalogCount = deckManager?.GetObjectCardCount() ?? 0;
        return Leader.controlledCharacters.Sum(ch => ch != null ? ch.objects.Count * 1f : 0f) / Math.Max(1f, catalogCount * 1f);
    }

    private bool IsEnemy(Leader other)
    {
        if (other == null || Leader == null) return false;
        if (other == Leader) return false;

        AlignmentEnum myAlignment = Leader.GetAlignment();
        AlignmentEnum otherAlignment = other.GetAlignment();

        if (myAlignment == otherAlignment && myAlignment != AlignmentEnum.neutral) return false;

        // Anything that is not aligned with us is an enemy; neutral is lowest priority but still an enemy.
        return otherAlignment != myAlignment || otherAlignment == AlignmentEnum.neutral;
    }

    private static bool NameContains(string source, string needle)
    {
        return source != null && source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public struct AIContextPrecomputedData
    {
        public EnemyTarget ClosestEnemy;
        public EnemyTarget ClosestNonNeutralEnemy;
        public float NearestUnrevealedNpcDistance;
        public Hex NearestUnrevealedNpcHex;
        public float NearestEnemyCharacterDistance;
        public Hex NearestEnemyCharacterHex;
        public float NearestEnemyPcOpportunityDistance;
        public Hex NearestEnemyPcOpportunityHex;
        public float NearestOwnPcLoyaltyRiskDistance;
        public Hex NearestOwnPcLoyaltyRiskHex;
        public float NearestEnemyPcVulnerabilityDistance;
        public Hex NearestEnemyPcVulnerabilityHex;
        public float NearestHighValueEnemyCharacterDistance;
        public Hex NearestHighValueEnemyCharacterHex;
        public float NearestOwnPcFortificationNeedDistance;
        public Hex NearestOwnPcFortificationNeedHex;
        public float NearestNplRecruitmentDistance;
        public Hex NearestNplRecruitmentHex;
        public bool NeedsIndirectApproach;
        public float LiquidWealth;
        public float NationPercentageArtifacts;
        public int HiddenArtifactsRemaining;
        public List<ArtifactTransferCandidate> ArtifactTransferCandidates;
        public float BestArtifactTransferScore;
    }

    public readonly struct EnemyTarget
    {
        public Hex Hex { get; }
        public float Distance { get; }
        public bool IsNeutral { get; }
        public float Strength { get; }
        public float Score => Distance + (IsNeutral ? 2f : 0f);

        public EnemyTarget(Hex hex, float distance, bool isNeutral, float strength)
        {
            Hex = hex;
            Distance = distance;
            IsNeutral = isNeutral;
            Strength = strength;
        }
    }

    private readonly struct AIScoredAction
    {
        public readonly string actionName;
        public readonly string advisor;
        public readonly float score;
        public readonly float targetDistance;

        public AIScoredAction(string actionName, string advisor, float score, float targetDistance)
        {
            this.actionName = actionName;
            this.advisor = advisor;
            this.score = score;
            this.targetDistance = targetDistance;
        }
    }

    public readonly struct ArtifactTransferCandidate
    {
        public readonly string artifactName;
        public readonly string targetName;
        public readonly float score;
        public readonly float distance;

        public ArtifactTransferCandidate(string artifactName, string targetName, float score, float distance)
        {
            this.artifactName = artifactName;
            this.targetName = targetName;
            this.score = score;
            this.distance = distance;
        }
    }

    private TargetInfo GetTargetInfo(Hex targetHex)
    {
        if (targetHex == null) return new TargetInfo(null, null, null);
        Leader rawLeader = GetEnemyLeaderOnHex(targetHex);
        if (rawLeader == null) return new TargetInfo(null, null, null);

        // If NPC has joined, prefer the joined owner
        Leader effective = rawLeader;
        if (rawLeader is NonPlayableLeader npc && npc.joined && npc.GetOwner() != null)
        {
            effective = npc.GetOwner();
        }

        string type = effective is NonPlayableLeader ? "NonPlayableLeader" : "Leader";
        return new TargetInfo(effective.characterName, effective.GetAlignment().ToString(), type);
    }

    private readonly struct TargetInfo
    {
        public readonly string name;
        public readonly string alignment;
        public readonly string type;

        public TargetInfo(string name, string alignment, string type)
        {
            this.name = name;
            this.alignment = alignment;
            this.type = type;
        }
    }

    private ResourceSnapshot CaptureSnapshot()
    {
        Leader owner = Character != null ? Character.GetOwner() : null;
        Army army = Character != null ? Character.GetArmy() : null;
        return new ResourceSnapshot
        {
            gold = owner != null ? owner.goldAmount : 0,
            goldPerTurn = 0,
            leather = owner != null ? owner.leatherAmount : 0,
            timber = owner != null ? owner.timberAmount : 0,
            iron = owner != null ? owner.ironAmount : 0,
            mounts = owner != null ? owner.mountsAmount : 0,
            mithril = owner != null ? owner.mithrilAmount : 0,
            leatherPerTurn = owner != null ? owner.GetLeatherPerTurn() : 0,
            timberPerTurn = owner != null ? owner.GetTimberPerTurn() : 0,
            ironPerTurn = owner != null ? owner.GetIronPerTurn() : 0,
            mountsPerTurn = owner != null ? owner.GetMountsPerTurn() : 0,
            mithrilPerTurn = owner != null ? owner.GetMithrilPerTurn() : 0,
            armyOffence = army != null ? army.GetOffence() : 0,
            armyDefence = army != null ? army.GetDefence() : 0,
            commander = Character?.GetCommander() ?? 0,
            agent = Character?.GetAgent() ?? 0,
            emmissary = Character?.GetEmmissary() ?? 0,
            mage = Character?.GetMage() ?? 0,
            health = Character?.health ?? 0,
            nearestEnemyStrength = closestEnemy.Strength,
            nearestNonNeutralStrength = closestNonNeutralEnemy.Strength
        };
    }

    private struct ResourceSnapshot
    {
        public int gold;
        public int goldPerTurn;
        public int leather;
        public int timber;
        public int iron;
        public int mounts;
        public int mithril;
        public int leatherPerTurn;
        public int timberPerTurn;
        public int ironPerTurn;
        public int mountsPerTurn;
        public int mithrilPerTurn;
        public int armyOffence;
        public int armyDefence;
        public int commander;
        public int agent;
        public int emmissary;
        public int mage;
        public int health;
        public float nearestEnemyStrength;
        public float nearestNonNeutralStrength;
    }

    private Dictionary<PlayableLeader, int> CaptureVictoryPointsSnapshot()
    {
        Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
        if (game == null) return new();
        return VictoryPoints.CalculateForAll(game)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RelativeScore);
    }

    private static int GetVictoryPoints(Dictionary<PlayableLeader, int> snapshot, PlayableLeader leader)
    {
        if (snapshot == null || leader == null) return 0;
        return snapshot.TryGetValue(leader, out int value) ? value : 0;
    }

    private static List<string> BuildOpponentVpDeltas(Dictionary<PlayableLeader, int> post, Dictionary<PlayableLeader, int> pre, PlayableLeader self)
    {
        List<string> result = new();
        if (post == null) return result;
        foreach (var kvp in post)
        {
            PlayableLeader leader = kvp.Key;
            if (leader == null || leader == self) continue;
            int before = pre != null && pre.TryGetValue(leader, out int v) ? v : 0;
            int delta = kvp.Value - before;
            result.Add($"{leader.characterName}|{delta}");
        }
        return result;
    }
}
