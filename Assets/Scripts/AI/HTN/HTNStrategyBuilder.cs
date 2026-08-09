// ---------------------------------------------------------------------------
// Hardcoded fallback strategy, used when no authored Strategies.json applies.
// Matches the default strategy shape shown in the AI Widget's Strategies tab.
// ---------------------------------------------------------------------------

public static class HTNStrategyBuilder
{
    public static HTNCompoundTask BuildDefault()
    {
        HTNRegistry.TryGetPredicate("Economic.Critical", out var economyCritical);
        HTNRegistry.TryGetPredicate("Economic.Weak", out var economyWeak);
        HTNRegistry.TryGetPredicate("Militaristic.Danger", out var danger);
        HTNRegistry.TryGetPredicate("Militaristic.Viable", out var militaristicViable);
        HTNRegistry.TryGetPredicate("Magic.Viable", out var magicViable);
        HTNRegistry.TryGetPredicate("Diplomatic.Viable", out var diplomaticViable);
        HTNRegistry.TryGetPredicate("Intelligence.Viable", out var intelligenceViable);
        HTNRegistry.TryGetPredicate("Logistics.Viable", out var logisticsViable);
        HTNRegistry.TryGetPredicate("Disruption.Viable", out var disruptionViable);
        HTNRegistry.TryGetPredicate("Global.Always", out var always);
        HTNRegistry.TryGetPredicate("Global.Never", out var never);

        // Target-quality predicates, gating the sub-branches within root.diplomacy/
        // root.intelligence/root.magic below — distinguishing "there's a specific good target"
        // from just "this advisor is generically viable".
        HTNRegistry.TryGetPredicate("Diplomatic.EnemyPcOpportunityReady", out var enemyPcOpportunityReady);
        HTNRegistry.TryGetPredicate("Diplomatic.OwnPcLoyaltyRiskReady", out var ownPcLoyaltyRiskReady);
        HTNRegistry.TryGetPredicate("Intelligence.HighValueEnemyCharacterReady", out var highValueEnemyCharacterReady);
        HTNRegistry.TryGetPredicate("Intelligence.EnemyPcVulnerabilityReady", out var enemyPcVulnerabilityReady);
        HTNRegistry.TryGetPredicate("Magic.ArtifactScarcityReady", out var artifactScarcityReady);
        HTNRegistry.TryGetPredicate("Magic.SpellOpportunityReady", out var spellOpportunityReady);
        HTNRegistry.TryGetPredicate("Militaristic.OwnPcFortificationNeedReady", out var fortificationNeedReady);
        HTNRegistry.TryGetPredicate("Diplomatic.NplRecruitmentReady", out var nplRecruitmentReady);
        HTNRegistry.TryGetPredicate("Logistics.ReachNpcReady", out var reachNpcReady);
        HTNRegistry.TryGetPredicate("Logistics.InterceptEnemyReady", out var interceptEnemyReady);
        HTNRegistry.TryGetPredicate("Logistics.ReachEnemyCharacterReady", out var reachEnemyCharacterReady);
        HTNRegistry.TryGetPredicate("Logistics.HealingNeedReady", out var healingNeedReady);
        HTNRegistry.TryGetPredicate("Disruption.EnemyPressureReady", out var disruptionPressureReady);
        HTNRegistry.TryGetPredicate("Economic.MithrilReady", out var mithrilReady);
        HTNRegistry.TryGetPredicate("Economic.SteelReady", out var steelReady);
        HTNRegistry.TryGetPredicate("Economic.IronReady", out var ironReady);
        HTNRegistry.TryGetPredicate("Economic.MountsReady", out var mountsReady);
        HTNRegistry.TryGetPredicate("Economic.TimberReady", out var timberReady);
        HTNRegistry.TryGetPredicate("Economic.LeatherReady", out var leatherReady);

        // Highest priority: a nearby enemy that outguns this leader's army. Biases toward
        // Militaristic — specifically the defensive cards in that pool. Intelligence/Diplomatic
        // need no HTN bias here at all: their own viability already adds an outmatched bonus
        // unconditionally (see AIContext.GetAdvisorViability), so both responses fire together
        // from this one Method regardless of which danger.pick branch is active.
        //
        // root.danger.pick: two real missions instead of one flat leaf — harden the PC's own
        // defense (FortifyPC) when that's the specific gap, otherwise muster/train/hold a
        // garrison there (ConscriptArmy/TrainArmy/Block) — same "specific opportunity before
        // generic fallback" shape as root.offense.pick.
        HTNPrimitiveTask dangerFortifyLeaf = new()
        {
            TaskId = "root.danger.pick.fortify.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MilitaristicOwnPcFortificationNeed }
        };
        HTNMethod dangerFortifyMethod = new() { TaskId = "root.danger.pick.fortify", Precondition = fortificationNeedReady };
        dangerFortifyMethod.Subtasks.Add(dangerFortifyLeaf);

        HTNPrimitiveTask dangerConscriptLeaf = new()
        {
            TaskId = "root.danger.pick.conscript.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MilitaristicOwnPcDefenderNeed }
        };
        HTNMethod dangerConscriptMethod = new() { TaskId = "root.danger.pick.conscript", Precondition = always };
        dangerConscriptMethod.Subtasks.Add(dangerConscriptLeaf);

        HTNCompoundTask dangerPick = new() { TaskId = "root.danger.pick" };
        dangerPick.Methods.Add(dangerFortifyMethod);
        dangerPick.Methods.Add(dangerConscriptMethod);

        HTNMethod dangerMethod = new() { TaskId = "root.danger", Precondition = danger };
        dangerMethod.Subtasks.Add(dangerPick);

        // root.recover.pick: one mission per tradeable material — insufficient stock biases
        // toward that material's Buy{X} card, surplus stock biases toward its Sell{X} card (both
        // via PreferredParameters on the same leaf; the underlying utility math, not branch
        // priority, decides which of the two actually wins). Gold has no Buy/SellGold card of
        // its own, so its Insufficient/Surplus instead ride along on every Sell{X}/Buy{X}
        // card's own authored profile (see AdvisorConfig.json) rather than getting a branch
        // here. Ordered by StoresManager trade value descending: Mithril > Steel > Iron = Mounts
        // > Timber > Leather, same "specific opportunity before generic fallback" shape as the
        // other picks.
        HTNPrimitiveTask recoverMithrilLeaf = new()
        {
            TaskId = "root.recover.pick.mithril.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicMithrilInsufficient, AIUtilityParameters.EconomicMithrilSurplus }
        };
        HTNMethod recoverMithrilMethod = new() { TaskId = "root.recover.pick.mithril", Precondition = mithrilReady };
        recoverMithrilMethod.Subtasks.Add(recoverMithrilLeaf);

        HTNPrimitiveTask recoverSteelLeaf = new()
        {
            TaskId = "root.recover.pick.steel.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicSteelInsufficient, AIUtilityParameters.EconomicSteelSurplus }
        };
        HTNMethod recoverSteelMethod = new() { TaskId = "root.recover.pick.steel", Precondition = steelReady };
        recoverSteelMethod.Subtasks.Add(recoverSteelLeaf);

        HTNPrimitiveTask recoverIronLeaf = new()
        {
            TaskId = "root.recover.pick.iron.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicIronInsufficient, AIUtilityParameters.EconomicIronSurplus }
        };
        HTNMethod recoverIronMethod = new() { TaskId = "root.recover.pick.iron", Precondition = ironReady };
        recoverIronMethod.Subtasks.Add(recoverIronLeaf);

        HTNPrimitiveTask recoverMountsLeaf = new()
        {
            TaskId = "root.recover.pick.mounts.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicMountsInsufficient, AIUtilityParameters.EconomicMountsSurplus }
        };
        HTNMethod recoverMountsMethod = new() { TaskId = "root.recover.pick.mounts", Precondition = mountsReady };
        recoverMountsMethod.Subtasks.Add(recoverMountsLeaf);

        HTNPrimitiveTask recoverTimberLeaf = new()
        {
            TaskId = "root.recover.pick.timber.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicTimberInsufficient, AIUtilityParameters.EconomicTimberSurplus }
        };
        HTNMethod recoverTimberMethod = new() { TaskId = "root.recover.pick.timber", Precondition = timberReady };
        recoverTimberMethod.Subtasks.Add(recoverTimberLeaf);

        HTNPrimitiveTask recoverLeatherLeaf = new()
        {
            TaskId = "root.recover.pick.leather.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicLeatherInsufficient, AIUtilityParameters.EconomicLeatherSurplus }
        };
        HTNMethod recoverLeatherMethod = new() { TaskId = "root.recover.pick.leather", Precondition = leatherReady };
        recoverLeatherMethod.Subtasks.Add(recoverLeatherLeaf);

        HTNPrimitiveTask recoverFallbackLeaf = new()
        {
            TaskId = "root.recover.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Economic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.EconomicLiquidWealth }
        };
        HTNMethod recoverFallbackMethod = new() { TaskId = "root.recover.pick.fallback", Precondition = always };
        recoverFallbackMethod.Subtasks.Add(recoverFallbackLeaf);

        HTNCompoundTask recoverPick = new() { TaskId = "root.recover.pick" };
        recoverPick.Methods.Add(recoverMithrilMethod);
        recoverPick.Methods.Add(recoverSteelMethod);
        recoverPick.Methods.Add(recoverIronMethod);
        recoverPick.Methods.Add(recoverMountsMethod);
        recoverPick.Methods.Add(recoverTimberMethod);
        recoverPick.Methods.Add(recoverLeatherMethod);
        recoverPick.Methods.Add(recoverFallbackMethod);

        // "Economic.Critical OR Economic.Weak" — composed directly here via HTNRegistry.Or,
        // not through a named alias predicate.
        HTNMethod recoverMethod = new() { TaskId = "root.recover", Precondition = HTNRegistry.Or(economyCritical, economyWeak) };
        recoverMethod.Subtasks.Add(recoverPick);

        // root.offense.pick: a specific under-fortified, threatened own PC takes priority over
        // generic attack — same "specific opportunity before generic fallback" shape as the
        // diplomacy/intelligence/magic picks below.
        HTNPrimitiveTask offenseFortifyLeaf = new()
        {
            TaskId = "root.offense.pick.fortify.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MilitaristicOwnPcFortificationNeed }
        };
        HTNMethod offenseFortifyMethod = new() { TaskId = "root.offense.pick.fortify", Precondition = fortificationNeedReady };
        offenseFortifyMethod.Subtasks.Add(offenseFortifyLeaf);

        HTNPrimitiveTask offenseAttackLeaf = new()
        {
            TaskId = "root.offense.pick.attack.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MilitaristicMilitaryEdge, AIUtilityParameters.MilitaristicEnemyPressure }
        };
        HTNMethod offenseAttackMethod = new() { TaskId = "root.offense.pick.attack", Precondition = always };
        offenseAttackMethod.Subtasks.Add(offenseAttackLeaf);

        HTNCompoundTask offensePick = new() { TaskId = "root.offense.pick" };
        offensePick.Methods.Add(offenseFortifyMethod);
        offensePick.Methods.Add(offenseAttackMethod);

        HTNMethod offenseMethod = new() { TaskId = "root.offense", Precondition = militaristicViable };
        offenseMethod.Subtasks.Add(offensePick);

        // Diplomatic/Intelligence/Magic each used to be a single-leaf stub: once viable, the
        // HTN picked that advisor but had no further opinion on which situation drove it there.
        // Each now decomposes into its own "pick" CompoundTask, first-match-wins over the
        // target-quality predicates above, so the active HTN task (and AIActionLogger's
        // ActiveHtnTaskId) actually distinguishes the situation, not just the advisor. The
        // leaves all still tag the same AdvisorName — that's what drives ScoreAction's flat
        // HTNBiasBonus — only the precondition/TaskId differ per branch.
        HTNPrimitiveTask diplomacyRecruitLeaf = new()
        {
            TaskId = "root.diplomacy.pick.recruit.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.DiplomaticNplRecruitment }
        };
        HTNMethod diplomacyRecruitMethod = new() { TaskId = "root.diplomacy.pick.recruit", Precondition = nplRecruitmentReady };
        diplomacyRecruitMethod.Subtasks.Add(diplomacyRecruitLeaf);

        HTNPrimitiveTask diplomacyFlipLeaf = new()
        {
            TaskId = "root.diplomacy.pick.flip.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.DiplomaticEnemyPcOpportunity }
        };
        HTNMethod diplomacyFlipMethod = new() { TaskId = "root.diplomacy.pick.flip", Precondition = enemyPcOpportunityReady };
        diplomacyFlipMethod.Subtasks.Add(diplomacyFlipLeaf);

        HTNPrimitiveTask diplomacyShoreLeaf = new()
        {
            TaskId = "root.diplomacy.pick.shore.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.DiplomaticOwnPcLoyaltyRisk }
        };
        HTNMethod diplomacyShoreMethod = new() { TaskId = "root.diplomacy.pick.shore", Precondition = ownPcLoyaltyRiskReady };
        diplomacyShoreMethod.Subtasks.Add(diplomacyShoreLeaf);

        HTNPrimitiveTask diplomacyFallbackLeaf = new()
        {
            TaskId = "root.diplomacy.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString()
        };
        HTNMethod diplomacyFallbackMethod = new() { TaskId = "root.diplomacy.pick.fallback", Precondition = always };
        diplomacyFallbackMethod.Subtasks.Add(diplomacyFallbackLeaf);

        HTNCompoundTask diplomacyPick = new() { TaskId = "root.diplomacy.pick" };
        diplomacyPick.Methods.Add(diplomacyRecruitMethod);
        diplomacyPick.Methods.Add(diplomacyFlipMethod);
        diplomacyPick.Methods.Add(diplomacyShoreMethod);
        diplomacyPick.Methods.Add(diplomacyFallbackMethod);

        HTNMethod diplomacyMethod = new() { TaskId = "root.diplomacy", Precondition = diplomaticViable };
        diplomacyMethod.Subtasks.Add(diplomacyPick);

        HTNPrimitiveTask intelligenceHighValueLeaf = new()
        {
            TaskId = "root.intelligence.pick.highvalue.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Intelligence.ToString(),
            PreferredParameters = new() { AIUtilityParameters.IntelligenceHighValueEnemyCharacter }
        };
        HTNMethod intelligenceHighValueMethod = new() { TaskId = "root.intelligence.pick.highvalue", Precondition = highValueEnemyCharacterReady };
        intelligenceHighValueMethod.Subtasks.Add(intelligenceHighValueLeaf);

        HTNPrimitiveTask intelligenceSabotageLeaf = new()
        {
            TaskId = "root.intelligence.pick.sabotage.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Intelligence.ToString(),
            PreferredParameters = new() { AIUtilityParameters.IntelligenceEnemyPcVulnerability }
        };
        HTNMethod intelligenceSabotageMethod = new() { TaskId = "root.intelligence.pick.sabotage", Precondition = enemyPcVulnerabilityReady };
        intelligenceSabotageMethod.Subtasks.Add(intelligenceSabotageLeaf);

        HTNPrimitiveTask intelligenceFallbackLeaf = new()
        {
            TaskId = "root.intelligence.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Intelligence.ToString()
        };
        HTNMethod intelligenceFallbackMethod = new() { TaskId = "root.intelligence.pick.fallback", Precondition = always };
        intelligenceFallbackMethod.Subtasks.Add(intelligenceFallbackLeaf);

        HTNCompoundTask intelligencePick = new() { TaskId = "root.intelligence.pick" };
        intelligencePick.Methods.Add(intelligenceHighValueMethod);
        intelligencePick.Methods.Add(intelligenceSabotageMethod);
        intelligencePick.Methods.Add(intelligenceFallbackMethod);

        HTNMethod intelligenceMethod = new() { TaskId = "root.intelligence", Precondition = intelligenceViable };
        intelligenceMethod.Subtasks.Add(intelligencePick);

        HTNPrimitiveTask magicRetrieveLeaf = new()
        {
            TaskId = "root.magic.pick.retrieve.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Magic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MagicArtifactScarcity, AIUtilityParameters.MagicHiddenArtifacts }
        };
        HTNMethod magicRetrieveMethod = new() { TaskId = "root.magic.pick.retrieve", Precondition = artifactScarcityReady };
        magicRetrieveMethod.Subtasks.Add(magicRetrieveLeaf);

        HTNPrimitiveTask magicCastLeaf = new()
        {
            TaskId = "root.magic.pick.cast.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Magic.ToString(),
            PreferredParameters = new() { AIUtilityParameters.MagicSpellOpportunity, AIUtilityParameters.MagicMageStrength }
        };
        HTNMethod magicCastMethod = new() { TaskId = "root.magic.pick.cast", Precondition = spellOpportunityReady };
        magicCastMethod.Subtasks.Add(magicCastLeaf);

        HTNPrimitiveTask magicFallbackLeaf = new()
        {
            TaskId = "root.magic.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Magic.ToString()
        };
        HTNMethod magicFallbackMethod = new() { TaskId = "root.magic.pick.fallback", Precondition = always };
        magicFallbackMethod.Subtasks.Add(magicFallbackLeaf);

        HTNCompoundTask magicPick = new() { TaskId = "root.magic.pick" };
        magicPick.Methods.Add(magicRetrieveMethod);
        magicPick.Methods.Add(magicCastMethod);
        magicPick.Methods.Add(magicFallbackMethod);

        HTNMethod magicMethod = new() { TaskId = "root.magic", Precondition = magicViable };
        magicMethod.Subtasks.Add(magicPick);

        // root.logistics.pick: these three "Ready" predicates already existed (each with its own
        // Card Board group) but nothing ever branched on them — root.logistics (nee root.movement)
        // was a one-leaf stub. Healing is the new branch added when Movement split into
        // Disruption (deny the enemy) and Logistics (reposition/heal our own side). Same
        // "specific opportunity before generic fallback" shape as the other picks.
        HTNPrimitiveTask logisticsReachNpcLeaf = new()
        {
            TaskId = "root.logistics.pick.reachnpc.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Logistics.ToString(),
            PreferredParameters = new() { AIUtilityParameters.LogisticsReachNpc }
        };
        HTNMethod logisticsReachNpcMethod = new() { TaskId = "root.logistics.pick.reachnpc", Precondition = reachNpcReady };
        logisticsReachNpcMethod.Subtasks.Add(logisticsReachNpcLeaf);

        HTNPrimitiveTask logisticsInterceptLeaf = new()
        {
            TaskId = "root.logistics.pick.intercept.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Logistics.ToString(),
            PreferredParameters = new() { AIUtilityParameters.LogisticsInterceptEnemy }
        };
        HTNMethod logisticsInterceptMethod = new() { TaskId = "root.logistics.pick.intercept", Precondition = interceptEnemyReady };
        logisticsInterceptMethod.Subtasks.Add(logisticsInterceptLeaf);

        HTNPrimitiveTask logisticsReachCharacterLeaf = new()
        {
            TaskId = "root.logistics.pick.reachcharacter.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Logistics.ToString(),
            PreferredParameters = new() { AIUtilityParameters.LogisticsReachEnemyCharacter }
        };
        HTNMethod logisticsReachCharacterMethod = new() { TaskId = "root.logistics.pick.reachcharacter", Precondition = reachEnemyCharacterReady };
        logisticsReachCharacterMethod.Subtasks.Add(logisticsReachCharacterLeaf);

        HTNPrimitiveTask logisticsHealLeaf = new()
        {
            TaskId = "root.logistics.pick.heal.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Logistics.ToString(),
            PreferredParameters = new() { AIUtilityParameters.LogisticsHealingNeed }
        };
        HTNMethod logisticsHealMethod = new() { TaskId = "root.logistics.pick.heal", Precondition = healingNeedReady };
        logisticsHealMethod.Subtasks.Add(logisticsHealLeaf);

        HTNPrimitiveTask logisticsFallbackLeaf = new()
        {
            TaskId = "root.logistics.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Logistics.ToString()
        };
        HTNMethod logisticsFallbackMethod = new() { TaskId = "root.logistics.pick.fallback", Precondition = always };
        logisticsFallbackMethod.Subtasks.Add(logisticsFallbackLeaf);

        HTNCompoundTask logisticsPick = new() { TaskId = "root.logistics.pick" };
        logisticsPick.Methods.Add(logisticsReachNpcMethod);
        logisticsPick.Methods.Add(logisticsInterceptMethod);
        logisticsPick.Methods.Add(logisticsReachCharacterMethod);
        logisticsPick.Methods.Add(logisticsHealMethod);
        logisticsPick.Methods.Add(logisticsFallbackMethod);

        HTNMethod logisticsMethod = new() { TaskId = "root.logistics", Precondition = logisticsViable };
        logisticsMethod.Subtasks.Add(logisticsPick);

        // root.disruption: the other half of the old Movement split — deny/debuff the enemy
        // (halt, block, negative status). Only one real signal exists for it today
        // (Disruption.EnemyPressure), so — like root.danger — it stays a single-leaf Method
        // rather than an artificial multi-branch pick.
        HTNPrimitiveTask disruptionLeaf = new()
        {
            TaskId = "root.disruption.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Disruption.ToString(),
            PreferredParameters = new() { AIUtilityParameters.DisruptionEnemyPressure }
        };
        HTNMethod disruptionMethod = new() { TaskId = "root.disruption", Precondition = disruptionViable };
        disruptionMethod.Subtasks.Add(disruptionLeaf);

        HTNPrimitiveTask fallbackLeaf = new()
        {
            TaskId = "root.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = string.Empty
        };
        HTNMethod fallbackMethod = new() { TaskId = "root.fallback", Precondition = always };
        fallbackMethod.Subtasks.Add(fallbackLeaf);

        HTNCompoundTask root = new() { TaskId = "root" };
        root.Methods.Add(dangerMethod);
        root.Methods.Add(recoverMethod);
        root.Methods.Add(offenseMethod);
        root.Methods.Add(magicMethod);
        root.Methods.Add(diplomacyMethod);
        root.Methods.Add(intelligenceMethod);
        root.Methods.Add(logisticsMethod);
        root.Methods.Add(disruptionMethod);
        root.Methods.Add(fallbackMethod);
        return root;
    }
}
