using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// Loads authored Strategies from Resources and builds HTNCompoundTask graphs.
// Falls back to HTNStrategyBuilder.BuildDefault() when no valid authored
// strategy applies.
// ---------------------------------------------------------------------------

public static class AIStrategyLibrary
{
    public const string ResourcePath = "AI/Strategies";
    public const string DefaultStrategyId = "default";

    private static HTNStrategyLibraryData loadedData;
    private static Dictionary<string, HTNCompoundTask> builtStrategies;
    private static bool loaded;

    public static void Reload()
    {
        loadedData = null;
        builtStrategies = null;
        loaded = false;
    }

    public static HTNCompoundTask GetStrategyFor(PlayableLeader leader)
    {
        EnsureLoaded();

        string strategyId = ResolveStrategyId(leader);
        if (!string.IsNullOrWhiteSpace(strategyId)
            && builtStrategies != null
            && builtStrategies.TryGetValue(strategyId, out HTNCompoundTask strategy)
            && strategy != null)
        {
            return strategy;
        }

        if (builtStrategies != null
            && builtStrategies.TryGetValue(DefaultStrategyId, out HTNCompoundTask defaultStrategy)
            && defaultStrategy != null)
        {
            return defaultStrategy;
        }

        return HTNStrategyBuilder.BuildDefault();
    }

    private static string ResolveStrategyId(PlayableLeader leader)
    {
        if (leader == null || loadedData?.assignments == null) return DefaultStrategyId;

        string alignmentName = leader.alignment.ToString();
        HTNStrategyAssignment assignment = loadedData.assignments.FirstOrDefault(a =>
            a != null && string.Equals(a.alignment, alignmentName, StringComparison.OrdinalIgnoreCase));

        return assignment != null && !string.IsNullOrWhiteSpace(assignment.strategyId)
            ? assignment.strategyId
            : DefaultStrategyId;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        builtStrategies = new Dictionary<string, HTNCompoundTask>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

        try { loadedData = JsonUtility.FromJson<HTNStrategyLibraryData>(asset.text); }
        catch (Exception e)
        {
            Debug.LogWarning($"AIStrategyLibrary: could not parse {ResourcePath}.json — using built-in default strategy. {e.Message}");
            return;
        }

        if (loadedData?.strategies == null) return;

        foreach (HTNStrategyData strategyData in loadedData.strategies)
        {
            if (strategyData == null || string.IsNullOrWhiteSpace(strategyData.strategyId)) continue;
            HTNCompoundTask strategy = BuildStrategy(strategyData);
            if (strategy != null) builtStrategies[strategyData.strategyId] = strategy;
        }
    }

    private static HTNCompoundTask BuildStrategy(HTNStrategyData strategyData)
    {
        if (strategyData?.nodes == null || strategyData.nodes.Count == 0) return null;

        int index = 0;
        if (strategyData.nodes[0] == null || strategyData.nodes[0].depth != 0)
        {
            Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyData.strategyId}' does not start with a depth-0 CompoundTask root — ignoring it.");
            return null;
        }

