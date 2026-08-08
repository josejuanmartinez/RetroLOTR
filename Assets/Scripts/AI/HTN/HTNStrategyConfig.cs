using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Serialized form of authored HTN strategies. Nodes are stored as a flat,
// depth-indented outline in document order: a node's parent is the nearest
// preceding node with depth - 1. Depth-0 nodes are the CompoundTask root(s).
// A Method's children (depth+1) are an ORDERED SEQUENCE of subtasks, executed
// in document order — see HTNPlanner for the runtime semantics.
//
// A precondition/completion condition is a LIST of terms, OR'd together —
// this is what lets "Economic.Critical OR Economic.Weak" be authored directly
// on a Method row, instead of requiring a named alias predicate for every
// combination someone might want. An empty list means Always (precondition)
// or Never (completion condition).
//
// Edited via Tools > RetroLOTR > AI Widget > Strategies.
// ---------------------------------------------------------------------------

[Serializable]
public class HTNConditionTerm
{
    public string name = string.Empty;
    public bool invert;
}

[Serializable]
public class HTNNodeData
{
    public int depth;
    public string type = HTNNodeType.PrimitiveTask.ToString();
    public List<HTNConditionTerm> precondition = new();
    public List<HTNConditionTerm> completionCondition = new();
    public string advisor = string.Empty;
    public string taskId = string.Empty;
}

[Serializable]
public class HTNStrategyData
{
    public string strategyId = string.Empty;
    public List<HTNNodeData> nodes = new();
}

[Serializable]
public class HTNStrategyAssignment
{
    public string alignment = string.Empty;
    public string strategyId = string.Empty;
}

[Serializable]
public class HTNStrategyLibraryData
{
    public List<HTNStrategyData> strategies = new();
    public List<HTNStrategyAssignment> assignments = new();
}
