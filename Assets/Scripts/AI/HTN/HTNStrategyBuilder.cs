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
        HTNRegistry.TryGetPredicate("Movement.Viable", out var movementViable);
        HTNRegistry.TryGetPredicate("Global.Always", out var always);
        HTNRegistry.TryGetPredicate("Global.Never", out var never);

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
        HTNMethod recoverMethod = new() { TaskId = "root.recover", Precondition = economyCritical };
        recoverMethod.Subtasks.Add(buildInfrastructure);
        recoverMethod.Subtasks.Add(establishTrade);

        HTNPrimitiveTask offenseLeaf = new()
        {
            TaskId = "root.offense.pick.mil.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Militaristic.ToString()
        };
        HTNMethod offensePickMethod = new() { TaskId = "root.offense.pick.mil", Precondition = always };
        offensePickMethod.Subtasks.Add(offenseLeaf);
        HTNCompoundTask offensePick = new() { TaskId = "root.offense.pick" };
        offensePick.Methods.Add(offensePickMethod);

        HTNMethod offenseMethod = new() { TaskId = "root.offense", Precondition = militaristicViable };
        offenseMethod.Subtasks.Add(offensePick);

        HTNPrimitiveTask movementLeaf = new()
        {
            TaskId = "root.movement.leaf",
            Precondition = always,
            CompletionCondition = never,
            AdvisorName = AdvisorType.Movement.ToString()
        };
        HTNMethod movementMethod = new() { TaskId = "root.movement", Precondition = movementViable };
        movementMethod.Subtasks.Add(movementLeaf);

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
        root.Methods.Add(movementMethod);
        root.Methods.Add(fallbackMethod);
        return root;
    }
}
