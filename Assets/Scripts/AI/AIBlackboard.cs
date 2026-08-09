using System.Collections.Generic;

public struct HTNStackFrame
{
    public string MethodTaskId;
    public int SubtaskIndex;
}

public class AIBlackboard
{
    public List<HTNStackFrame> ActiveStack { get; set; } = new();
    public int TurnsOnCurrentTask { get; set; }
    public Dictionary<string, float> Facts { get; } = new();

    // The specific hex behind whichever situational parameter the active primitive task
    // prefers (see AIContext.GetTargetHexForParameter) — set once per turn in
    // AITurnController.AdvanceHtnStrategy. This is what lets a character actually travel to
    // and act on the location that triggered its current strategy, instead of only ever
    // acting on whatever hex it happens to already be standing on. Null when the active task
    // has no specific location (e.g. Economic recovery, or the generic fallback).
    public Hex TargetHex { get; set; }
}

public static class AIBlackboardStore
{
    // Keyed per (leader, character) rather than per leader: HTN strategy is decided by each
    // character's own local situation now (see AITurnController.AdvanceHtnStrategy), so each
    // character needs its own persistent stack/task-continuity state, not one shared per
    // leader. Keying on the pair (not Character alone) means a character that changes
    // allegiance (e.g. via conversion) starts fresh under its new leader rather than carrying
    // over stale commitment state from its old one.
    private static readonly Dictionary<(PlayableLeader, Character), AIBlackboard> blackboards = new();

    public static AIBlackboard GetOrCreate(PlayableLeader leader, Character character)
    {
        if (leader == null || character == null) return new AIBlackboard();
        var key = (leader, character);
        if (!blackboards.TryGetValue(key, out AIBlackboard blackboard))
        {
            blackboard = new AIBlackboard();
            blackboards[key] = blackboard;
        }
        return blackboard;
    }

    // Read-only lookup for debug/inspection tools (e.g. AIBlackboardDebugPanel) — unlike
    // GetOrCreate, never creates an entry, so querying a human-controlled character (one the AI
    // turn controller has never processed) correctly reports "no blackboard yet" instead of
    // silently planting a permanent empty one.
    public static bool TryGet(PlayableLeader leader, Character character, out AIBlackboard blackboard)
    {
        blackboard = null;
        if (leader == null || character == null) return false;
        return blackboards.TryGetValue((leader, character), out blackboard);
    }
}
