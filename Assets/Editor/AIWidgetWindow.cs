using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class AIWidgetWindow : EditorWindow
{
    private enum Tab
    {
        Situations = 0,
        BehaviourTrees = 1,
        NN = 2
    }

    private static readonly string[] TabLabels = { "Situations", "Behaviour Trees", "NN" };

    private const string PriorityAssetPath = "Assets/Resources/" + SituationEvaluator.PriorityResourcePath + ".json";
    private const string TreesAssetPath = "Assets/Resources/" + AIBehaviourTreeLibrary.ResourcePath + ".json";

    private Tab currentTab = Tab.Situations;

    // Situations tab
    private Vector2 situationsScroll;
    private List<CardSituationEnum> situationOrder = new();
    private ReorderableList situationList;
    private bool orderDirty;

    // Behaviour Trees tab
    private BehaviourTreeLibraryData treeLibrary = new();
    private int selectedTreeIndex;
    private Vector2 treesScroll;
    private bool treesDirty;

    private static readonly Color RowHighlightColor = new(0.26f, 0.53f, 0.96f, 0.22f);

    // Advisors section (inside the Behaviour Trees tab)
    private readonly Dictionary<string, float> advisorWeights = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AdvisorType> advisorActionOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> advisorActionBonuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionScoreFlags> advisorActionFlags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> advisorGroupExpanded = new();
    private List<(string actionClass, AdvisorType defaultAdvisor)> actionCatalog;
    private Dictionary<string, List<CardUsage>> cardsByActionRef;
    private readonly HashSet<string> expandedActions = new(StringComparer.OrdinalIgnoreCase);
    private bool advisorsSectionExpanded;
    private bool onlyShowCardActions = true;
    private bool advisorsDirty;
    private string actionSearch = string.Empty;
    private int actionSortMode;

    private static readonly string[] ActionSortLabels = { "Sort: Score", "Sort: Advisor", "Sort: Name", "Sort: Card Count" };

    // Score-preview scenario: user-set assumptions for everything the real
    // scoring reads from the board at runtime.
    private int simCommander = 2;
    private int simAgent = 2;
    private int simEmissary = 2;
    private int simMage = 2;
    private int simArtifactsCarried;
    private bool simLeadingArmy;
    private bool simMovementPriority;
    private bool simHostageToRescue;
    private bool simHoldingHostage;
    private int simGoldBuffer = 50;
    private int simGoldPerTurn = 10;
    private int simMyArmyStrength = 100;
    private int simEnemyStrength = 100;
    private float simEnemyDistance = 5f;
    private float simEnemyCharacterDistance = 5f;
    private float simNpcDistance = 5f;
    private float simDestinationDistance = 3f;
    private float simArtifactShare = 0.25f;

    private class CardUsage
    {
        public string cardName;
        public string effect;
        public int difficulty;
        public int goldCost;
    }

    private static readonly string[] NodeTypeLabels =
        { "Selector", "Sequence", "Condition", "Action" };

    private static readonly string[] InvertLabels = { "IS TRUE", "IS FALSE" };

    [MenuItem("Window/RetroLOTR/AI Widget")]
    public static void Open()
    {
        GetWindow<AIWidgetWindow>("AI Widget");
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
        LoadSituationOrder();
        LoadTreeLibrary();
        LoadAdvisorConfig();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.MouseMove) Repaint();

        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, TabLabels);
        EditorGUILayout.Space();

        switch (currentTab)
        {
            case Tab.Situations:
                DrawSituationsTab();
                break;
            case Tab.BehaviourTrees:
                DrawBehaviourTreesTab();
                break;
            case Tab.NN:
                EditorGUILayout.HelpBox("NN — not implemented yet.", MessageType.Info);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Situations tab
    // ------------------------------------------------------------------

    private void DrawSituationsTab()
    {
        EditorGUILayout.LabelField("Situation priority", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag to reorder. When more than 2 situations are active on a hex, the top-most 2 that can back a playable card are offered as opportunity cards.\n"
            + $"Saved to {PriorityAssetPath} and read by SituationEvaluator at runtime.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!orderDirty))
        {
            if (GUILayout.Button("Save", GUILayout.Width(90f))) SaveSituationOrder();
            if (GUILayout.Button("Revert", GUILayout.Width(90f))) LoadSituationOrder();
        }
        if (GUILayout.Button("Reset To Default", GUILayout.Width(130f)))
        {
            situationOrder = SituationEvaluator.GetDefaultPriority().ToList();
            RebuildReorderableList();
            orderDirty = true;
        }
        GUILayout.FlexibleSpace();
        if (orderDirty)
        {
            GUILayout.Label("Unsaved changes", EditorStyles.miniBoldLabel);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        situationsScroll = EditorGUILayout.BeginScrollView(situationsScroll);
        situationList?.DoLayoutList();
        EditorGUILayout.EndScrollView();
    }

    private void LoadSituationOrder()
    {
        situationOrder = ReadAuthoredOrder();
        RebuildReorderableList();
        orderDirty = false;
    }

    // Authored order from the JSON (if any), then any situations it is missing,
    // in default order — same merge the runtime performs.
    private static List<CardSituationEnum> ReadAuthoredOrder()
    {
        List<CardSituationEnum> order = new();
        HashSet<CardSituationEnum> seen = new();

        if (File.Exists(PriorityAssetPath))
        {
            try
            {
                SituationPriorityData data = JsonUtility.FromJson<SituationPriorityData>(File.ReadAllText(PriorityAssetPath));
                if (data?.situations != null)
                {
                    foreach (string name in data.situations)
                    {
                        if (Enum.TryParse(name, true, out CardSituationEnum situation)
                            && situation != CardSituationEnum.None
                            && seen.Add(situation))
                        {
                            order.Add(situation);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AIWidget: could not parse {PriorityAssetPath} — falling back to default order. {e.Message}");
            }
        }

        foreach (CardSituationEnum situation in SituationEvaluator.GetDefaultPriority())
        {
            if (seen.Add(situation)) order.Add(situation);
        }

        return order;
    }

    private void RebuildReorderableList()
    {
        situationList = new ReorderableList(situationOrder, typeof(CardSituationEnum), true, true, false, false)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Priority (top = offered first)"),
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 1f;
                rect.height = EditorGUIUtility.singleLineHeight;
                CardSituationEnum situation = situationOrder[index];
                EditorGUI.LabelField(rect, $"{index + 1}.", EditorStyles.miniLabel);
                rect.x += 28f;
                rect.width -= 28f;
                EditorGUI.LabelField(rect, ObjectNames.NicifyVariableName(situation.ToString()));
            },
            onReorderCallback = _ => orderDirty = true
        };
    }

    private void SaveSituationOrder()
    {
        SituationPriorityData data = new()
        {
            situations = situationOrder.Select(s => s.ToString()).ToList()
        };

        WriteJsonAsset(PriorityAssetPath, JsonUtility.ToJson(data, true));
        SituationEvaluator.ReloadPriority();
        orderDirty = false;
        Debug.Log($"AIWidget: saved situation priority to {PriorityAssetPath}");
    }

    // ------------------------------------------------------------------
    // Behaviour Trees tab
    // ------------------------------------------------------------------

    private void DrawBehaviourTreesTab()
    {
        EditorGUILayout.LabelField("Behaviour trees", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each AI character ticks its leader's tree once per turn. Depth-0 rows act as a priority list (tried top-down until one succeeds). "
            + "Selector = first child that succeeds; Sequence = all children in order, stops on failure; Condition/Action = leaves.\n"
            + $"Saved to {TreesAssetPath} and read by AIBehaviourTreeLibrary at runtime.",
            MessageType.None);

        DrawTreesToolbar();
        EditorGUILayout.Space();
        DrawAssignments();
        EditorGUILayout.Space();

        if (treeLibrary.trees.Count == 0)
        {
            EditorGUILayout.HelpBox("No trees defined. Click 'Add Tree'.", MessageType.Warning);
            return;
        }

        selectedTreeIndex = Mathf.Clamp(selectedTreeIndex, 0, treeLibrary.trees.Count - 1);
        BehaviourTreeData tree = treeLibrary.trees[selectedTreeIndex];

        foreach (string issue in ValidateTree(tree))
        {
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }

        treesScroll = EditorGUILayout.BeginScrollView(treesScroll);
        DrawNodeOutline(tree);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("FINISH — the first root-level row that succeeds ends the character's action for this turn.", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Node (root level)", GUILayout.Width(170f)))
        {
            tree.nodes.Add(NewLeafNode());
            treesDirty = true;
        }

        EditorGUILayout.Space(14f);
        advisorsSectionExpanded = EditorGUILayout.Foldout(advisorsSectionExpanded,
            "Advisors — what the advisor rows above actually do", true, EditorStyles.foldoutHeader);
        if (advisorsSectionExpanded)
        {
            DrawAdvisorsSection();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTreesToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        string[] treeIds = treeLibrary.trees.Select(t => t.treeId).ToArray();
        if (treeIds.Length > 0)
        {
            selectedTreeIndex = EditorGUILayout.Popup(Mathf.Clamp(selectedTreeIndex, 0, treeIds.Length - 1), treeIds, GUILayout.Width(180f));

            string currentId = treeLibrary.trees[selectedTreeIndex].treeId;
            string newId = EditorGUILayout.DelayedTextField(currentId, GUILayout.Width(160f));
            if (!string.Equals(newId, currentId, StringComparison.Ordinal)) RenameSelectedTree(newId);
        }

        if (GUILayout.Button("Add Tree", GUILayout.Width(80f)))
        {
            BehaviourTreeData tree = new()
            {
                treeId = MakeUniqueTreeId("new_tree"),
                nodes = new List<BehaviourTreeNodeData>
                {
                    new() { depth = 0, type = "Action", action = "BestAvailableAction" },
                    new() { depth = 0, type = "Action", action = "Pass" }
                }
            };
            treeLibrary.trees.Add(tree);
            selectedTreeIndex = treeLibrary.trees.Count - 1;
            treesDirty = true;
        }

        using (new EditorGUI.DisabledScope(treeLibrary.trees.Count == 0))
        {
            if (GUILayout.Button("Duplicate", GUILayout.Width(80f)))
            {
                BehaviourTreeData source = treeLibrary.trees[selectedTreeIndex];
                BehaviourTreeData copy = JsonUtility.FromJson<BehaviourTreeData>(JsonUtility.ToJson(source));
                copy.treeId = MakeUniqueTreeId(source.treeId);
                treeLibrary.trees.Add(copy);
                selectedTreeIndex = treeLibrary.trees.Count - 1;
                treesDirty = true;
            }

            using (new EditorGUI.DisabledScope(treeLibrary.trees.Count <= 1))
            {
                if (GUILayout.Button("Delete", GUILayout.Width(70f))
                    && EditorUtility.DisplayDialog("Delete tree",
                        $"Delete tree '{treeLibrary.trees[selectedTreeIndex].treeId}'?", "Delete", "Cancel"))
                {
                    string removedId = treeLibrary.trees[selectedTreeIndex].treeId;
                    treeLibrary.trees.RemoveAt(selectedTreeIndex);
                    selectedTreeIndex = Mathf.Clamp(selectedTreeIndex, 0, treeLibrary.trees.Count - 1);
                    string fallbackId = treeLibrary.trees[selectedTreeIndex].treeId;
                    foreach (BehaviourTreeAssignment assignment in treeLibrary.assignments)
                    {
                        if (string.Equals(assignment.treeId, removedId, StringComparison.OrdinalIgnoreCase))
                            assignment.treeId = fallbackId;
                    }
                    treesDirty = true;
                }
            }
        }

        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!treesDirty && !advisorsDirty))
        {
            if (GUILayout.Button("Save", GUILayout.Width(90f)))
            {
                if (treesDirty) SaveTreeLibrary();
                if (advisorsDirty) SaveAdvisorConfig();
            }
            if (GUILayout.Button("Revert", GUILayout.Width(90f)))
            {
                LoadTreeLibrary();
                LoadAdvisorConfig();
            }
        }
        if (treesDirty || advisorsDirty) GUILayout.Label("Unsaved changes", EditorStyles.miniBoldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAssignments()
    {
        EditorGUILayout.LabelField("Tree per alignment", EditorStyles.boldLabel);

        string[] treeIds = treeLibrary.trees.Select(t => t.treeId).ToArray();
        if (treeIds.Length == 0) return;

        foreach (string alignmentName in Enum.GetNames(typeof(AlignmentEnum)))
        {
            BehaviourTreeAssignment assignment = treeLibrary.assignments.FirstOrDefault(a =>
                string.Equals(a.alignment, alignmentName, StringComparison.OrdinalIgnoreCase));
            if (assignment == null)
            {
                assignment = new BehaviourTreeAssignment { alignment = alignmentName, treeId = treeIds[0] };
                treeLibrary.assignments.Add(assignment);
            }

            int current = Array.FindIndex(treeIds, id => string.Equals(id, assignment.treeId, StringComparison.OrdinalIgnoreCase));
            if (current < 0) current = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(alignmentName), GUILayout.Width(120f));
            int picked = EditorGUILayout.Popup(current, treeIds, GUILayout.Width(180f));
            EditorGUILayout.EndHorizontal();

            if (picked != current || !string.Equals(assignment.treeId, treeIds[picked], StringComparison.Ordinal))
            {
                if (!string.Equals(assignment.treeId, treeIds[picked], StringComparison.Ordinal)) treesDirty = true;
                assignment.treeId = treeIds[picked];
            }
        }
    }

    private void DrawNodeOutline(BehaviourTreeData tree)
    {
        List<BehaviourTreeNodeData> nodes = tree.nodes;
        int pendingOp = -1; // 0 up, 1 down, 2 outdent, 3 indent, 4 insert-after, 5 delete
        int pendingIndex = -1;
        int hoverEnd = -1; // exclusive end of the hovered subtree's row range

        for (int i = 0; i < nodes.Count; i++)
        {
            BehaviourTreeNodeData node = nodes[i];
            if (node == null) continue;

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            // The subtree a hovered row would move starts at that row, so it can
            // be highlighted in this same pass: its rows all come after it.
            if (Event.current.type == EventType.Repaint && rowRect.Contains(Event.current.mousePosition))
            {
                hoverEnd = i + SubtreeCount(nodes, i);
            }
            if (i < hoverEnd)
            {
                EditorGUI.DrawRect(rowRect, RowHighlightColor);
            }
            GUILayout.Space(8f + node.depth * 24f);

            GUILayout.Label(GetRowConnective(nodes, i), EditorStyles.miniBoldLabel, GUILayout.Width(52f));

            int typeIndex = Mathf.Max(0, Array.IndexOf(NodeTypeLabels, node.type));
            int newTypeIndex = EditorGUILayout.Popup(typeIndex, NodeTypeLabels, GUILayout.Width(85f));
            if (newTypeIndex != typeIndex)
            {
                node.type = NodeTypeLabels[newTypeIndex];
                treesDirty = true;
            }

            BehaviourTreeNodeType nodeType = ParseNodeType(node.type);
            switch (nodeType)
            {
                case BehaviourTreeNodeType.Condition:
                {
                    string picked = DrawNamePopup(node.condition, BehaviourTreeRegistry.ConditionNames);
                    if (!string.Equals(picked, node.condition, StringComparison.Ordinal)) { node.condition = picked; treesDirty = true; }

                    int invertIndex = EditorGUILayout.Popup(node.invert ? 1 : 0, InvertLabels, GUILayout.Width(80f));
                    bool newInvert = invertIndex == 1;
                    if (newInvert != node.invert) { node.invert = newInvert; treesDirty = true; }
                    break;
                }
                case BehaviourTreeNodeType.Action:
                {
                    string picked = DrawNamePopup(node.action, BehaviourTreeRegistry.ActionNames);
                    if (!string.Equals(picked, node.action, StringComparison.Ordinal)) { node.action = picked; treesDirty = true; }
                    break;
                }
                default:
                    GUILayout.Label(nodeType == BehaviourTreeNodeType.Selector
                        ? "one of the rows below (first that works):"
                        : "all of the rows below (in order, quit if one fails):", EditorStyles.miniLabel);
                    break;
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(PrevSiblingIndex(nodes, i) < 0))
                if (GUILayout.Button("▲", GUILayout.Width(24f))) { pendingOp = 0; pendingIndex = i; }
            using (new EditorGUI.DisabledScope(NextSiblingIndex(nodes, i) < 0))
                if (GUILayout.Button("▼", GUILayout.Width(24f))) { pendingOp = 1; pendingIndex = i; }
            using (new EditorGUI.DisabledScope(node.depth <= 0))
                if (GUILayout.Button("◀", GUILayout.Width(24f))) { pendingOp = 2; pendingIndex = i; }
            using (new EditorGUI.DisabledScope(PrevSiblingIndex(nodes, i) < 0))
                if (GUILayout.Button("▶", GUILayout.Width(24f))) { pendingOp = 3; pendingIndex = i; }
            if (GUILayout.Button("+", GUILayout.Width(24f))) { pendingOp = 4; pendingIndex = i; }
            if (GUILayout.Button("✕", GUILayout.Width(24f))) { pendingOp = 5; pendingIndex = i; }

            EditorGUILayout.EndHorizontal();
        }

        if (pendingIndex < 0) return;

        switch (pendingOp)
        {
            case 0: MoveSubtreeUp(nodes, pendingIndex); break;
            case 1:
            {
                int next = NextSiblingIndex(nodes, pendingIndex);
                if (next >= 0) MoveSubtreeUp(nodes, next);
                break;
            }
            case 2: ShiftSubtreeDepth(nodes, pendingIndex, -1); break;
            case 3: ShiftSubtreeDepth(nodes, pendingIndex, +1); break;
            case 4: nodes.Insert(pendingIndex + SubtreeCount(nodes, pendingIndex), NewLeafNode(nodes[pendingIndex].depth)); break;
            case 5: nodes.RemoveRange(pendingIndex, SubtreeCount(nodes, pendingIndex)); break;
        }
        treesDirty = true;
        GUI.FocusControl(null);
    }

    private static string DrawNamePopup(string current, IReadOnlyList<string> names)
    {
        List<string> options = names.ToList();
        if (!string.IsNullOrEmpty(current) && !options.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(current + " (unknown)");
        }

        int index = options.FindIndex(o => string.Equals(o, current, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = string.IsNullOrEmpty(current) ? 0 : options.Count - 1;

        int picked = EditorGUILayout.Popup(index, options.ToArray(), GUILayout.Width(200f));
        return picked < names.Count ? names[picked] : current;
    }

    private static BehaviourTreeNodeType ParseNodeType(string type)
        => Enum.TryParse(type, true, out BehaviourTreeNodeType parsed) ? parsed : BehaviourTreeNodeType.Action;

    // Pseudocode connective for a row, based on its parent's type and whether
    // it is the first sibling: Selector children chain with TRY/OR ELSE,
    // Sequence children with IF/AND IF (conditions) and DO/THEN (the rest).
    private static string GetRowConnective(List<BehaviourTreeNodeData> nodes, int index)
    {
        BehaviourTreeNodeType parentType = BehaviourTreeNodeType.Selector; // implicit root
        for (int j = index - 1; j >= 0; j--)
        {
            if (nodes[j].depth < nodes[index].depth)
            {
                parentType = ParseNodeType(nodes[j].type);
                break;
            }
        }

        bool isFirstSibling = PrevSiblingIndex(nodes, index) < 0;

        if (parentType == BehaviourTreeNodeType.Selector)
        {
            return isFirstSibling ? "TRY" : "OR ELSE";
        }

        bool isCondition = ParseNodeType(nodes[index].type) == BehaviourTreeNodeType.Condition;
        if (isCondition) return isFirstSibling ? "IF" : "AND IF";
        return isFirstSibling ? "DO" : "THEN";
    }

    private static BehaviourTreeNodeData NewLeafNode(int depth = 0)
        => new() { depth = depth, type = "Action", action = "Pass" };

    // ---- outline structure helpers (a subtree = a node plus the contiguous
    // run of following nodes with greater depth) ----

    private static int SubtreeCount(List<BehaviourTreeNodeData> nodes, int index)
    {
        int count = 1;
        int depth = nodes[index].depth;
        while (index + count < nodes.Count && nodes[index + count].depth > depth) count++;
        return count;
    }

    private static int PrevSiblingIndex(List<BehaviourTreeNodeData> nodes, int index)
    {
        int depth = nodes[index].depth;
        for (int j = index - 1; j >= 0; j--)
        {
            if (nodes[j].depth < depth) return -1;
            if (nodes[j].depth == depth) return j;
        }
        return -1;
    }

    private static int NextSiblingIndex(List<BehaviourTreeNodeData> nodes, int index)
    {
        int next = index + SubtreeCount(nodes, index);
        if (next < nodes.Count && nodes[next].depth == nodes[index].depth) return next;
        return -1;
    }

    private static void MoveSubtreeUp(List<BehaviourTreeNodeData> nodes, int index)
    {
        int prev = PrevSiblingIndex(nodes, index);
        if (prev < 0) return;

        int count = SubtreeCount(nodes, index);
        List<BehaviourTreeNodeData> block = nodes.GetRange(index, count);
        nodes.RemoveRange(index, count);
        nodes.InsertRange(prev, block);
    }

    private static void ShiftSubtreeDepth(List<BehaviourTreeNodeData> nodes, int index, int delta)
    {
        if (delta > 0 && PrevSiblingIndex(nodes, index) < 0) return;
        if (delta < 0 && nodes[index].depth <= 0) return;

        int count = SubtreeCount(nodes, index);
        for (int j = index; j < index + count; j++)
        {
            nodes[j].depth += delta;
        }
    }

    private static List<string> ValidateTree(BehaviourTreeData tree)
    {
        List<string> issues = new();
        List<BehaviourTreeNodeData> nodes = tree.nodes;

        if (nodes.Count == 0)
        {
            issues.Add("Tree has no nodes — the built-in default tree will be used instead.");
            return issues;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            BehaviourTreeNodeData node = nodes[i];
            BehaviourTreeNodeType type = ParseNodeType(node.type);
            bool hasChildren = i + 1 < nodes.Count && nodes[i + 1].depth > node.depth;

            if (i == 0 && node.depth != 0)
                issues.Add("Row 1 must be at root level (depth 0).");
            if (i > 0 && node.depth > nodes[i - 1].depth + 1)
                issues.Add($"Row {i + 1}: indented more than one level below the previous row — it will be skipped.");

            if ((type == BehaviourTreeNodeType.Selector || type == BehaviourTreeNodeType.Sequence) && !hasChildren)
                issues.Add($"Row {i + 1}: {type} has no children — it will be skipped.");
            if ((type == BehaviourTreeNodeType.Condition || type == BehaviourTreeNodeType.Action) && hasChildren)
                issues.Add($"Row {i + 1}: {type} is a leaf — rows indented under it will be ignored.");
            if (type == BehaviourTreeNodeType.Condition && !BehaviourTreeRegistry.TryGetCondition(node.condition, out _))
                issues.Add($"Row {i + 1}: unknown condition '{node.condition}'.");
            if (type == BehaviourTreeNodeType.Action && !BehaviourTreeRegistry.TryGetAction(node.action, out _))
                issues.Add($"Row {i + 1}: unknown action '{node.action}'.");
        }

        return issues;
    }

    private string MakeUniqueTreeId(string baseId)
    {
        string candidate = baseId;
        int suffix = 2;
        while (treeLibrary.trees.Any(t => string.Equals(t.treeId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    private void RenameSelectedTree(string newId)
    {
        newId = (newId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newId)) return;
        if (treeLibrary.trees.Any(t => string.Equals(t.treeId, newId, StringComparison.OrdinalIgnoreCase))) return;

        string oldId = treeLibrary.trees[selectedTreeIndex].treeId;
        treeLibrary.trees[selectedTreeIndex].treeId = newId;
        foreach (BehaviourTreeAssignment assignment in treeLibrary.assignments)
        {
            if (string.Equals(assignment.treeId, oldId, StringComparison.OrdinalIgnoreCase))
                assignment.treeId = newId;
        }
        treesDirty = true;
    }

    private void LoadTreeLibrary()
    {
        treeLibrary = null;
        if (File.Exists(TreesAssetPath))
        {
            try { treeLibrary = JsonUtility.FromJson<BehaviourTreeLibraryData>(File.ReadAllText(TreesAssetPath)); }
            catch (Exception e)
            {
                Debug.LogWarning($"AIWidget: could not parse {TreesAssetPath} — starting from the built-in default tree. {e.Message}");
            }
        }

        treeLibrary ??= new BehaviourTreeLibraryData();
        treeLibrary.trees ??= new List<BehaviourTreeData>();
        treeLibrary.assignments ??= new List<BehaviourTreeAssignment>();
        treeLibrary.trees.RemoveAll(t => t == null);
        foreach (BehaviourTreeData tree in treeLibrary.trees)
        {
            tree.nodes ??= new List<BehaviourTreeNodeData>();
            tree.nodes.RemoveAll(n => n == null);
            ClampDepths(tree.nodes);
        }

        if (treeLibrary.trees.Count == 0)
        {
            treeLibrary.trees.Add(BuildDefaultTreeData());
        }

        selectedTreeIndex = Mathf.Clamp(selectedTreeIndex, 0, treeLibrary.trees.Count - 1);
        treesDirty = false;
    }

    private static void ClampDepths(List<BehaviourTreeNodeData> nodes)
    {
        int previousDepth = -1;
        foreach (BehaviourTreeNodeData node in nodes)
        {
            node.depth = Mathf.Max(0, Mathf.Min(node.depth, previousDepth + 1));
            previousDepth = node.depth;
        }
    }

    // Data mirror of AIBehaviourTreeBuilder.BuildDefault().
    private static BehaviourTreeData BuildDefaultTreeData()
    {
        return new BehaviourTreeData
        {
            treeId = AIBehaviourTreeLibrary.DefaultTreeId,
            nodes = new List<BehaviourTreeNodeData>
            {
                new() { depth = 0, type = "Sequence" },
                new() { depth = 1, type = "Condition", condition = "NeedsEconomicHelp" },
                new() { depth = 1, type = "Action", action = "EconomicAdvisor" },
                new() { depth = 0, type = "Sequence" },
                new() { depth = 1, type = "Condition", condition = "HasEnemyTarget" },
                new() { depth = 1, type = "Selector" },
                new() { depth = 2, type = "Action", action = "MilitaristicAdvisor" },
                new() { depth = 2, type = "Action", action = "IntelligenceAdvisor" },
                new() { depth = 2, type = "Action", action = "MagicAdvisor" },
                new() { depth = 2, type = "Action", action = "DiplomaticAdvisor" },
                new() { depth = 0, type = "Sequence" },
                new() { depth = 1, type = "Condition", condition = "ShouldPrioritizeMovement" },
                new() { depth = 1, type = "Action", action = "MovementAdvisor" },
                new() { depth = 0, type = "Action", action = "BestAvailableAction" },
                new() { depth = 0, type = "Action", action = "Pass" }
            }
        };
    }

    private void SaveTreeLibrary()
    {
        WriteJsonAsset(TreesAssetPath, JsonUtility.ToJson(treeLibrary, true));
        AIBehaviourTreeLibrary.Reload();
        treesDirty = false;
        Debug.Log($"AIWidget: saved behaviour trees to {TreesAssetPath}");
    }

    // ------------------------------------------------------------------
    // Advisors tab
    // ------------------------------------------------------------------

    private const string AdvisorAssetPath = "Assets/Resources/" + AIAdvisorConfig.ResourcePath + ".json";

    private void DrawAdvisorsSection()
    {
        EditorGUILayout.HelpBox(
            "The AI plays cards from its hand, exactly like a human player. Every card carries an action "
            + "(the 'action' field in the deck JSONs, e.g. StealGold) that runs when the card is played.\n\n"
            + "When a tree row calls an advisor (e.g. TRY MilitaristicAdvisor), that advisor:\n"
            + "  1. takes the cards currently in the AI leader's hand that are playable by this character,\n"
            + "  2. keeps only the cards whose action belongs to it (the ownership list below),\n"
            + "  3. scores each card and plays the single highest-scoring one.\n"
            + "If it holds no playable card, the row FAILS and the tree falls through to the next OR ELSE.\n\n"
            + "Card score = Base Score\n"
            + "  −  difficulty penalty (card difficulty ÷ Difficulty Divisor, capped at Max Difficulty Penalty)\n"
            + "  −  gold-cost pressure (bigger when the card is expensive and the treasury is thin;\n"
            + "      multiplied by Cost Pressure When Poor while the economy needs help)\n"
            + "  +  affinity (the character's skill levels × the Affinity weights — a mage leans Magic, an agent Intelligence)\n"
            + "  +  the advisor's situational bonuses (its group below — economy state, enemy distance, being outmatched, ...).\n"
            + "Typical totals land between about −5 and +15; a bonus of 3 is significant, 10 is dominant.\n\n"
            + $"Saved to {AdvisorAssetPath} (part of this tab's Save button).",
            MessageType.Info);

        if (GUILayout.Button("Reset Advisor Tuning To Default", GUILayout.Width(220f)))
        {
            foreach (AdvisorWeightDefinition definition in AIAdvisorConfig.KnownWeights)
            {
                advisorWeights[definition.key] = definition.defaultValue;
            }
            advisorActionOverrides.Clear();
            advisorActionBonuses.Clear();
            advisorActionFlags.Clear();
            advisorsDirty = true;
        }
        EditorGUILayout.Space();

        DrawAdvisorWeights();
        EditorGUILayout.Space(10f);
        DrawActionOwnership();
    }

    private void DrawAdvisorWeights()
    {
        EditorGUILayout.LabelField("Scoring weights", EditorStyles.boldLabel);

        foreach (IGrouping<string, AdvisorWeightDefinition> group in AIAdvisorConfig.KnownWeights
                     .GroupBy(d => d.key.Split('.')[0]))
        {
            bool expanded = !advisorGroupExpanded.TryGetValue(group.Key, out bool value) || value;
            expanded = EditorGUILayout.Foldout(expanded, group.Key, true, EditorStyles.foldoutHeader);
            advisorGroupExpanded[group.Key] = expanded;
            if (!expanded) continue;

            foreach (AdvisorWeightDefinition definition in group)
            {
                float current = advisorWeights.TryGetValue(definition.key, out float v) ? v : definition.defaultValue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                string label = ObjectNames.NicifyVariableName(
                    definition.key.Substring(group.Key.Length + 1).Replace(".", " "));
                EditorGUILayout.LabelField(new GUIContent(label, definition.description), GUILayout.Width(280f));

                float picked = EditorGUILayout.FloatField(current, GUILayout.Width(60f));
                if (!Mathf.Approximately(picked, current))
                {
                    advisorWeights[definition.key] = picked;
                    advisorsDirty = true;
                    current = picked;
                }

                if (!Mathf.Approximately(current, definition.defaultValue))
                {
                    GUILayout.Label($"(default {definition.defaultValue})", EditorStyles.miniLabel, GUILayout.Width(80f));
                    if (GUILayout.Button("↺", GUILayout.Width(24f)))
                    {
                        advisorWeights[definition.key] = definition.defaultValue;
                        advisorsDirty = true;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.Space(8f);
                GUILayout.Label(definition.description, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawActionOwnership()
    {
        EditorGUILayout.LabelField("Card actions → advisor ownership", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Which advisor gets to play the cards that carry each action. 'Default' keeps the value coded on the action class; "
            + "an override moves those cards to another advisor's pool. 'Not advised' removes them from every advisor's pool — "
            + "only a Best Available Action tree row can still play them.\n\n"
            + "PRIORITY: within an advisor there is no manual order — the advisor always plays its HIGHEST-SCORING card. "
            + "To prioritize one action over another, give it a Bonus: a flat number added to its score every time it is considered. "
            + "+2 wins most ties, +10 dominates, negative pushes it to last resort.",
            MessageType.None);

        DrawScenarioInputs();
        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        actionSearch = EditorGUILayout.TextField("Filter", actionSearch);
        actionSortMode = EditorGUILayout.Popup(actionSortMode, ActionSortLabels, GUILayout.Width(130f));
        onlyShowCardActions = GUILayout.Toggle(onlyShowCardActions, "Only actions used by cards", GUILayout.Width(180f));
        EditorGUILayout.EndHorizontal();

        actionCatalog ??= BuildActionCatalog();
        cardsByActionRef ??= BuildCardUsageMap();
        string[] advisorNames = Enum.GetNames(typeof(AdvisorType))
            .Select(n => n == nameof(AdvisorType.None) ? "Not advised" : n)
            .ToArray();

        foreach ((string actionClass, AdvisorType defaultAdvisor) in SortedActionCatalog())
        {
            cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards);
            bool usedByCards = cards != null && cards.Count > 0;
            if (onlyShowCardActions && !usedByCards) continue;

            if (!string.IsNullOrWhiteSpace(actionSearch)
                && actionClass.IndexOf(actionSearch, StringComparison.OrdinalIgnoreCase) < 0
                && (cards == null || !cards.Any(c => c.cardName.IndexOf(actionSearch, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                continue;
            }

            bool hasOverride = advisorActionOverrides.TryGetValue(actionClass, out AdvisorType overridden);
            AdvisorType resolvedAdvisor = hasOverride ? overridden : defaultAdvisor;
            bool expanded = expandedActions.Contains(actionClass);
            float bonus = advisorActionBonuses.TryGetValue(actionClass, out float b) ? b : 0f;
            float bestScore = BestScoreForAction(actionClass, resolvedAdvisor, cards, bonus);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            bool nowExpanded = GUILayout.Toggle(expanded, ObjectNames.NicifyVariableName(actionClass), EditorStyles.foldout, GUILayout.Width(220f));
            if (nowExpanded != expanded)
            {
                if (nowExpanded) expandedActions.Add(actionClass);
                else expandedActions.Remove(actionClass);
            }

            GUILayout.Label(new GUIContent(bestScore.ToString("0.0"),
                "Exact score of this action's best card under the scenario above. The advisor plays the card with the highest score."),
                EditorStyles.miniBoldLabel, GUILayout.Width(34f));

            string[] options = new string[advisorNames.Length + 1];
            options[0] = defaultAdvisor == AdvisorType.None ? "Not advised" : $"Default ({defaultAdvisor})";
            Array.Copy(advisorNames, 0, options, 1, advisorNames.Length);

            int currentIndex = hasOverride ? 1 + (int)overridden : 0;
            int pickedIndex = EditorGUILayout.Popup(currentIndex, options, GUILayout.Width(170f));

            if (pickedIndex != currentIndex)
            {
                if (pickedIndex == 0) advisorActionOverrides.Remove(actionClass);
                else advisorActionOverrides[actionClass] = (AdvisorType)(pickedIndex - 1);
                advisorsDirty = true;
            }

            GUILayout.Label(new GUIContent("Bonus",
                "Flat score adjustment added every time the AI scores this action. Use it to prioritize one action over its advisor's other cards: +2 wins most ties, +10 dominates, negative = last resort."),
                EditorStyles.miniLabel, GUILayout.Width(38f));
            float pickedBonus = EditorGUILayout.FloatField(bonus, GUILayout.Width(40f));
            if (!Mathf.Approximately(pickedBonus, bonus))
            {
                if (Mathf.Approximately(pickedBonus, 0f)) advisorActionBonuses.Remove(actionClass);
                else advisorActionBonuses[actionClass] = pickedBonus;
                advisorsDirty = true;
            }

            if (hasOverride)
            {
                GUILayout.Label("overridden", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            }

            GUILayout.Space(8f);
            GUILayout.Label(DescribeCardUsage(cards), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (nowExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(48f);
                EditorGUILayout.BeginVertical();

                DrawScoreTermToggles(actionClass);

                float effectiveBonus = advisorActionBonuses.TryGetValue(actionClass, out float eb) ? eb : 0f;
                if (usedByCards)
                {
                    var scored = cards
                        .Select(c =>
                        {
                            float score = SimulateScore(actionClass, resolvedAdvisor, c.difficulty, c.goldCost, effectiveBonus, out string parts);
                            return (card: c, score, parts);
                        })
                        .OrderByDescending(t => t.score)
                        .ToList();

                    foreach ((CardUsage card, float score, string parts) in scored)
                    {
                        string cost = card.goldCost > 0 ? $", {card.goldCost} gold" : string.Empty;
                        string effect = string.IsNullOrWhiteSpace(card.effect) ? "(no effect text)" : card.effect;
                        EditorGUILayout.LabelField($"• {score:0.0} — {card.cardName} (difficulty {card.difficulty}{cost}) — {effect}", EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.LabelField($"        {parts}", EditorStyles.wordWrappedMiniLabel);
                    }
                }
                else
                {
                    float score = SimulateScore(actionClass, resolvedAdvisor, 0, 0, effectiveBonus, out string parts);
                    EditorGUILayout.LabelField($"• no cards currently use this action (a difficulty-0, free card would score {score:0.0}: {parts})", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);
            }
        }
    }

    private IEnumerable<(string actionClass, AdvisorType defaultAdvisor)> SortedActionCatalog()
    {
        AdvisorType Resolved((string actionClass, AdvisorType defaultAdvisor) entry) =>
            advisorActionOverrides.TryGetValue(entry.actionClass, out AdvisorType o) ? o : entry.defaultAdvisor;
        int CardCount((string actionClass, AdvisorType defaultAdvisor) entry) =>
            cardsByActionRef.TryGetValue(entry.actionClass, out List<CardUsage> c) ? c.Count : 0;
        float Score((string actionClass, AdvisorType defaultAdvisor) entry)
        {
            cardsByActionRef.TryGetValue(entry.actionClass, out List<CardUsage> cards);
            float bonus = advisorActionBonuses.TryGetValue(entry.actionClass, out float b) ? b : 0f;
            return BestScoreForAction(entry.actionClass, Resolved(entry), cards, bonus);
        }

        return actionSortMode switch
        {
            // By advisor: real advisors first (enum order), Not advised last.
            1 => actionCatalog
                .OrderBy(e => Resolved(e) == AdvisorType.None ? int.MaxValue : (int)Resolved(e))
                .ThenBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase),
            2 => actionCatalog.OrderBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase),
            3 => actionCatalog.OrderByDescending(CardCount).ThenBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase),
            // By score: exactly the order an advisor (or Best Available Action) would prefer them.
            _ => actionCatalog.OrderByDescending(Score).ThenBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase)
        };
    }

    // Scenario assumptions for the score preview: every value the real scoring
    // reads from the board at runtime becomes an editable input here.
    private void DrawScenarioInputs()
    {
        EditorGUILayout.LabelField("Score preview scenario — set the unknowns; every card below shows its exact score", EditorStyles.boldLabel);

        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 78f;

        EditorGUILayout.BeginHorizontal();
        simCommander = EditorGUILayout.IntField("Commander", simCommander, GUILayout.Width(130f));
        simAgent = EditorGUILayout.IntField("Agent", simAgent, GUILayout.Width(130f));
        simEmissary = EditorGUILayout.IntField("Emissary", simEmissary, GUILayout.Width(130f));
        simMage = EditorGUILayout.IntField("Mage", simMage, GUILayout.Width(130f));
        simArtifactsCarried = EditorGUILayout.IntField("Artifacts", simArtifactsCarried, GUILayout.Width(130f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        simLeadingArmy = GUILayout.Toggle(simLeadingArmy, "Leading an army", GUILayout.Width(120f));
        simMovementPriority = GUILayout.Toggle(simMovementPriority, new GUIContent("Movement priority", "No threats around and the character has somewhere to go."), GUILayout.Width(130f));
        simHostageToRescue = GUILayout.Toggle(simHostageToRescue, new GUIContent("Hostage to rescue", "A friendly character is held captive nearby. Off = rescue actions (Free Character) get 0 situation points."), GUILayout.Width(130f));
        simHoldingHostage = GUILayout.Toggle(simHoldingHostage, new GUIContent("Holding hostage", "This character holds a captive. Off = hostage actions (Ask Ransom, Release Character) get 0 situation points."), GUILayout.Width(120f));

        GUILayout.Label(new GUIContent($"Outmatched: {(SimulatedOutmatched() ? "Yes" : "No")}",
            "Derived, not chosen: outmatched = leading an army AND my army strength < enemy army strength. Not leading an army = never outmatched."),
            EditorStyles.miniBoldLabel, GUILayout.Width(110f));

        EconomyStatus economy = SimulatedEconomyStatus();
        GUILayout.Label(new GUIContent($"Economy: {economy}", EconomyThresholdsTooltip()), EditorStyles.miniBoldLabel, GUILayout.Width(140f));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        simGoldBuffer = EditorGUILayout.IntField("Gold", simGoldBuffer, GUILayout.Width(130f));
        simGoldPerTurn = EditorGUILayout.IntField("Gold/turn", simGoldPerTurn, GUILayout.Width(130f));
        using (new EditorGUI.DisabledScope(!simLeadingArmy))
        {
            int shownStrength = simLeadingArmy ? simMyArmyStrength : 0;
            int pickedStrength = EditorGUILayout.IntField(new GUIContent("My army", "Army offence strength. 0 while not leading an army."), shownStrength, GUILayout.Width(130f));
            if (simLeadingArmy) simMyArmyStrength = pickedStrength;
        }
        simEnemyStrength = EditorGUILayout.IntField("Enemy army", simEnemyStrength, GUILayout.Width(130f));
        simArtifactShare = EditorGUILayout.Slider(new GUIContent("Artifacts %", "Share of the world's artifacts the nation already owns (0..1)."), simArtifactShare, 0f, 1f, GUILayout.Width(240f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 118f;
        EditorGUILayout.BeginHorizontal();
        simEnemyDistance = EditorGUILayout.FloatField(new GUIContent("Enemy PC/Army dist", "Hexes to the nearest enemy PC or army. Use 99 for none."), simEnemyDistance, GUILayout.Width(170f));
        simEnemyCharacterDistance = EditorGUILayout.FloatField(new GUIContent("Enemy char dist", "Hexes to the nearest enemy character. Use 99 for none."), simEnemyCharacterDistance, GUILayout.Width(170f));
        simNpcDistance = EditorGUILayout.FloatField(new GUIContent("NPC dist", "Hexes to the nearest unrevealed NPC. Use 99 for none."), simNpcDistance, GUILayout.Width(170f));
        simDestinationDistance = EditorGUILayout.FloatField(new GUIContent("Destination dist", "Hexes to the preferred movement destination."), simDestinationDistance, GUILayout.Width(170f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = previousLabelWidth;
    }

    // Checkboxes deciding which formula terms count for this action's score.
    private void DrawScoreTermToggles(string actionClass)
    {
        ActionScoreFlags flags = advisorActionFlags.TryGetValue(actionClass, out ActionScoreFlags f) ? f : default;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Score counts:", "Untick a term to leave it out of this action's score — in the preview below AND in the real game (after Save)."), EditorStyles.miniBoldLabel, GUILayout.Width(85f));

        bool useDifficulty = GUILayout.Toggle(!flags.ignoreDifficulty, new GUIContent("Difficulty", "Subtract the card's difficulty penalty."), GUILayout.Width(85f));
        bool useGoldCost = GUILayout.Toggle(!flags.ignoreGoldCost, new GUIContent("Gold cost", "Subtract the gold-cost pressure."), GUILayout.Width(85f));
        bool useSkills = GUILayout.Toggle(!flags.ignoreSkills, new GUIContent("Skills", "Add the character-skill affinity for the advisor."), GUILayout.Width(70f));
        bool useSituation = GUILayout.Toggle(!flags.ignoreSituation, new GUIContent("Situation", "Add the advisor's situational bonuses (economy, distances, ...)."), GUILayout.Width(85f));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        ActionScoreFlags picked = new()
        {
            ignoreDifficulty = !useDifficulty,
            ignoreGoldCost = !useGoldCost,
            ignoreSkills = !useSkills,
            ignoreSituation = !useSituation
        };

        if (!picked.Equals(flags))
        {
            if (picked.AnySet) advisorActionFlags[actionClass] = picked;
            else advisorActionFlags.Remove(actionClass);
            advisorsDirty = true;
        }
    }

    // Derived, mirroring GetMilitaryEdgeScore: only an army commander whose
    // army is weaker than the enemy's counts as outmatched.
    private bool SimulatedOutmatched()
        => simLeadingArmy && simMyArmyStrength < simEnemyStrength;

    // Hostage-dependent actions lose their situation term when the scenario
    // has no matching hostage (in game they would not be playable at all).
    private bool SituationHostageGatedOff(string actionClass)
    {
        if (string.Equals(actionClass, "FreeCharacter", StringComparison.OrdinalIgnoreCase))
            return !simHostageToRescue;
        if (string.Equals(actionClass, "AskRansom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionClass, "ReleaseCharacter", StringComparison.OrdinalIgnoreCase))
            return !simHoldingHostage;
        return false;
    }

    // Economy status derived from the scenario's Gold + Gold/turn, using the
    // CURRENT (possibly unsaved) threshold weights — mirrors AIAdvisorConfig.EvaluateEconomyStatus.
    private EconomyStatus SimulatedEconomyStatus()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);

        if (simGoldPerTurn < W(AIAdvisorConfig.Keys.EconomyCriticalIncomeBelow)
            || simGoldBuffer < W(AIAdvisorConfig.Keys.EconomyCriticalGoldBelow)) return EconomyStatus.Critical;
        if (simGoldPerTurn <= W(AIAdvisorConfig.Keys.EconomyWeakIncomeAtMost)
            || simGoldBuffer < W(AIAdvisorConfig.Keys.EconomyWeakGoldBelow)) return EconomyStatus.Weak;
        if (simGoldPerTurn <= W(AIAdvisorConfig.Keys.EconomyStableIncomeAtMost)) return EconomyStatus.Stable;
        return EconomyStatus.Surplus;
    }

    private string EconomyThresholdsTooltip()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);

        return "Derived from Gold and Gold/turn — not chosen by hand. In game, Gold/turn is the sum of the leader's PC city sizes.\n"
            + $"Critical: income < {W(AIAdvisorConfig.Keys.EconomyCriticalIncomeBelow):0.#} or gold < {W(AIAdvisorConfig.Keys.EconomyCriticalGoldBelow):0.#}\n"
            + $"Weak: income ≤ {W(AIAdvisorConfig.Keys.EconomyWeakIncomeAtMost):0.#} or gold < {W(AIAdvisorConfig.Keys.EconomyWeakGoldBelow):0.#}\n"
            + $"Stable: income ≤ {W(AIAdvisorConfig.Keys.EconomyStableIncomeAtMost):0.#}\n"
            + "Surplus: anything above.\n"
            + "Edit these thresholds in the 'Economy' group of the scoring weights.";
    }

    private float BestScoreForAction(string actionClass, AdvisorType advisor, List<CardUsage> cards, float bonus)
    {
        if (cards == null || cards.Count == 0)
        {
            return SimulateScore(actionClass, advisor, 0, 0, bonus, out _);
        }
        return cards.Max(c => SimulateScore(actionClass, advisor, c.difficulty, c.goldCost, bonus, out _));
    }

    // Exact mirror of AIContext.ScoreAction under the scenario assumptions,
    // using the CURRENT (possibly unsaved) weight values. Artifact-transfer
    // value is the one term not simulated (needs concrete artifacts/targets).
    private float SimulateScore(string actionClass, AdvisorType advisor, int difficulty, int goldCost, float bonus, out string breakdown)
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);

        ActionScoreFlags flags = advisorActionFlags.TryGetValue(actionClass, out ActionScoreFlags fl) ? fl : default;

        float baseScore = W(AIAdvisorConfig.Keys.BaseScore);
        float difficultyPenalty = flags.ignoreDifficulty
            ? 0f
            : Mathf.Clamp(
                difficulty / Mathf.Max(1f, W(AIAdvisorConfig.Keys.DifficultyDivisor)),
                0f,
                W(AIAdvisorConfig.Keys.MaxDifficultyPenalty));

        EconomyStatus simEconomy = SimulatedEconomyStatus();
        bool economyPoor = simEconomy == EconomyStatus.Critical || simEconomy == EconomyStatus.Weak;
        float costPenalty = 0f;
        if (goldCost > 0 && !flags.ignoreGoldCost)
        {
            float pressureFactor = economyPoor ? W(AIAdvisorConfig.Keys.CostPressureWhenPoor) : 1f;
            float bufferFactor = Mathf.Max(1f, (simGoldBuffer + Mathf.Max(0, simGoldPerTurn * 2)) / 10f);
            costPenalty = goldCost / bufferFactor * pressureFactor;
        }

        float affinity = flags.ignoreSkills ? 0f : advisor switch
        {
            AdvisorType.Militaristic => simCommander * W(AIAdvisorConfig.Keys.MilitaristicPerCommanderLevel)
                + (simLeadingArmy ? W(AIAdvisorConfig.Keys.MilitaristicLeadingArmyBonus) : 0f),
            AdvisorType.Economic => simEmissary * W(AIAdvisorConfig.Keys.EconomicPerEmissaryLevel)
                + simCommander * W(AIAdvisorConfig.Keys.EconomicPerCommanderLevel),
            AdvisorType.Diplomatic => simEmissary * W(AIAdvisorConfig.Keys.DiplomaticPerEmissaryLevel),
            AdvisorType.Intelligence => simAgent * W(AIAdvisorConfig.Keys.IntelligencePerAgentLevel),
            AdvisorType.Magic => simMage * W(AIAdvisorConfig.Keys.MagicPerMageLevel)
                + simArtifactsCarried * W(AIAdvisorConfig.Keys.MagicPerArtifactCarried),
            AdvisorType.Movement => simCommander * W(AIAdvisorConfig.Keys.MovementPerCommanderLevel)
                + simAgent * W(AIAdvisorConfig.Keys.MovementPerAgentLevel)
                + simEmissary * W(AIAdvisorConfig.Keys.MovementPerEmissaryLevel),
            _ => 0f
        };

        float enemyProximity = Mathf.Max(0f, W(AIAdvisorConfig.Keys.EnemyProximityMax) - simEnemyDistance);

        float situational = 0f;
        List<string> situationParts = new();
        void AddSituation(float value, string label)
        {
            situational += value;
            if (!Mathf.Approximately(value, 0f)) situationParts.Add($"{label} {(value > 0 ? "+" : "−")}{Mathf.Abs(value):0.#}");
        }

        // Hostage-dependent actions are not even offered in game without their
        // hostage, so grant them no situation points in that scenario.
        bool hostageGatedOff = SituationHostageGatedOff(actionClass);
        if (hostageGatedOff) situationParts.Add("no hostages, 0");

        switch (flags.ignoreSituation || hostageGatedOff ? AdvisorType.None : advisor)
        {
            case AdvisorType.Economic:
                AddSituation(simEconomy switch
                {
                    EconomyStatus.Critical => W(AIAdvisorConfig.Keys.EconomyCriticalBonus),
                    EconomyStatus.Weak => W(AIAdvisorConfig.Keys.EconomyWeakBonus),
                    EconomyStatus.Stable => W(AIAdvisorConfig.Keys.EconomyStableBonus),
                    _ => 0f
                }, $"economy {simEconomy}");
                break;
            case AdvisorType.Militaristic:
                AddSituation(enemyProximity, "enemy near");
                if (!simLeadingArmy)
                {
                    AddSituation(W(AIAdvisorConfig.Keys.NoArmyPenalty), "no army");
                }
                else if (simEnemyStrength > 0)
                {
                    float strengthDiff = simMyArmyStrength - simEnemyStrength;
                    float farPenalty = simEnemyDistance > 1f ? W(AIAdvisorConfig.Keys.FarTargetPenalty) : 0f;
                    AddSituation(strengthDiff < 0
                        ? Mathf.Max(-10f, strengthDiff / 10f - farPenalty)
                        : Mathf.Clamp(strengthDiff / 20f, -5f, 8f) - farPenalty, "army edge");
                }
                break;
            case AdvisorType.Intelligence:
                if (economyPoor) AddSituation(W(AIAdvisorConfig.Keys.IntelligencePoorEconomyBonus), "economy poor");
                if (SimulatedOutmatched()) AddSituation(W(AIAdvisorConfig.Keys.IntelligenceOutmatchedBonus), "outmatched");
                if (string.Equals(actionClass, "ScoutArea", StringComparison.OrdinalIgnoreCase))
                    AddSituation(W(AIAdvisorConfig.Keys.ScoutAreaBonus), "Scout Area");
                AddSituation(Mathf.Max(0f, W(AIAdvisorConfig.Keys.EnemyCharacterProximityMax) - simEnemyCharacterDistance), "enemy char near");
                AddSituation(enemyProximity, "enemy near");
                break;
            case AdvisorType.Magic:
                AddSituation((1f - Mathf.Clamp01(simArtifactShare)) * W(AIAdvisorConfig.Keys.ArtifactScarcityWeight), "artifact scarcity");
                AddSituation(enemyProximity, "enemy near");
                break;
            case AdvisorType.Diplomatic:
                AddSituation(Mathf.Max(0f, W(AIAdvisorConfig.Keys.NpcProximityMax) - simNpcDistance), "NPC near");
                if (SimulatedOutmatched()) AddSituation(W(AIAdvisorConfig.Keys.DiplomaticOutmatchedBonus), "outmatched");
                AddSituation(enemyProximity, "enemy near");
                break;
            case AdvisorType.Movement:
                if (simMovementPriority) AddSituation(W(AIAdvisorConfig.Keys.MovementPriorityBonus), "movement priority");
                AddSituation(Mathf.Max(0f, W(AIAdvisorConfig.Keys.MovementProximityMax)
                    - simDestinationDistance * W(AIAdvisorConfig.Keys.MovementDistancePenaltyPerHex)), "destination near");
                break;
        }

        float total = baseScore - difficultyPenalty - costPenalty + affinity + situational + bonus;

        string situationDetail = situationParts.Count > 0 ? $" [{string.Join(", ", situationParts)}]" : string.Empty;

        List<string> parts = new() { $"{baseScore:0.#} base" };
        parts.Add(flags.ignoreDifficulty ? "(difficulty off)" : $"− {difficultyPenalty:0.#} difficulty");
        parts.Add(flags.ignoreGoldCost ? "(gold cost off)" : $"− {costPenalty:0.#} gold cost");
        parts.Add(flags.ignoreSkills ? "(skills off)" : $"+ {affinity:0.#} skills");
        parts.Add(flags.ignoreSituation ? "(situation off)" : $"+ {situational:0.#} situation{situationDetail}");
        if (!Mathf.Approximately(bonus, 0f))
        {
            parts.Add($"{(bonus > 0 ? "+" : "−")} {Mathf.Abs(bonus):0.#} bonus");
        }
        breakdown = string.Join(" ", parts) + $" = {total:0.0}";
        return total;
    }

    private static string DescribeCardUsage(List<CardUsage> cards)
    {
        if (cards == null || cards.Count == 0) return "no cards use this action";

        const int maxShown = 3;
        string shown = string.Join(", ", cards.Take(maxShown).Select(c => c.cardName));
        return cards.Count <= maxShown
            ? $"{cards.Count} card{(cards.Count == 1 ? "" : "s")}: {shown}"
            : $"{cards.Count} cards: {shown} (+{cards.Count - maxShown} more)";
    }

    // Scans the deck JSONs so each action row can show which cards trigger it
    // and what those cards do.
    private static Dictionary<string, List<CardUsage>> BuildCardUsageMap()
    {
        Dictionary<string, Dictionary<string, CardUsage>> byAction = new(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> deckFiles = Directory
            .GetFiles("Assets/Resources/Cards/Modular", "*.json")
            .Concat(new[] { "Assets/Resources/Cards/EncounterDeck.json" })
            .Where(File.Exists)
            .Where(path => !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));

        foreach (string path in deckFiles)
        {
            DeckData deck = null;
            try { deck = JsonUtility.FromJson<DeckData>(File.ReadAllText(path)); }
            catch { /* not a deck file — skip */ }
            if (deck?.cards == null) continue;

            foreach (CardData card in deck.cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.name)) continue;
                string actionRef = card.GetActionRef();
                if (string.IsNullOrWhiteSpace(actionRef)) continue;

                actionRef = actionRef.Trim();
                if (actionRef.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    actionRef = actionRef.Substring(0, actionRef.Length - 3).Trim();
                }

                if (!byAction.TryGetValue(actionRef, out Dictionary<string, CardUsage> set))
                {
                    set = new Dictionary<string, CardUsage>(StringComparer.OrdinalIgnoreCase);
                    byAction[actionRef] = set;
                }

                if (!set.ContainsKey(card.name))
                {
                    string effect = card.GetActionEffectText();
                    set[card.name] = new CardUsage
                    {
                        cardName = card.name,
                        effect = string.IsNullOrWhiteSpace(effect) ? string.Empty : Regex.Replace(effect, "<[^>]+>", string.Empty).Trim(),
                        difficulty = Mathf.Max(0, card.difficulty),
                        goldCost = Mathf.Max(0, card.goldRequired)
                    };
                }
            }
        }

        return byAction.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Values.OrderBy(c => c.cardName, StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    // All concrete CharacterAction classes with the advisor coded on them.
    // Instances are created only to read DefaultAdvisorType (plain C# classes).
    private static List<(string, AdvisorType)> BuildActionCatalog()
    {
        List<(string, AdvisorType)> catalog = new();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<CharacterAction>())
        {
            if (type == null || type.IsAbstract) continue;

            AdvisorType defaultAdvisor = AdvisorType.None;
            try
            {
                if (Activator.CreateInstance(type) is CharacterAction instance)
                {
                    defaultAdvisor = instance.GetAdvisorType();
                }
            }
            catch
            {
                // No parameterless constructor or a throwing initializer — list it with None.
            }

            catalog.Add((type.Name, defaultAdvisor));
        }

        return catalog.OrderBy(entry => entry.Item2).ThenBy(entry => entry.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void LoadAdvisorConfig()
    {
        advisorWeights.Clear();
        advisorActionOverrides.Clear();
        advisorActionBonuses.Clear();
        advisorActionFlags.Clear();
        foreach (AdvisorWeightDefinition definition in AIAdvisorConfig.KnownWeights)
        {
            advisorWeights[definition.key] = definition.defaultValue;
        }

        if (File.Exists(AdvisorAssetPath))
        {
            try
            {
                AdvisorConfigData data = JsonUtility.FromJson<AdvisorConfigData>(File.ReadAllText(AdvisorAssetPath));
                if (data?.weights != null)
                {
                    foreach (AdvisorWeightEntry entry in data.weights)
                    {
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.key) && advisorWeights.ContainsKey(entry.key))
                        {
                            advisorWeights[entry.key] = entry.value;
                        }
                    }
                }
                if (data?.actionOverrides != null)
                {
                    foreach (AdvisorActionOverride entry in data.actionOverrides)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.actionClass)) continue;
                        if (!string.IsNullOrWhiteSpace(entry.advisor)
                            && Enum.TryParse(entry.advisor, true, out AdvisorType advisor))
                        {
                            advisorActionOverrides[entry.actionClass] = advisor;
                        }
                        if (!Mathf.Approximately(entry.scoreBonus, 0f))
                        {
                            advisorActionBonuses[entry.actionClass] = entry.scoreBonus;
                        }
                        ActionScoreFlags flags = new()
                        {
                            ignoreDifficulty = entry.ignoreDifficulty,
                            ignoreGoldCost = entry.ignoreGoldCost,
                            ignoreSkills = entry.ignoreSkills,
                            ignoreSituation = entry.ignoreSituation
                        };
                        if (flags.AnySet)
                        {
                            advisorActionFlags[entry.actionClass] = flags;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AIWidget: could not parse {AdvisorAssetPath} — starting from defaults. {e.Message}");
            }
        }

        advisorsDirty = false;
    }

    private void SaveAdvisorConfig()
    {
        AdvisorConfigData data = new()
        {
            weights = AIAdvisorConfig.KnownWeights
                .Select(d => new AdvisorWeightEntry
                {
                    key = d.key,
                    value = advisorWeights.TryGetValue(d.key, out float v) ? v : d.defaultValue
                })
                .ToList(),
            actionOverrides = advisorActionOverrides.Keys
                .Union(advisorActionBonuses.Keys, StringComparer.OrdinalIgnoreCase)
                .Union(advisorActionFlags.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name =>
                {
                    ActionScoreFlags flags = advisorActionFlags.TryGetValue(name, out ActionScoreFlags f) ? f : default;
                    return new AdvisorActionOverride
                    {
                        actionClass = name,
                        advisor = advisorActionOverrides.TryGetValue(name, out AdvisorType advisor) ? advisor.ToString() : string.Empty,
                        scoreBonus = advisorActionBonuses.TryGetValue(name, out float bonus) ? bonus : 0f,
                        ignoreDifficulty = flags.ignoreDifficulty,
                        ignoreGoldCost = flags.ignoreGoldCost,
                        ignoreSkills = flags.ignoreSkills,
                        ignoreSituation = flags.ignoreSituation
                    };
                })
                .ToList()
        };

        WriteJsonAsset(AdvisorAssetPath, JsonUtility.ToJson(data, true));
        AIAdvisorConfig.Reload();
        advisorsDirty = false;
        Debug.Log($"AIWidget: saved advisor config to {AdvisorAssetPath}");
    }

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------

    private static void WriteJsonAsset(string assetPath, string json)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(assetPath, json);
        AssetDatabase.ImportAsset(assetPath);
    }
}
