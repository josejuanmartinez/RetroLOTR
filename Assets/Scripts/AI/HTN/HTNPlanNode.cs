using System;
using System.Collections.Generic;

public interface IHTNNode
{
    HTNNodeType NodeType { get; }
    string TaskId { get; }
}

public class HTNCompoundTask : IHTNNode
{
    public HTNNodeType NodeType => HTNNodeType.CompoundTask;
    public string TaskId { get; set; } = string.Empty;
    public List<HTNMethod> Methods { get; } = new();
}

// A Method is a precondition-gated Sequence: its Subtasks execute in order.
public class HTNMethod
{
    public string TaskId { get; set; } = string.Empty;
    public Func<UtilityAIContext, CharacterBlackboard, bool> Precondition { get; set; } = (_, _) => true;
    public List<IHTNNode> Subtasks { get; } = new();
}

public class HTNPrimitiveTask : IHTNNode
{
    public HTNNodeType NodeType => HTNNodeType.PrimitiveTask;
    public string TaskId { get; set; } = string.Empty;

    // Entry/failure gate: must hold for this task to be chosen or to keep running.
    public Func<UtilityAIContext, CharacterBlackboard, bool> Precondition { get; set; } = (_, _) => true;

    // "Effect": once true, this subtask is done and execution advances to the next
    // subtask in its Method's sequence. Never true (the "Never" predicate) means this
    // task only ever leaves via interrupt or its own precondition breaking.
    public Func<UtilityAIContext, CharacterBlackboard, bool> CompletionCondition { get; set; } = (_, _) => false;

    // Which specific UtilityAIParameters names this task's situation is actually about (e.g.
    // "root.offense.pick.fortify" -> Militaristic.OwnPcFortificationNeed). Cards whose own Card
    // Board profile already uses one of these get UtilityAI.Keys.HTNBiasBonus in
    // UtilityAIContext.ScoreAction — this list is the *only* thing tying a card to "the active
    // strategy is about this", there is no separate advisor tag. Also read by
    // UtilityAIContext.HasEligibleCard for HTNPlanner's role-eligibility backtracking, and by
    // GetTargetHexForParameter to resolve a travel destination. Empty = no bias, no target.
    public List<string> PreferredParameters { get; set; } = new();
}
