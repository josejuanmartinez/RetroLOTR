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
    public Func<AIContext, AIBlackboard, bool> Precondition { get; set; } = (_, _) => true;
    public List<IHTNNode> Subtasks { get; } = new();
}

public class HTNPrimitiveTask : IHTNNode
{
    public HTNNodeType NodeType => HTNNodeType.PrimitiveTask;
    public string TaskId { get; set; } = string.Empty;

    // Entry/failure gate: must hold for this task to be chosen or to keep running.
    public Func<AIContext, AIBlackboard, bool> Precondition { get; set; } = (_, _) => true;

    // "Effect": once true, this subtask is done and execution advances to the next
    // subtask in its Method's sequence. Never true (the "Never" predicate) means this
    // task only ever leaves via interrupt or its own precondition breaking.
    public Func<AIContext, AIBlackboard, bool> CompletionCondition { get; set; } = (_, _) => false;

    // Which AIAdvisorConfig scoring profile this task biases the Utility scorer toward
    // for as long as it stays active. Empty/null = neutral, no bias.
    public string AdvisorName { get; set; } = string.Empty;

    // Which specific AIUtilityParameters names this task's situation is actually about (e.g.
    // "root.offense.pick.fortify" -> Militaristic.OwnPcFortificationNeed). Cards whose own Card
    // Board profile already uses one of these get an extra nudge on top of the flat advisor
    // bias in AIContext.ScoreAction — "this card's own authored profile already targets this
    // exact situation" is a stronger signal than "this card merely belongs to the favored
    // advisor". Empty = today's flat-only bias, unchanged.
    public List<string> PreferredParameters { get; set; } = new();
}
