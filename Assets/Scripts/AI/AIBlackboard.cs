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
}

public static class AIBlackboardStore
{
    private static readonly Dictionary<PlayableLeader, AIBlackboard> blackboards = new();

    public static AIBlackboard GetOrCreate(PlayableLeader leader)
    {
        if (leader == null) return new AIBlackboard();
        if (!blackboards.TryGetValue(leader, out AIBlackboard blackboard))
        {
            blackboard = new AIBlackboard();
            blackboards[leader] = blackboard;
        }
        return blackboard;
    }
}
