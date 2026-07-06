using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// ---------------------------------------------------------------------------
// Serialized form of the authored behaviour trees.
// Nodes are stored as a flat, depth-indented outline in document order:
// a node's parent is the nearest preceding node with depth - 1. Depth-0 nodes
// are children of an implicit root Selector (tried top-down until one succeeds,
// matching the situation-priority mental model).
// Edited via Window > RetroLOTR > AI Widget > Behaviour Trees.
// ---------------------------------------------------------------------------

[Serializable]
public class BehaviourTreeNodeData
{
    public int depth;
    public string type = BehaviourTreeNodeType.Action.ToString();
    public string condition = string.Empty;
    public bool invert;
    public string action = string.Empty;
}

[Serializable]
public class BehaviourTreeData
{
    public string treeId = string.Empty;
    public List<BehaviourTreeNodeData> nodes = new();
}

[Serializable]
public class BehaviourTreeAssignment
{
    public string alignment = string.Empty;
    public string treeId = string.Empty;
}

[Serializable]
public class BehaviourTreeLibraryData
{
    public List<BehaviourTreeData> trees = new();
    public List<BehaviourTreeAssignment> assignments = new();
}

public enum BehaviourTreeNodeType
{
    Selector = 0,
    Sequence = 1,
    Condition = 2,
    Action = 3
}

// ---------------------------------------------------------------------------
// Named vocabulary the authored trees can reference. Add entries here to make
// new conditions/actions available in the AI Widget dropdowns.
// ---------------------------------------------------------------------------

public static class BehaviourTreeRegistry
{
    private static readonly Dictionary<string, Func<AIContext, bool>> Conditions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NeedsEconomicHelp"]       = ctx => ctx.NeedsEconomicHelp,
        ["HasEnemyTarget"]          = ctx => ctx.HasEnemyTarget,
        ["HasNpcTarget"]            = ctx => ctx.HasNpcTarget,
        ["ShouldPrioritizeMovement"] = ctx => ctx.ShouldPrioritizeMovement,
        ["EconomyCritical"]         = ctx => ctx.EconomyStatus == EconomyStatus.Critical,
        ["EconomyWeak"]             = ctx => ctx.EconomyStatus == EconomyStatus.Weak,
        ["EconomyStable"]           = ctx => ctx.EconomyStatus == EconomyStatus.Stable,
        ["EconomySurplus"]          = ctx => ctx.EconomyStatus == EconomyStatus.Surplus,
    };

    private static readonly Dictionary<string, Func<AIContext, Task<bool>>> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EconomicAdvisor"]      = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Economic),
        ["MilitaristicAdvisor"]  = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Militaristic),
        ["DiplomaticAdvisor"]    = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Diplomatic),
        ["IntelligenceAdvisor"]  = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Intelligence),
        ["MagicAdvisor"]         = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Magic),
        ["MovementAdvisor"]      = ctx => ctx.TryExecuteAdvisorActionAsync(AdvisorType.Movement),
        ["BestAvailableAction"]  = ctx => ctx.TryExecuteBestAvailableActionAsync(),
        ["Pass"]                 = ctx => ctx.PassAsync(),
    };

    public static IReadOnlyList<string> ConditionNames => Conditions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    public static IReadOnlyList<string> ActionNames => Actions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool TryGetCondition(string name, out Func<AIContext, bool> predicate)
        => Conditions.TryGetValue(name ?? string.Empty, out predicate);

    public static bool TryGetAction(string name, out Func<AIContext, Task<bool>> action)
        => Actions.TryGetValue(name ?? string.Empty, out action);
}

// ---------------------------------------------------------------------------
// Loads authored trees from Resources and builds IBehaviourNode graphs.
// Falls back to AIBehaviourTreeBuilder.BuildDefault() when no valid authored
// tree applies.
// ---------------------------------------------------------------------------

public static class AIBehaviourTreeLibrary
{
    public const string ResourcePath = "AI/BehaviourTrees";
    public const string DefaultTreeId = "default";

    private static BehaviourTreeLibraryData loadedData;
    private static Dictionary<string, IBehaviourNode> builtTrees;
    private static bool loaded;

    public static void Reload()
    {
        loadedData = null;
        builtTrees = null;
        loaded = false;
    }

    public static IBehaviourNode GetTreeFor(PlayableLeader leader)
    {
        EnsureLoaded();

        string treeId = ResolveTreeId(leader);
        if (!string.IsNullOrWhiteSpace(treeId)
            && builtTrees != null
            && builtTrees.TryGetValue(treeId, out IBehaviourNode tree)
            && tree != null)
        {
            return tree;
        }

        if (builtTrees != null
            && builtTrees.TryGetValue(DefaultTreeId, out IBehaviourNode defaultTree)
            && defaultTree != null)
        {
            return defaultTree;
        }

        return AIBehaviourTreeBuilder.BuildDefault();
    }