        if (!Enum.TryParse(strategyData.nodes[0].type, true, out HTNNodeType rootType) || rootType != HTNNodeType.CompoundTask)
        {
            Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyData.strategyId}' root row must be a CompoundTask — ignoring it.");
            return null;
        }

        HTNCompoundTask root = BuildCompoundTask(strategyData.nodes, ref index, strategyData.strategyId);
        if (root == null || root.Methods.Count == 0)
        {
            Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyData.strategyId}' has no valid methods — ignoring it.");
            return null;
        }

        return root;
    }

    private static HTNCompoundTask BuildCompoundTask(List<HTNNodeData> nodes, ref int index, string strategyId)
    {
        HTNNodeData data = nodes[index];
        int myDepth = data.depth;
        index++;

        HTNCompoundTask compound = new() { TaskId = data.taskId };

        while (index < nodes.Count && nodes[index] != null && nodes[index].depth > myDepth)
        {
            if (nodes[index].depth != myDepth + 1) { index++; continue; }

            if (!Enum.TryParse(nodes[index].type, true, out HTNNodeType childType) || childType != HTNNodeType.Method)
            {
                Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyId}' expected a Method under CompoundTask '{compound.TaskId}' but found '{nodes[index].type}' — skipping.");
                int skippedDepth = nodes[index].depth;
                index++;
                SkipChildren(nodes, ref index, skippedDepth);
                continue;
            }

            HTNMethod method = BuildMethod(nodes, ref index, strategyId);
            if (method != null && method.Subtasks.Count > 0) compound.Methods.Add(method);
        }

        return compound;
    }

    private static HTNMethod BuildMethod(List<HTNNodeData> nodes, ref int index, string strategyId)
    {
        HTNNodeData data = nodes[index];
        int myDepth = data.depth;
        index++;

        HTNMethod method = new()
        {
            TaskId = data.taskId,
            Precondition = ResolvePredicate(data.precondition, data.invert, blankDefaultsToAlways: true, strategyId, "Method precondition")
        };

        while (index < nodes.Count && nodes[index] != null && nodes[index].depth > myDepth)
        {
            if (nodes[index].depth != myDepth + 1) { index++; continue; }

            if (!Enum.TryParse(nodes[index].type, true, out HTNNodeType childType))
            {
                Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyId}' has an unknown node type '{nodes[index].type}' — skipping.");
                int skippedDepth = nodes[index].depth;
                index++;
                SkipChildren(nodes, ref index, skippedDepth);
                continue;
            }

            IHTNNode subtask = childType switch
            {
                HTNNodeType.CompoundTask => BuildCompoundTask(nodes, ref index, strategyId),
                HTNNodeType.PrimitiveTask => BuildPrimitiveTask(nodes, ref index, strategyId),
                _ => SkipAndWarnMethodChild(nodes, ref index, strategyId)
            };
            if (subtask != null) method.Subtasks.Add(subtask);
        }

        return method;
    }

    private static IHTNNode SkipAndWarnMethodChild(List<HTNNodeData> nodes, ref int index, string strategyId)
    {
        Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyId}' expected a CompoundTask or PrimitiveTask subtask but found a Method row — skipping.");
        int myDepth = nodes[index].depth;
        index++;
        SkipChildren(nodes, ref index, myDepth);
        return null;
    }

    private static HTNPrimitiveTask BuildPrimitiveTask(List<HTNNodeData> nodes, ref int index, string strategyId)
    {
        HTNNodeData data = nodes[index];
        int myDepth = data.depth;
        index++;
        SkipChildren(nodes, ref index, myDepth); // leaves cannot have children

        return new HTNPrimitiveTask
        {
            TaskId = data.taskId,
            Precondition = ResolvePredicate(data.precondition, data.invert, blankDefaultsToAlways: true, strategyId, "PrimitiveTask precondition"),
            CompletionCondition = ResolvePredicate(data.completionCondition, data.completionInvert, blankDefaultsToAlways: false, strategyId, "PrimitiveTask completion condition"),
            AdvisorName = data.advisor ?? string.Empty
        };
    }

    private static Func<AIContext, AIBlackboard, bool> ResolvePredicate(string name, bool invert, bool blankDefaultsToAlways, string strategyId, string role)
    {
        // Blank precondition means "no gate" (Method) / blank completion means "never completes" — both map to Always/Never already in HTNRegistry.
        string effectiveName = string.IsNullOrWhiteSpace(name) ? (blankDefaultsToAlways ? "Always" : "Never") : name;

        if (!HTNRegistry.TryGetPredicate(effectiveName, out Func<AIContext, AIBlackboard, bool> predicate))
        {
            Debug.LogWarning($"AIStrategyLibrary: strategy '{strategyId}' references unknown {role} '{effectiveName}' — treating it as always-false.");
            predicate = (_, _) => false;
        }

        return invert ? (ctx, bb) => !predicate(ctx, bb) : predicate;
    }

    private static void SkipChildren(List<HTNNodeData> nodes, ref int index, int parentDepth)
    {
        while (index < nodes.Count && nodes[index] != null && nodes[index].depth > parentDepth)
        {
            index++;
        }
    }
}
