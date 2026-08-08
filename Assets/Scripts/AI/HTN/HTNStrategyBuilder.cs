// ---------------------------------------------------------------------------
// Hardcoded fallback strategy, used when no authored Strategies.json applies.
// Matches the default strategy shape shown in the AI Widget's Strategies tab,
// including the 2-step economic-recovery sequence HTN Methods make possible.
// ---------------------------------------------------------------------------

public static class HTNStrategyBuilder
{
    public static HTNCompoundTask BuildDefault()
    {
        HTNRegistry.TryGetPredicate("Economic.Critical", out var economyCritical);
        HTNRegistry.TryGetPredicate("Economic.Weak", out var economyWeak);
        HTNRegistry.TryGetPredicate("Economic.Stable", out var economyStable);
        HTNRegistry.TryGetPredicate("Militaristic.Danger", out var danger);
        HTNRegistry.TryGetPredicate("Militaristic.Viable", out var militaristicViable);
        HTNRegistry.TryGetPredicate("Magic.Viable", out var magicViable);
        HTNRegistry.TryGetPredicate("Diplomatic.Viable", out var diplomaticViable);
        HTNRegistry.TryGetPredicate("Intelligence.Viable", out var intelligenceViable);
        HTNRegistry.TryGetPredicate("Movement.Viable", out var movementViable);
        HTNRegistry.TryGetPredicate("Global.Always", out var always);
        HTNRegistry.TryGetPredicate("Global.Never", out var never);

        // Target-quality predicates, gating the sub-branches within root.diplomacy/
        // root.intelligence/root.magic below — distinguishing "there's a specific good target"
        // from just "this advisor is generically viable".
        HTNRegistry.TryGetPredicate("Diplomatic.EnemyPcOpportunityReady", out var enemyPcOpportunityReady);
        HTNRegistry.TryGetPredicate("Diplomatic.OwnPcLoyaltyRiskReady", out var ownPcLoyaltyRiskReady);
        HTNRegistry.TryGetPredicate("Diplomatic.NpcDiscoveryReady", out var npcDiscoveryReady);
        HTNRegistry.TryGetPredicate("Intelligence.HighValueEnemyCharacterReady", out var highValueEnemyCharacterReady);
        HTNRegistry.TryGetPredicate("Intelligence.EnemyPcVulnerabilityReady", out var enemyPcVulnerabilityReady);
        HTNRegistry.TryGetPredicate("Magic.ArtifactScarcityReady", out var artifactScarcityReady);
        HTNRegistry.TryGetPredicate("Magic.SpellOpportunityReady", out var spellOpportunityReady);
        HTNRegistry.TryGetPredicate("Militaristic.OwnPcFortificationNeedReady", out var fortificationNeedReady);
        HTNRegistry.TryGetPredicate("Diplomatic.NplRecruitmentReady", out var nplRecruitmentReady);
        HTNRegistry.TryGetPredicate("Movement.ReachNpcReady", out var reachNpcReady);
        HTNRegistry.TryGetPredicate("Movement.InterceptEnemyReady", out var interceptEnemyReady);
        HTNRegistry.TryGetPredicate("Movement.ReachEnemyCharacterReady", out var reachEnemyCharacterReady);

        // Highest priority: a nearby enemy that outguns this leader's army. Biases toward
        // Militaristic — specifically the defensive cards in that pool (e.g. FortifyPC), since
        // Militaristic's own viability formula already penalizes reckless Attack heavily while
        // outmatched. Intelligence/Diplomatic need no HTN bias here at all: their own viability
        // already adds an outmatched bonus unconditionally (see AIContext.GetAdvisorViability),
        // so both responses fire together from this one Method.
        HTNPrimitiveTask dangerLeaf = new()
        {
            TaskId = "root.danger.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString()
        };
        HTNMethod dangerMethod = new() { TaskId = "root.danger", Precondition = danger };
        dangerMethod.Subtasks.Add(dangerLeaf);

        HTNPrimitiveTask buildInfrastructure = new()
        {
            TaskId = "root.recover.build",
            Precondition = always,
            CompletionCondition = economyWeak,
            AdvisorName = AdvisorType.Economic.ToString()
        };
        HTNPrimitiveTask establishTrade = new()
        {
            TaskId = "root.recover.trade",
            Precondition = always,
            CompletionCondition = economyStable,
            AdvisorName = AdvisorType.Economic.ToString()
        };
        // "Economic.Critical OR Economic.Weak" — composed directly here via HTNRegistry.Or,
        // not through a named alias predicate.
        HTNMethod recoverMethod = new() { TaskId = "root.recover", Precondition = HTNRegistry.Or(economyCritical, economyWeak) };
        recoverMethod.Subtasks.Add(buildInfrastructure);
        recoverMethod.Subtasks.Add(establishTrade);

        // root.offense.pick: a specific under-fortified, threatened own PC takes priority over
        // generic attack — same "specific opportunity before generic fallback" shape as the
        // diplomacy/intelligence/magic picks below.
        HTNPrimitiveTask offenseFortifyLeaf = new()
        {
            TaskId = "root.offense.pick.fortify.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString()
        };
        HTNMethod offenseFortifyMethod = new() { TaskId = "root.offense.pick.fortify", Precondition = fortificationNeedReady };
        offenseFortifyMethod.Subtasks.Add(offenseFortifyLeaf);

        HTNPrimitiveTask offenseAttackLeaf = new()
        {
            TaskId = "root.offense.pick.attack.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString()
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
            AdvisorName = AdvisorType.Diplomatic.ToString()
        };
        HTNMethod diplomacyRecruitMethod = new() { TaskId = "root.diplomacy.pick.recruit", Precondition = nplRecruitmentReady };
        diplomacyRecruitMethod.Subtasks.Add(diplomacyRecruitLeaf);

        HTNPrimitiveTask diplomacyFlipLeaf = new()
        {
            TaskId = "root.diplomacy.pick.flip.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString()
        };
        HTNMethod diplomacyFlipMethod = new() { TaskId = "root.diplomacy.pick.flip", Precondition = enemyPcOpportunityReady };
        diplomacyFlipMethod.Subtasks.Add(diplomacyFlipLeaf);

        HTNPrimitiveTask diplomacyShoreLeaf = new()
        {
            TaskId = "root.diplomacy.pick.shore.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString()
        };
        HTNMethod diplomacyShoreMethod = new() { TaskId = "root.diplomacy.pick.shore", Precondition = ownPcLoyaltyRiskReady };
        diplomacyShoreMethod.Subtasks.Add(diplomacyShoreLeaf);

        HTNPrimitiveTask diplomacyWooLeaf = new()
        {
            TaskId = "root.diplomacy.pick.woo.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Diplomatic.ToString()
        };
        HTNMethod diplomacyWooMethod = new() { TaskId = "root.diplomacy.pick.woo", Precondition = npcDiscoveryReady };
        diplomacyWooMethod.Subtasks.Add(diplomacyWooLeaf);

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
        diplomacyPick.Methods.Add(diplomacyWooMethod);
        diplomacyPick.Methods.Add(diplomacyFallbackMethod);

        HTNMethod diplomacyMethod = new() { TaskId = "root.diplomacy", Precondition = diplomaticViable };
        diplomacyMethod.Subtasks.Add(diplomacyPick);

        HTNPrimitiveTask intelligenceHighValueLeaf = new()
        {
            TaskId = "root.intelligence.pick.highvalue.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Intelligence.ToString()
        };
        HTNMethod intelligenceHighValueMethod = new() { TaskId = "root.intelligence.pick.highvalue", Precondition = highValueEnemyCharacterReady };
        intelligenceHighValueMethod.Subtasks.Add(intelligenceHighValueLeaf);

        HTNPrimitiveTask intelligenceSabotageLeaf = new()
        {
            TaskId = "root.intelligence.pick.sabotage.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Intelligence.ToString()
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
            AdvisorName = AdvisorType.Magic.ToString()
        };
        HTNMethod magicRetrieveMethod = new() { TaskId = "root.magic.pick.retrieve", Precondition = artifactScarcityReady };
        magicRetrieveMethod.Subtasks.Add(magicRetrieveLeaf);

        HTNPrimitiveTask magicCastLeaf = new()
        {
            TaskId = "root.magic.pick.cast.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Magic.ToString()
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

        // root.movement.pick: these three "Ready" predicates already existed (each with its own
        // Card Board group) but nothing ever branched on them — root.movement was a one-leaf
        // stub. Same "specific opportunity before generic fallback" shape as the other picks.
        HTNPrimitiveTask movementReachNpcLeaf = new()
        {
            TaskId = "root.movement.pick.reachnpc.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Movement.ToString()
        };
        HTNMethod movementReachNpcMethod = new() { TaskId = "root.movement.pick.reachnpc", Precondition = reachNpcReady };
        movementReachNpcMethod.Subtasks.Add(movementReachNpcLeaf);

        HTNPrimitiveTask movementInterceptLeaf = new()
        {
            TaskId = "root.movement.pick.intercept.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Movement.ToString()
        };
        HTNMethod movementInterceptMethod = new() { TaskId = "root.movement.pick.intercept", Precondition = interceptEnemyReady };
        movementInterceptMethod.Subtasks.Add(movementInterceptLeaf);

        HTNPrimitiveTask movementReachCharacterLeaf = new()
        {
            TaskId = "root.movement.pick.reachcharacter.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Movement.ToString()
        };
        HTNMethod movementReachCharacterMethod = new() { TaskId = "root.movement.pick.reachcharacter", Precondition = reachEnemyCharacterReady };
        movementReachCharacterMethod.Subtasks.Add(movementReachCharacterLeaf);

        HTNPrimitiveTask movementFallbackLeaf = new()
        {
            TaskId = "root.movement.pick.fallback.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Movement.ToString()
        };
        HTNMethod movementFallbackMethod = new() { TaskId = "root.movement.pick.fallback", Precondition = always };
        movementFallbackMethod.Subtasks.Add(movementFallbackLeaf);

        HTNCompoundTask movementPick = new() { TaskId = "root.movement.pick" };
        movementPick.Methods.Add(movementReachNpcMethod);
        movementPick.Methods.Add(movementInterceptMethod);
        movementPick.Methods.Add(movementReachCharacterMethod);
        movementPick.Methods.Add(movementFallbackMethod);

        HTNMethod movementMethod = new() { TaskId = "root.movement", Precondition = movementViable };
        movementMethod.Subtasks.Add(movementPick);

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
        root.Methods.Add(movementMethod);
        root.Methods.Add(fallbackMethod);
        return root;
    }
}