    private static string ResolveTreeId(PlayableLeader leader)
    {
        if (leader == null || loadedData?.assignments == null) return DefaultTreeId;

        string alignmentName = leader.alignment.ToString();
        BehaviourTreeAssignment assignment = loadedData.assignments.FirstOrDefault(a =>
            a != null && string.Equals(a.alignment, alignmentName, StringComparison.OrdinalIgnoreCase));

        return assignment != null && !string.IsNullOrWhiteSpace(assignment.treeId)
            ? assignment.treeId
            : DefaultTreeId;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        builtTrees = new Dictionary<string, IBehaviourNode>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

        try { loadedData = JsonUtility.FromJson<BehaviourTreeLibraryData>(asset.text); }
        catch (Exception e)
        {
            Debug.LogWarning($"AIBehaviourTreeLibrary: could not parse {ResourcePath}.json — using built-in default tree. {e.Message}");
            return;
        }

        if (loadedData?.trees == null) return;

        foreach (BehaviourTreeData treeData in loadedData.trees)
        {
            if (treeData == null || string.IsNullOrWhiteSpace(treeData.treeId)) continue;
            IBehaviourNode tree = BuildTree(treeData);
            if (tree != null) builtTrees[treeData.treeId] = tree;
        }
    }

    // Builds the implicit-root Selector over the depth-indented node outline.
    private static IBehaviourNode BuildTree(BehaviourTreeData treeData)
    {
        if (treeData?.nodes == null || treeData.nodes.Count == 0) return null;

        int index = 0;
        List<IBehaviourNode> roots = new();
        while (index < treeData.nodes.Count)
        {
            if (treeData.nodes[index] == null || treeData.nodes[index].depth != 0)
            {
                index++;
                continue;
            }
            IBehaviourNode node = BuildNode(treeData, ref index, treeData.treeId);
            if (node != null) roots.Add(node);
        }

        if (roots.Count == 0)
        {
            Debug.LogWarning($"AIBehaviourTreeLibrary: tree '{treeData.treeId}' has no valid nodes — ignoring it.");
            return null;
        }

        return roots.Count == 1 ? roots[0] : new SelectorNode(roots.ToArray());
    }

    private static IBehaviourNode BuildNode(BehaviourTreeData treeData, ref int index, string treeId)
    {
        BehaviourTreeNodeData data = treeData.nodes[index];
        int myDepth = data.depth;
        index++;

        if (!Enum.TryParse(data.type, true, out BehaviourTreeNodeType nodeType))
        {
            Debug.LogWarning($"AIBehaviourTreeLibrary: tree '{treeId}' has unknown node type '{data.type}' — skipping node and its children.");
            SkipChildren(treeData, ref index, myDepth);
            return null;
        }

        if (nodeType == BehaviourTreeNodeType.Condition || nodeType == BehaviourTreeNodeType.Action)
        {
            // Leaves cannot have children; anything indented under them is ignored.
            SkipChildren(treeData, ref index, myDepth);

            if (nodeType == BehaviourTreeNodeType.Condition)
            {
                if (!BehaviourTreeRegistry.TryGetCondition(data.condition, out Func<AIContext, bool> predicate))
                {
                    Debug.LogWarning($"AIBehaviourTreeLibrary: tree '{treeId}' references unknown condition '{data.condition}' — treating it as always-false.");
                    predicate = _ => false;
                }
                bool invert = data.invert;
                return new ConditionNode(ctx => invert ? !predicate(ctx) : predicate(ctx));
            }

            if (!BehaviourTreeRegistry.TryGetAction(data.action, out Func<AIContext, Task<bool>> action))
            {
                Debug.LogWarning($"AIBehaviourTreeLibrary: tree '{treeId}' references unknown action '{data.action}' — treating it as a failed action.");
                action = _ => Task.FromResult(false);
            }
            return new ActionNode(action);
        }

        List<IBehaviourNode> children = new();
        while (index < treeData.nodes.Count
            && treeData.nodes[index] != null
            && treeData.nodes[index].depth > myDepth)
        {
            if (treeData.nodes[index].depth != myDepth + 1)
            {
                index++;
                continue;
            }
            IBehaviourNode child = BuildNode(treeData, ref index, treeId);
            if (child != null) children.Add(child);
        }

        if (children.Count == 0)
        {
            Debug.LogWarning($"AIBehaviourTreeLibrary: tree '{treeId}' has an empty {nodeType} node — skipping it.");
            return null;
        }

        return nodeType == BehaviourTreeNodeType.Selector
            ? new SelectorNode(children.ToArray())
            : new SequenceNode(children.ToArray());
    }

    private static void SkipChildren(BehaviourTreeData treeData, ref int index, int parentDepth)
    {
        while (index < treeData.nodes.Count
            && treeData.nodes[index] != null
            && treeData.nodes[index].depth > parentDepth)
        {
            index++;
        }
    }
}
