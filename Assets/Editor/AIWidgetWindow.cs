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
        Strategies = 1,
        Advisors = 2,
        NN = 3
    }

    private static readonly string[] TabLabels = { "Situations", "HTN", "Advisors", "NN" };

    private const string PriorityAssetPath = "Assets/Resources/" + SituationEvaluator.PriorityResourcePath + ".json";
    private const string StrategiesAssetPath = "Assets/Resources/" + AIStrategyLibrary.ResourcePath + ".json";
    private const string AdvisorAssetPath = "Assets/Resources/" + AIAdvisorConfig.ResourcePath + ".json";

    private Tab currentTab = Tab.Situations;

    // Situations tab
    private Vector2 situationsScroll;
    private List<CardSituationEnum> situationOrder = new();
    private ReorderableList situationList;
    private bool orderDirty;

    // Strategies tab
    private HTNStrategyLibraryData strategyLibrary = new();
    private int selectedStrategyIndex;
    private Vector2 strategiesScroll;
    private bool strategiesDirty;

    private static readonly Color RowHighlightColor = new(0.26f, 0.53f, 0.96f, 0.22f);

    // Advisor-profile readability styles (weight rows, condition rows, the HTN scorecard).
    // Lazily built in EnsureAdvisorStyles — EditorStyles isn't safe to touch before OnGUI runs.
    private static readonly Color BadgeTrueColor = new(0.20f, 0.47f, 0.24f);
    private static readonly Color BadgeFalseColor = new(0.32f, 0.32f, 0.32f);
    private static readonly Color WeightRowAltColor = new(1f, 1f, 1f, 0.03f);
    private GUIStyle weightRowBoxStyle;
    private GUIStyle weightLabelStyle;
    private GUIStyle weightDescStyle;
    private GUIStyle conditionKeyStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle metricValueStyle;
    private GUIStyle metricCaptionStyle;
    private GUIStyle badgeStyle;
    private GUIStyle compactBadgeStyle;

    private void EnsureAdvisorStyles()
    {
        if (weightRowBoxStyle != null) return;
        weightRowBoxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 0, 4) };
        weightLabelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        weightDescStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { wordWrap = true };
        conditionKeyStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, margin = new RectOffset(0, 0, 6, 4) };
        metricValueStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        metricCaptionStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        badgeStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        compactBadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
    }

    // Live TRUE/FALSE for any HTNRegistry predicate key, under the current "Live effect"
    // scenario inputs — the same signal HTNRegistry's own lambda would return at runtime,
    // computed via the widget's Simulated* mirrors since there's no live AIContext here.
    // This is what lets every condition shown in the widget (not just the one currently
    // active tier) carry its own live state, instead of the reader having to infer the other
    // three tiers are false from the one tier that's shown true.
    private bool EvaluatePredicateLive(string key)
    {
        if (string.Equals(key, "Global.Always", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(key, "Global.Never", StringComparison.OrdinalIgnoreCase)) return false;

        EconomyStatus tier = SimulatedEconomyStatus();
        switch (key)
        {
            case "Economic.NeedsHelp": return tier is EconomyStatus.Critical or EconomyStatus.Weak;
            case "Economic.Critical": return tier == EconomyStatus.Critical;
            case "Economic.Weak": return tier == EconomyStatus.Weak;
            case "Economic.Stable": return tier == EconomyStatus.Stable;
            case "Economic.Surplus": return tier == EconomyStatus.Surplus;
            case "Militaristic.EnemyNear": return SimulatedEnemyNear();
            case "Militaristic.Danger": return SimulatedDanger();
        }

        // Every remaining predicate follows the "<Advisor>.Viable" pattern.
        string advisorPart = key.Contains('.') ? key.Split('.')[0] : key;
        if (Enum.TryParse(advisorPart, out AdvisorType advisor))
        {
            string thresholdKey = ViabilityThresholdKeyFor(advisor);
            if (thresholdKey != null)
            {
                float threshold = advisorWeights.TryGetValue(thresholdKey, out float t) ? t : AIAdvisorConfig.GetDefaultWeight(thresholdKey);
                return SimulateViability(advisor) > threshold;
            }
        }
        return false;
    }

    // Compact colored pill sized to its own text — for packing several conditions into one row.
    private void DrawConditionBadge(string label, bool value)
    {
        GUIContent content = new GUIContent(label);
        Vector2 size = compactBadgeStyle.CalcSize(content);
        Rect r = GUILayoutUtility.GetRect(size.x + 16f, 22f, GUILayout.Width(size.x + 16f));
        EditorGUI.DrawRect(r, value ? BadgeTrueColor : BadgeFalseColor);
        GUI.Label(r, label, compactBadgeStyle);
        GUILayout.Space(6f);
    }

    // Advisors tab
    private Vector2 advisorsScroll;
    private readonly Dictionary<string, float> advisorWeights = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AdvisorType> advisorActionOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> advisorActionBonuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionScoreFlags> advisorActionFlags = new(StringComparer.OrdinalIgnoreCase);
    private List<(string actionClass, AdvisorType defaultAdvisor)> actionCatalog;
    private Dictionary<string, List<CardUsage>> cardsByActionRef;
    private readonly HashSet<string> expandedActions = new(StringComparer.OrdinalIgnoreCase);
    private bool advisorsDirty;
    private string actionSearch = string.Empty;
    private int simBiasedAdvisorIndex; // index into Enum.GetNames(typeof(AdvisorType)); 0 = None

    // Score-preview scenario: user-set assumptions for everything the real
    // scoring reads from the board at runtime.
    private int simCommander = 2;
    private int simAgent = 2;
    private int simEmissary = 2;
    private int simMage = 2;
    private int simArtifactsCarried;
    private bool simLeadingArmy;
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

    private static readonly string[] NodeTypeLabels = { "CompoundTask", "Method", "PrimitiveTask" };
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
        LoadStrategyLibrary();
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
            case Tab.Strategies:
                DrawStrategiesTab();
                break;
            case Tab.Advisors:
                DrawAdvisorsTab();
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
        EditorGUILayout.LabelField("Situation ranking", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag to reorder. This ranking feeds a score bonus for player Opportunity Cards (earlier = bigger bonus) "
            + "when a card's own situation tag matches one that's currently active — it no longer gates or caps which "
            + "cards can be offered, scoring across the whole eligible pool decides that.\n"
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
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Ranking (top = biggest score bonus)"),
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
        Debug.Log($"AIWidget: saved situation ranking to {PriorityAssetPath}");
    }

    // ------------------------------------------------------------------
    // Strategies tab (HTN)
    // ------------------------------------------------------------------

    private void DrawStrategiesTab()
    {
        EditorGUILayout.LabelField("Strategies (HTN)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each AI leader persists a strategy across turns instead of recomputing it every turn. A CompoundTask's "
            + "Methods are tried top-down by row order (priority) — a Method higher in the list can always interrupt "
            + "an already-active lower one, even mid-sequence. A Method's subtasks run in order: each PrimitiveTask "
            + "biases card scoring toward its advisor (see Advisors tab) until its completion condition fires, then "
            + "execution advances to the next step in the sequence. Row order matters for BOTH priority (which Method "
            + "wins / can interrupt) and sequence (subtask execution order) — reordering rows is a behavior change.\n"
            + $"Saved to {StrategiesAssetPath} and read by AIStrategyLibrary at runtime.",
            MessageType.None);

        DrawStrategiesToolbar();
        EditorGUILayout.Space();
        DrawStrategyAssignments();
        EditorGUILayout.Space();

        if (strategyLibrary.strategies.Count == 0)
        {
            EditorGUILayout.HelpBox("No strategies defined. Click 'Add Strategy'.", MessageType.Warning);
            return;
        }

        selectedStrategyIndex = Mathf.Clamp(selectedStrategyIndex, 0, strategyLibrary.strategies.Count - 1);
        HTNStrategyData strategy = strategyLibrary.strategies[selectedStrategyIndex];

        foreach (string issue in ValidateStrategy(strategy))
        {
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }

        strategiesScroll = EditorGUILayout.BeginScrollView(strategiesScroll);
        DrawStrategyOutline(strategy);
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Add Node (root level)", GUILayout.Width(170f)))
        {
            strategy.nodes.Add(NewLeafNode());
            EnsureUniqueTaskIds(strategy.nodes);
            strategiesDirty = true;
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawStrategiesToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        string[] strategyIds = strategyLibrary.strategies.Select(s => s.strategyId).ToArray();
        if (strategyIds.Length > 0)
        {
            selectedStrategyIndex = EditorGUILayout.Popup(Mathf.Clamp(selectedStrategyIndex, 0, strategyIds.Length - 1), strategyIds, GUILayout.Width(180f));

            string currentId = strategyLibrary.strategies[selectedStrategyIndex].strategyId;
            string newId = EditorGUILayout.DelayedTextField(currentId, GUILayout.Width(160f));
            if (!string.Equals(newId, currentId, StringComparison.Ordinal)) RenameSelectedStrategy(newId);
        }

        if (GUILayout.Button("Add Strategy", GUILayout.Width(100f)))
        {
            HTNStrategyData strategy = new()
            {
                strategyId = MakeUniqueStrategyId("new_strategy"),
                nodes = new List<HTNNodeData>
                {
                    new() { depth = 0, type = "CompoundTask", taskId = "root" },
                    new() { depth = 1, type = "Method", precondition = "Global.Always", taskId = "root.fallback" },
                    new() { depth = 2, type = "PrimitiveTask", advisor = string.Empty, completionCondition = "Global.Never", taskId = "root.fallback.leaf" }
                }
            };
            strategyLibrary.strategies.Add(strategy);
            selectedStrategyIndex = strategyLibrary.strategies.Count - 1;
            strategiesDirty = true;
        }

        using (new EditorGUI.DisabledScope(strategyLibrary.strategies.Count == 0))
        {
            if (GUILayout.Button("Duplicate", GUILayout.Width(80f)))
            {
                HTNStrategyData source = strategyLibrary.strategies[selectedStrategyIndex];
                HTNStrategyData copy = JsonUtility.FromJson<HTNStrategyData>(JsonUtility.ToJson(source));
                copy.strategyId = MakeUniqueStrategyId(source.strategyId);
                strategyLibrary.strategies.Add(copy);
                selectedStrategyIndex = strategyLibrary.strategies.Count - 1;
                strategiesDirty = true;
            }

            using (new EditorGUI.DisabledScope(strategyLibrary.strategies.Count <= 1))
            {
                if (GUILayout.Button("Delete", GUILayout.Width(70f))
                    && EditorUtility.DisplayDialog("Delete strategy",
                        $"Delete strategy '{strategyLibrary.strategies[selectedStrategyIndex].strategyId}'?", "Delete", "Cancel"))
                {
                    string removedId = strategyLibrary.strategies[selectedStrategyIndex].strategyId;
                    strategyLibrary.strategies.RemoveAt(selectedStrategyIndex);
                    selectedStrategyIndex = Mathf.Clamp(selectedStrategyIndex, 0, strategyLibrary.strategies.Count - 1);
                    string fallbackId = strategyLibrary.strategies[selectedStrategyIndex].strategyId;
                    foreach (HTNStrategyAssignment assignment in strategyLibrary.assignments)
                    {
                        if (string.Equals(assignment.strategyId, removedId, StringComparison.OrdinalIgnoreCase))
                            assignment.strategyId = fallbackId;
                    }
                    strategiesDirty = true;
                }
            }
        }

        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!strategiesDirty))
        {
            if (GUILayout.Button("Save", GUILayout.Width(90f))) SaveStrategyLibrary();
            if (GUILayout.Button("Revert", GUILayout.Width(90f))) LoadStrategyLibrary();
        }
        if (strategiesDirty) GUILayout.Label("Unsaved changes", EditorStyles.miniBoldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawStrategyAssignments()
    {
        EditorGUILayout.LabelField("Strategy per alignment", EditorStyles.boldLabel);

        string[] strategyIds = strategyLibrary.strategies.Select(s => s.strategyId).ToArray();
        if (strategyIds.Length == 0) return;

        foreach (string alignmentName in Enum.GetNames(typeof(AlignmentEnum)))
        {
            HTNStrategyAssignment assignment = strategyLibrary.assignments.FirstOrDefault(a =>
                string.Equals(a.alignment, alignmentName, StringComparison.OrdinalIgnoreCase));
            if (assignment == null)
            {
                assignment = new HTNStrategyAssignment { alignment = alignmentName, strategyId = strategyIds[0] };
                strategyLibrary.assignments.Add(assignment);
            }

            int current = Array.FindIndex(strategyIds, id => string.Equals(id, assignment.strategyId, StringComparison.OrdinalIgnoreCase));
            if (current < 0) current = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(alignmentName), GUILayout.Width(120f));
            int picked = EditorGUILayout.Popup(current, strategyIds, GUILayout.Width(180f));
            EditorGUILayout.EndHorizontal();

            if (picked != current || !string.Equals(assignment.strategyId, strategyIds[picked], StringComparison.Ordinal))
            {
                if (!string.Equals(assignment.strategyId, strategyIds[picked], StringComparison.Ordinal)) strategiesDirty = true;
                assignment.strategyId = strategyIds[picked];
            }
        }
    }

    private void DrawStrategyOutline(HTNStrategyData strategy)
    {
        List<HTNNodeData> nodes = strategy.nodes;
        int pendingOp = -1; // 0 up, 1 down, 2 outdent, 3 indent, 4 insert-after, 5 delete
        int pendingIndex = -1;
        int hoverEnd = -1; // exclusive end of the hovered subtree's row range
        string[] advisorOptions = Enum.GetNames(typeof(AdvisorType));

        for (int i = 0; i < nodes.Count; i++)
        {
            HTNNodeData node = nodes[i];
            if (node == null) continue;

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint && rowRect.Contains(Event.current.mousePosition))
            {
                hoverEnd = i + SubtreeCount(nodes, i);
            }
            if (i < hoverEnd)
            {
                EditorGUI.DrawRect(rowRect, RowHighlightColor);
            }
            GUILayout.Space(8f + node.depth * 24f);

            GUILayout.Label(GetStrategyRowConnective(nodes, i), EditorStyles.miniBoldLabel, GUILayout.Width(60f));

            int typeIndex = Mathf.Max(0, Array.IndexOf(NodeTypeLabels, node.type));
            int newTypeIndex = EditorGUILayout.Popup(typeIndex, NodeTypeLabels, GUILayout.Width(105f));
            if (newTypeIndex != typeIndex)
            {
                node.type = NodeTypeLabels[newTypeIndex];
                strategiesDirty = true;
            }

            string newTaskId = EditorGUILayout.DelayedTextField(node.taskId, GUILayout.Width(90f));
            if (!string.Equals(newTaskId, node.taskId, StringComparison.Ordinal))
            {
                node.taskId = newTaskId;
                strategiesDirty = true;
            }

            HTNNodeType nodeType = ParseNodeType(node.type);
            switch (nodeType)
            {
                case HTNNodeType.Method:
                {
                    string picked = DrawNamePopup(node.precondition, HTNRegistry.PredicateNames, 140f);
                    if (!string.Equals(picked, node.precondition, StringComparison.Ordinal)) { node.precondition = picked; strategiesDirty = true; }

                    int invertIndex = EditorGUILayout.Popup(node.invert ? 1 : 0, InvertLabels, GUILayout.Width(75f));
                    bool newInvert = invertIndex == 1;
                    if (newInvert != node.invert) { node.invert = newInvert; strategiesDirty = true; }
                    break;
                }
                case HTNNodeType.PrimitiveTask:
                {
                    string currentAdvisor = string.IsNullOrEmpty(node.advisor) ? nameof(AdvisorType.None) : node.advisor;
                    int advisorIndex = Mathf.Max(0, Array.IndexOf(advisorOptions, currentAdvisor));
                    int pickedAdvisorIndex = EditorGUILayout.Popup(advisorIndex, advisorOptions, GUILayout.Width(110f));
                    string pickedAdvisor = advisorOptions[pickedAdvisorIndex] == nameof(AdvisorType.None) ? string.Empty : advisorOptions[pickedAdvisorIndex];
                    if (!string.Equals(pickedAdvisor, node.advisor ?? string.Empty, StringComparison.Ordinal))
                    {
                        node.advisor = pickedAdvisor;
                        strategiesDirty = true;
                    }

                    GUILayout.Label("until", EditorStyles.miniLabel, GUILayout.Width(28f));
                    string pickedCompletion = DrawNamePopup(node.completionCondition, HTNRegistry.PredicateNames, 140f);
                    if (!string.Equals(pickedCompletion, node.completionCondition, StringComparison.Ordinal)) { node.completionCondition = pickedCompletion; strategiesDirty = true; }

                    int completionInvertIndex = EditorGUILayout.Popup(node.completionInvert ? 1 : 0, InvertLabels, GUILayout.Width(75f));
                    bool newCompletionInvert = completionInvertIndex == 1;
                    if (newCompletionInvert != node.completionInvert) { node.completionInvert = newCompletionInvert; strategiesDirty = true; }
                    break;
                }
                default:
                    GUILayout.Label("methods below, tried top-down (higher = can interrupt an active lower one):", EditorStyles.miniLabel);
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

            // Always-visible description line — never hide condition meaning behind a hover
            // state. Every name in the two dropdowns above comes from HTNRegistry.KnownPredicates,
            // which is required to carry a plain-English Description for exactly this reason.
            string conditionSummary = BuildConditionSummary(node);
            if (!string.IsNullOrEmpty(conditionSummary))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8f + node.depth * 24f + 60f);
                EditorGUILayout.LabelField(conditionSummary, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
            }
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
        EnsureUniqueTaskIds(nodes);
        strategiesDirty = true;
        GUI.FocusControl(null);
    }

    private static string DrawNamePopup(string current, IReadOnlyList<string> names, float width = 200f)
    {
        List<string> options = names.ToList();
        if (!string.IsNullOrEmpty(current) && !options.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(current + " (unknown)");
        }

        int index = options.FindIndex(o => string.Equals(o, current, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = string.IsNullOrEmpty(current) ? 0 : options.Count - 1;

        int picked = EditorGUILayout.Popup(index, options.ToArray(), GUILayout.Width(width));
        return picked < names.Count ? names[picked] : current;
    }

    private static HTNNodeType ParseNodeType(string type)
        => Enum.TryParse(type, true, out HTNNodeType parsed) ? parsed : HTNNodeType.PrimitiveTask;

    // The always-visible line under a Method/PrimitiveTask row explaining what its
    // precondition/completion condition actually means — sourced from
    // HTNRegistry.KnownPredicates.Description, never left as a bare unexplained name.
    private static string BuildConditionSummary(HTNNodeData node)
    {
        HTNNodeType type = ParseNodeType(node.type);
        string key = type switch
        {
            HTNNodeType.Method => node.precondition,
            HTNNodeType.PrimitiveTask => node.completionCondition,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(key)) return null;

        bool inverted = type == HTNNodeType.Method ? node.invert : node.completionInvert;
        string description = HTNRegistry.TryGetDescription(key, out string desc)
            ? desc
            : $"Unknown condition '{key}' — not in HTNRegistry.KnownPredicates.";
        string invertedNote = inverted ? " (inverted: met when the above is FALSE)" : string.Empty;
        string roleLabel = type == HTNNodeType.Method ? "Precondition" : "Completes when";
        return $"{roleLabel} '{key}'{invertedNote} — {description}";
    }

    // Pseudocode connective for a row, based on its parent's type and whether it is the
    // first sibling: a CompoundTask's Method children chain with TRY/OR ELSE (priority —
    // also the interrupt order, see HTNPlanner); a Method's subtask children chain with
    // DO/THEN (sequence — the order they execute in, advancing on completion).
    private static string GetStrategyRowConnective(List<HTNNodeData> nodes, int index)
    {
        HTNNodeType type = ParseNodeType(nodes[index].type);
        if (type == HTNNodeType.CompoundTask) return "TASK";

        HTNNodeType parentType = HTNNodeType.CompoundTask; // implicit: depth-0 rows are the strategy root
        for (int j = index - 1; j >= 0; j--)
        {
            if (nodes[j].depth < nodes[index].depth)
            {
                parentType = ParseNodeType(nodes[j].type);
                break;
            }
        }

        bool isFirstSibling = PrevSiblingIndex(nodes, index) < 0;
        if (parentType == HTNNodeType.CompoundTask)
        {
            return isFirstSibling ? "TRY" : "OR ELSE";
        }

        return isFirstSibling ? "DO" : "THEN";
    }

    private static HTNNodeData NewLeafNode(int depth = 0)
        => new() { depth = depth, type = HTNNodeType.PrimitiveTask.ToString(), advisor = string.Empty, completionCondition = "Never", taskId = string.Empty };

    // Fills in blank/duplicate taskIds after a structural edit. Never touches an
    // already-unique non-blank id — the Blackboard's persisted execution stack
    // references these by id across turns, so stable ids must not churn on reorder.
    private static void EnsureUniqueTaskIds(List<HTNNodeData> nodes)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (HTNNodeData node in nodes)
        {
            if (node == null) continue;
            bool blank = string.IsNullOrWhiteSpace(node.taskId);
            bool duplicate = !blank && !seen.Add(node.taskId);
            if (!blank && !duplicate) { seen.Add(node.taskId); continue; }

            string candidate;
            int suffix = seen.Count + 1;
            do { candidate = $"node_{suffix++}"; } while (!seen.Add(candidate));
            node.taskId = candidate;
        }
    }

    // ---- outline structure helpers (a subtree = a node plus the contiguous
    // run of following nodes with greater depth) — pure depth-int operations,
    // identical shape to the old Behaviour Tree outline's helpers. ----

    private static int SubtreeCount(List<HTNNodeData> nodes, int index)
    {
        int count = 1;
        int depth = nodes[index].depth;
        while (index + count < nodes.Count && nodes[index + count].depth > depth) count++;
        return count;
    }

    private static int PrevSiblingIndex(List<HTNNodeData> nodes, int index)
    {
        int depth = nodes[index].depth;
        for (int j = index - 1; j >= 0; j--)
        {
            if (nodes[j].depth < depth) return -1;
            if (nodes[j].depth == depth) return j;
        }
        return -1;
    }

    private static int NextSiblingIndex(List<HTNNodeData> nodes, int index)
    {
        int next = index + SubtreeCount(nodes, index);
        if (next < nodes.Count && nodes[next].depth == nodes[index].depth) return next;
        return -1;
    }

    private static void MoveSubtreeUp(List<HTNNodeData> nodes, int index)
    {
        int prev = PrevSiblingIndex(nodes, index);
        if (prev < 0) return;

        int count = SubtreeCount(nodes, index);
        List<HTNNodeData> block = nodes.GetRange(index, count);
        nodes.RemoveRange(index, count);
        nodes.InsertRange(prev, block);
    }

    private static void ShiftSubtreeDepth(List<HTNNodeData> nodes, int index, int delta)
    {
        if (delta > 0 && PrevSiblingIndex(nodes, index) < 0) return;
        if (delta < 0 && nodes[index].depth <= 0) return;

        int count = SubtreeCount(nodes, index);
        for (int j = index; j < index + count; j++)
        {
            nodes[j].depth += delta;
        }
    }

    private static List<string> ValidateStrategy(HTNStrategyData strategy)
    {
        List<string> issues = new();
        List<HTNNodeData> nodes = strategy.nodes;

        if (nodes.Count == 0)
        {
            issues.Add("Strategy has no nodes — the built-in default strategy will be used instead.");
            return issues;
        }

        if (nodes[0] == null || nodes[0].depth != 0)
        {
            issues.Add("Row 1 must be at root level (depth 0).");
        }
        else if (ParseNodeType(nodes[0].type) != HTNNodeType.CompoundTask)
        {
            issues.Add("Row 1 must be a CompoundTask (the strategy root).");
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            HTNNodeData node = nodes[i];
            if (node == null) continue;
            HTNNodeType type = ParseNodeType(node.type);

            int childCount = 0;
            for (int j = i + 1; j < nodes.Count && nodes[j] != null && nodes[j].depth > node.depth; j++)
            {
                if (nodes[j].depth == node.depth + 1) childCount++;
            }

            if (i > 0 && node.depth > nodes[i - 1].depth + 1)
                issues.Add($"Row {i + 1}: indented more than one level below the previous row — it will be skipped.");

            if (type == HTNNodeType.CompoundTask && childCount == 0)
                issues.Add($"Row {i + 1}: CompoundTask has no Methods — it will be skipped.");
            if (type == HTNNodeType.Method && childCount == 0)
                issues.Add($"Row {i + 1}: Method has no subtasks — it will be skipped.");
            if (type == HTNNodeType.PrimitiveTask && childCount > 0)
                issues.Add($"Row {i + 1}: PrimitiveTask is a leaf — rows indented under it will be ignored.");
            if (type == HTNNodeType.Method && !string.IsNullOrWhiteSpace(node.precondition) && !HTNRegistry.TryGetPredicate(node.precondition, out _))
                issues.Add($"Row {i + 1}: unknown precondition '{node.precondition}'.");
            if (type == HTNNodeType.PrimitiveTask && !string.IsNullOrWhiteSpace(node.completionCondition) && !HTNRegistry.TryGetPredicate(node.completionCondition, out _))
                issues.Add($"Row {i + 1}: unknown completion condition '{node.completionCondition}'.");

            if (type == HTNNodeType.PrimitiveTask
                && string.Equals(node.completionCondition, "Never", StringComparison.OrdinalIgnoreCase)
                && NextSiblingIndex(nodes, i) >= 0)
            {
                issues.Add($"Row {i + 1}: completion condition is 'Never', but it isn't the last step in its sequence — later rows will never be reached through normal advancement (only via interrupt).");
            }
        }

        foreach (IGrouping<string, HTNNodeData> group in nodes
                     .Where(n => n != null && !string.IsNullOrWhiteSpace(n.taskId))
                     .GroupBy(n => n.taskId, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            issues.Add($"taskId '{group.Key}' is used by {group.Count()} rows — must be unique within a strategy.");
        }

        return issues;
    }

    private string MakeUniqueStrategyId(string baseId)
    {
        string candidate = baseId;
        int suffix = 2;
        while (strategyLibrary.strategies.Any(s => string.Equals(s.strategyId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}_{suffix++}";
        }
        return candidate;
    }

    private void RenameSelectedStrategy(string newId)
    {
        newId = (newId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newId)) return;
        if (strategyLibrary.strategies.Any(s => string.Equals(s.strategyId, newId, StringComparison.OrdinalIgnoreCase))) return;

        string oldId = strategyLibrary.strategies[selectedStrategyIndex].strategyId;
        strategyLibrary.strategies[selectedStrategyIndex].strategyId = newId;
        foreach (HTNStrategyAssignment assignment in strategyLibrary.assignments)
        {
            if (string.Equals(assignment.strategyId, oldId, StringComparison.OrdinalIgnoreCase))
                assignment.strategyId = newId;
        }
        strategiesDirty = true;
    }

    private void LoadStrategyLibrary()
    {
        strategyLibrary = null;
        if (File.Exists(StrategiesAssetPath))
        {
            try { strategyLibrary = JsonUtility.FromJson<HTNStrategyLibraryData>(File.ReadAllText(StrategiesAssetPath)); }
            catch (Exception e)
            {
                Debug.LogWarning($"AIWidget: could not parse {StrategiesAssetPath} — starting from the built-in default strategy. {e.Message}");
            }
        }

        strategyLibrary ??= new HTNStrategyLibraryData();
        strategyLibrary.strategies ??= new List<HTNStrategyData>();
        strategyLibrary.assignments ??= new List<HTNStrategyAssignment>();
        strategyLibrary.strategies.RemoveAll(s => s == null);
        foreach (HTNStrategyData strategy in strategyLibrary.strategies)
        {
            strategy.nodes ??= new List<HTNNodeData>();
            strategy.nodes.RemoveAll(n => n == null);
            ClampDepths(strategy.nodes);
        }

        if (strategyLibrary.strategies.Count == 0)
        {
            strategyLibrary.strategies.Add(BuildDefaultStrategyData());
        }

        selectedStrategyIndex = Mathf.Clamp(selectedStrategyIndex, 0, strategyLibrary.strategies.Count - 1);
        strategiesDirty = false;
    }

    private static void ClampDepths(List<HTNNodeData> nodes)
    {
        int previousDepth = -1;
        foreach (HTNNodeData node in nodes)
        {
            node.depth = Mathf.Max(0, Mathf.Min(node.depth, previousDepth + 1));
            previousDepth = node.depth;
        }
    }

    // Data mirror of HTNStrategyBuilder.BuildDefault().
    private static HTNStrategyData BuildDefaultStrategyData()
    {
        return new HTNStrategyData
        {
            strategyId = AIStrategyLibrary.DefaultStrategyId,
            nodes = new List<HTNNodeData>
            {
                new() { depth = 0, type = "CompoundTask", taskId = "root" },
                new() { depth = 1, type = "Method", precondition = "Militaristic.Danger", taskId = "root.danger" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Militaristic", completionCondition = "Global.Never", taskId = "root.danger.leaf" },
                new() { depth = 1, type = "Method", precondition = "Economic.Critical", taskId = "root.recover" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Economic", completionCondition = "Economic.Weak", taskId = "root.recover.build" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Economic", completionCondition = "Economic.Stable", taskId = "root.recover.trade" },
                new() { depth = 1, type = "Method", precondition = "Militaristic.Viable", taskId = "root.offense" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.offense.pick" },
                new() { depth = 3, type = "Method", precondition = "Global.Always", taskId = "root.offense.pick.mil" },
                new() { depth = 4, type = "PrimitiveTask", advisor = "Militaristic", completionCondition = "Global.Never", taskId = "root.offense.pick.mil.leaf" },
                new() { depth = 1, type = "Method", precondition = "Movement.Viable", taskId = "root.movement" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Movement", completionCondition = "Global.Never", taskId = "root.movement.leaf" },
                new() { depth = 1, type = "Method", precondition = "Global.Always", taskId = "root.fallback" },
                new() { depth = 2, type = "PrimitiveTask", advisor = string.Empty, completionCondition = "Global.Never", taskId = "root.fallback.leaf" }
            }
        };
    }

    private void SaveStrategyLibrary()
    {
        WriteJsonAsset(StrategiesAssetPath, JsonUtility.ToJson(strategyLibrary, true));
        AIStrategyLibrary.Reload();
        strategiesDirty = false;
        Debug.Log($"AIWidget: saved strategies to {StrategiesAssetPath}");
    }

    // ------------------------------------------------------------------
    // Advisors tab
    // ------------------------------------------------------------------

    private void DrawAdvisorsTab()
    {
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!advisorsDirty))
        {
            if (GUILayout.Button("Save", GUILayout.Width(90f))) SaveAdvisorConfig();
            if (GUILayout.Button("Revert", GUILayout.Width(90f))) LoadAdvisorConfig();
        }
        if (advisorsDirty) GUILayout.Label("Unsaved changes", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        advisorsScroll = EditorGUILayout.BeginScrollView(advisorsScroll);
        DrawAdvisorsSection();
        EditorGUILayout.EndScrollView();
    }

    // "Shared" holds everything not specific to one advisor (base score, difficulty,
    // HTN bias bonus, Always/Never, ...). The rest match AdvisorType names exactly, so a
    // single string doubles as both the toolbar label and (parsed) the AdvisorType filter.
    private static readonly string[] AdvisorProfileNames = { "Shared", "Militaristic", "Economic", "Diplomatic", "Intelligence", "Magic", "Movement" };
    private int selectedAdvisorProfile;

    private void DrawAdvisorsSection()
    {
        EditorGUILayout.HelpBox(
            "Pick an advisor to see everything about it in one place: its scoring weights, the HTN conditions that read "
            + "its state, which authored HTN tasks bias toward it, and which cards it owns (with a live score example). "
            + "\"Shared\" holds the handful of things every advisor uses.\n\n"
            + $"Saved to {AdvisorAssetPath}.",
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
        EditorGUILayout.Space(10f);

        selectedAdvisorProfile = GUILayout.Toolbar(Mathf.Clamp(selectedAdvisorProfile, 0, AdvisorProfileNames.Length - 1), AdvisorProfileNames);
        EditorGUILayout.Space(10f);

        DrawAdvisorProfile(AdvisorProfileNames[selectedAdvisorProfile]);
    }

    // AIAdvisorConfig.Keys groups don't all match an advisor name literally (Affinity.X.*,
    // Economy.* for Economic, Targeting.* shared by several) — this is the one place that
    // mapping lives, so "which advisor is this weight about" has a single source of truth.
    private static string AdvisorGroupForWeightKey(string key)
    {
        if (key.StartsWith("Affinity.", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = key.Split('.');
            return parts.Length > 1 ? parts[1] : "Shared";
        }
        if (key.StartsWith("Economy.", StringComparison.OrdinalIgnoreCase)) return "Economic";
        if (key.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)) return "Shared";
        if (key.StartsWith("Targeting.", StringComparison.OrdinalIgnoreCase)) return "Shared";
        return key.Split('.')[0];
    }

    private static string AdvisorGroupForPredicateKey(string key)
        => key.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? "Shared" : key.Split('.')[0];

    // Everything connected to one advisor (or "Shared"), in one screen: weights it owns,
    // HTN conditions about it, HTN tasks that bias toward it, and the cards it owns with a
    // live score preview. Replaces the old flat "everything, in five separate lists" layout.
    private void DrawAdvisorProfile(string advisorGroup)
    {
        EnsureAdvisorStyles();
        bool isShared = string.Equals(advisorGroup, "Shared", StringComparison.OrdinalIgnoreCase);
        Enum.TryParse(advisorGroup, out AdvisorType advisor); // AdvisorType.None if isShared or unparseable

        EditorGUILayout.LabelField("Scoring weights", sectionHeaderStyle);
        List<AdvisorWeightDefinition> weights = AIAdvisorConfig.KnownWeights
            .Where(d => string.Equals(AdvisorGroupForWeightKey(d.key), advisorGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (weights.Count == 0)
        {
            EditorGUILayout.LabelField("(no weights for this advisor)", weightDescStyle);
        }
        else
        {
            foreach (AdvisorWeightDefinition definition in weights) DrawWeightRow(definition);
        }

        if (isShared)
        {
            EditorGUILayout.Space(12f);
            DrawConditionsForGroup(advisorGroup);
            return; // "cards owned" / "tasks that bias toward" only make sense per-advisor
        }

        EditorGUILayout.Space(16f);
        DrawHtnDrivingPanel(advisorGroup, advisor);

        // Live effect comes next, directly under the weights that drive it — change a
        // number above, watch the scores below move, with nothing in between to scroll past.
        EditorGUILayout.Space(16f);
        EditorGUILayout.LabelField("Live effect", sectionHeaderStyle);
        EditorGUILayout.LabelField("Edit any weight above, or any field below, and every score updates.", weightDescStyle);
        DrawScenarioInputs();
        EditorGUILayout.Space(4f);
        DrawActionOwnership(advisor);

        EditorGUILayout.Space(20f);
        EditorGUILayout.LabelField("Reference", sectionHeaderStyle);
        DrawConditionsForGroup(advisorGroup);

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("HTN tasks that bias toward this advisor", conditionKeyStyle);
        List<string> taskRefs = new();
        foreach (HTNStrategyData strategy in strategyLibrary.strategies)
        {
            if (strategy?.nodes == null) continue;
            foreach (HTNNodeData node in strategy.nodes)
            {
                if (node == null || ParseNodeType(node.type) != HTNNodeType.PrimitiveTask) continue;
                if (string.Equals(node.advisor, advisorGroup, StringComparison.OrdinalIgnoreCase))
                {
                    taskRefs.Add($"{strategy.strategyId} : {node.taskId}");
                }
            }
        }
        if (taskRefs.Count == 0)
        {
            EditorGUILayout.LabelField("Not referenced by any authored HTN strategy yet — see the HTN tab.", EditorStyles.miniLabel);
        }
        else
        {
            foreach (string taskRef in taskRefs)
            {
                EditorGUILayout.LabelField("•  " + taskRef, EditorStyles.miniLabel);
            }
        }
    }

    private void DrawConditionsForGroup(string advisorGroup)
    {
        EnsureAdvisorStyles();
        EditorGUILayout.LabelField("HTN conditions about this", sectionHeaderStyle);
        List<HTNPredicateDefinition> conditions = HTNRegistry.KnownPredicates
            .Where(d => string.Equals(AdvisorGroupForPredicateKey(d.Key), advisorGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (conditions.Count == 0)
        {
            EditorGUILayout.LabelField("No HTN condition currently reads this advisor's state.", weightDescStyle);
        }
        else
        {
            foreach (HTNPredicateDefinition definition in conditions)
            {
                bool live = EvaluatePredicateLive(definition.Key);
                EditorGUILayout.BeginVertical(weightRowBoxStyle);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(definition.Key, conditionKeyStyle);
                GUILayout.FlexibleSpace();
                DrawConditionBadge(live ? "TRUE" : "FALSE", live);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(definition.Description, weightDescStyle);
                EditorGUILayout.EndVertical();
            }
        }
    }

    // One card per weight: a top row with the label and the number field big enough to read,
    // and the plain-English description on its own full-width wrapped line underneath —
    // replaces the old single-line layout that squeezed label + field + default + description
    // into one row and clipped the description on anything but a very wide window.
    private void DrawWeightRow(AdvisorWeightDefinition definition)
    {
        EnsureAdvisorStyles();
        float current = advisorWeights.TryGetValue(definition.key, out float v) ? v : definition.defaultValue;
        bool isDefault = Mathf.Approximately(current, definition.defaultValue);

        int firstDot = definition.key.IndexOf('.');
        string shortKey = firstDot >= 0 && firstDot < definition.key.Length - 1 ? definition.key.Substring(firstDot + 1) : definition.key;
        string label = ObjectNames.NicifyVariableName(shortKey.Replace(".", " "));

        EditorGUILayout.BeginVertical(weightRowBoxStyle);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, weightLabelStyle, GUILayout.MinWidth(140f));
        GUILayout.FlexibleSpace();
        if (!isDefault)
        {
            GUILayout.Label($"default {definition.defaultValue:0.##}", EditorStyles.miniLabel);
            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(50f)))
            {
                advisorWeights[definition.key] = definition.defaultValue;
                advisorsDirty = true;
                GUI.FocusControl(null);
                current = definition.defaultValue;
            }
            GUILayout.Space(6f);
        }
        float picked = EditorGUILayout.FloatField(current, GUILayout.Width(70f), GUILayout.Height(20f));
        if (!Mathf.Approximately(picked, current))
        {
            advisorWeights[definition.key] = picked;
            advisorsDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(definition.description, weightDescStyle);

        EditorGUILayout.EndVertical();
    }

    // forcedAdvisor scopes the list to cards owned by exactly one advisor — the only way
    // this is called now, from DrawAdvisorProfile. An action's "resolved" advisor is its
    // override if it has one, else the default coded on the CharacterAction class.
    private void DrawActionOwnership(AdvisorType forcedAdvisor)
    {
        EditorGUILayout.HelpBox(
            "'Default' keeps the advisor coded on the action class; an override moves it here. Scoring has no manual "
            + "order — the single highest-scoring card across the whole deck wins each pick — so to prioritize one "
            + "action, give it a Bonus instead: +2 wins most ties, +10 dominates, negative pushes it to last resort.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        actionSearch = EditorGUILayout.TextField("Filter", actionSearch);
        EditorGUILayout.EndHorizontal();

        actionCatalog ??= BuildActionCatalog();
        cardsByActionRef ??= BuildCardUsageMap();
        string[] advisorNames = Enum.GetNames(typeof(AdvisorType))
            .Select(n => n == nameof(AdvisorType.None) ? "Not advised" : n)
            .ToArray();

        // Auto-expand the single best-scoring, actually-in-a-deck card for this advisor so a
        // full "here's the effect" breakdown is visible on arrival — never hidden behind a
        // click nobody knows to make.
        AdvisorType ResolvedOf(string actionClass, AdvisorType defaultAdvisor) =>
            advisorActionOverrides.TryGetValue(actionClass, out AdvisorType o) ? o : defaultAdvisor;
        List<string> currentClasses = SortedActionCatalog()
            .Where(e => ResolvedOf(e.actionClass, e.defaultAdvisor) == forcedAdvisor)
            .Select(e => e.actionClass)
            .ToList();
        if (!currentClasses.Any(expandedActions.Contains))
        {
            string autoExpand = currentClasses.FirstOrDefault(c =>
                cardsByActionRef.TryGetValue(c, out List<CardUsage> u) && u.Count > 0);
            if (autoExpand != null) expandedActions.Add(autoExpand);
        }

        bool anyShown = false;
        foreach ((string actionClass, AdvisorType defaultAdvisor) in SortedActionCatalog())
        {
            bool hasOverride = advisorActionOverrides.TryGetValue(actionClass, out AdvisorType overridden);
            AdvisorType resolvedAdvisor = hasOverride ? overridden : defaultAdvisor;
            if (resolvedAdvisor != forcedAdvisor) continue;

            cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards);
            bool usedByCards = cards != null && cards.Count > 0;

            if (!string.IsNullOrWhiteSpace(actionSearch)
                && actionClass.IndexOf(actionSearch, StringComparison.OrdinalIgnoreCase) < 0
                && (cards == null || !cards.Any(c => c.cardName.IndexOf(actionSearch, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                continue;
            }

            anyShown = true;
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
                "Exact score of this action's best card under the scenario above. The AI plays the single highest-scoring card across the whole deck."),
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
                "Flat score adjustment added every time the AI scores this action. +2 wins most ties, +10 dominates, negative = last resort."),
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

        if (!anyShown)
        {
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(actionSearch) ? "No actions currently resolve to this advisor." : "No match for that filter.",
                EditorStyles.miniLabel);
        }
    }

    // Highest-scoring first — exactly the order the AI would prefer these cards this turn.
    private IEnumerable<(string actionClass, AdvisorType defaultAdvisor)> SortedActionCatalog()
    {
        float Score((string actionClass, AdvisorType defaultAdvisor) entry)
        {
            AdvisorType resolved = advisorActionOverrides.TryGetValue(entry.actionClass, out AdvisorType o) ? o : entry.defaultAdvisor;
            cardsByActionRef.TryGetValue(entry.actionClass, out List<CardUsage> cards);
            float bonus = advisorActionBonuses.TryGetValue(entry.actionClass, out float b) ? b : 0f;
            return BestScoreForAction(entry.actionClass, resolved, cards, bonus);
        }

        return actionCatalog.OrderByDescending(Score).ThenBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase);
    }

    // Scenario assumptions for the score preview: every value the real scoring
    // reads from the board at runtime becomes an editable input here.
    // Every field gets its own caption above the box (instead of a fixed-width inline label
    // that clips) and fields are grouped into named cards — Character / Status / Gold &
    // military / Distances / HTN bias — so it reads as a form, not one run-on row of controls.
    private void DrawScenarioInputs()
    {
        EnsureAdvisorStyles();
        EditorGUILayout.LabelField("Score preview scenario", sectionHeaderStyle);
        EditorGUILayout.LabelField(
            "Set the unknowns below — every card score, viability tile, and HTN badge on this page recomputes from them.",
            weightDescStyle);
        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Character", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simCommander = DrawStatField("Commander", simCommander);
        simAgent = DrawStatField("Agent", simAgent);
        simEmissary = DrawStatField("Emissary", simEmissary);
        simMage = DrawStatField("Mage", simMage);
        simArtifactsCarried = DrawStatField("Artifacts carried", simArtifactsCarried, width: 110f);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Status", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simLeadingArmy = GUILayout.Toggle(simLeadingArmy, "Leading an army", GUILayout.Width(130f));
        simHostageToRescue = GUILayout.Toggle(simHostageToRescue, new GUIContent("Hostage to rescue", "A friendly character is held captive nearby. Off = rescue actions (Free Character) get 0 situation points."), GUILayout.Width(150f));
        simHoldingHostage = GUILayout.Toggle(simHoldingHostage, new GUIContent("Holding hostage", "This character holds a captive. Off = hostage actions (Ask Ransom, Release Character) get 0 situation points."), GUILayout.Width(140f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        bool outmatched = SimulatedOutmatched();
        DrawConditionBadge(outmatched ? "Outmatched: Yes" : "Outmatched: No", outmatched);
        GUILayout.Space(4f);
        DrawMetricTile("Economy", SimulatedEconomyStatus().ToString(), EconomyThresholdsTooltip());
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Gold & military", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simGoldBuffer = DrawStatField("Gold", simGoldBuffer);
        simGoldPerTurn = DrawStatField("Gold / turn", simGoldPerTurn, width: 100f);
        using (new EditorGUI.DisabledScope(!simLeadingArmy))
        {
            int shownStrength = simLeadingArmy ? simMyArmyStrength : 0;
            int picked = DrawStatField("My army", shownStrength, "Army offence strength. 0 while not leading an army.");
            if (simLeadingArmy) simMyArmyStrength = picked;
        }
        simEnemyStrength = DrawStatField("Enemy army", simEnemyStrength);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Artifacts % owned", "Share of the world's artifacts the nation already owns (0..1)."), GUILayout.Width(130f));
        simArtifactShare = EditorGUILayout.Slider(simArtifactShare, 0f, 1f, GUILayout.Width(220f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Distances (hexes)", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simEnemyDistance = DrawStatFieldF("Enemy PC / army", simEnemyDistance, "Hexes to the nearest enemy PC or army. Use 99 for none.");
        simEnemyCharacterDistance = DrawStatFieldF("Enemy character", simEnemyCharacterDistance, "Hexes to the nearest enemy character. Use 99 for none.");
        simNpcDistance = DrawStatFieldF("Unrevealed NPC", simNpcDistance, "Hexes to the nearest unrevealed NPC. Use 99 for none.");
        simDestinationDistance = DrawStatFieldF("Move destination", simDestinationDistance, "Hexes to the preferred movement destination.");
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("HTN bias preview", conditionKeyStyle);
        EditorGUILayout.LabelField("Simulate the HTN tab's currently-active task biasing scoring toward this advisor.", weightDescStyle);
        string[] biasOptions = Enum.GetNames(typeof(AdvisorType));
        simBiasedAdvisorIndex = EditorGUILayout.Popup(Mathf.Clamp(simBiasedAdvisorIndex, 0, biasOptions.Length - 1), biasOptions, GUILayout.Width(160f));
        EditorGUILayout.EndVertical();
    }

    private int DrawStatField(string caption, int value, string tooltip = null, float width = 90f)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(new GUIContent(caption, tooltip), EditorStyles.miniLabel);
        int result = EditorGUILayout.IntField(value, GUILayout.Width(width), GUILayout.Height(20f));
        EditorGUILayout.EndVertical();
        GUILayout.Space(10f);
        return result;
    }

    private float DrawStatFieldF(string caption, float value, string tooltip = null, float width = 120f)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(new GUIContent(caption, tooltip), EditorStyles.miniLabel);
        float result = EditorGUILayout.FloatField(value, GUILayout.Width(width), GUILayout.Height(20f));
        EditorGUILayout.EndVertical();
        GUILayout.Space(10f);
        return result;
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
    // Mirrors AIContextDataBuilder.CacheEnemyTargets exactly: 0 strength while leading no
    // army (so "no army" always counts as outmatched against any enemy), compared against
    // enemy strength via the single OutmatchedStrengthRatio weight — not a second, looser
    // threshold invented just for the widget.
    private bool SimulatedOutmatched()
    {
        float ratio = advisorWeights.TryGetValue(AIAdvisorConfig.Keys.OutmatchedStrengthRatio, out float r)
            ? r : AIAdvisorConfig.GetDefaultWeight(AIAdvisorConfig.Keys.OutmatchedStrengthRatio);
        float myStrength = simLeadingArmy ? simMyArmyStrength : 0f;
        return simEnemyStrength > myStrength * ratio;
    }

    // Mirrors AIContext.IsEnemyNear (GetDistanceScore(false) > 0).
    private bool SimulatedEnemyNear()
    {
        float proximityMax = advisorWeights.TryGetValue(AIAdvisorConfig.Keys.EnemyProximityMax, out float p)
            ? p : AIAdvisorConfig.GetDefaultWeight(AIAdvisorConfig.Keys.EnemyProximityMax);
        return proximityMax - simEnemyDistance > 0f;
    }

    // Mirrors HTNRegistry's Militaristic.Danger predicate.
    private bool SimulatedDanger() => SimulatedEnemyNear() && SimulatedOutmatched();

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

    // Exact mirror of AIContext.GetAdvisorViability under the scenario assumptions above —
    // literally the same terms SimulateScore adds as an advisor's situational bonus, minus
    // the handful tied to one specific action. This is the number HTNRegistry's Viable
    // predicates compare against a threshold at runtime; showing it here, live, next to the
    // weights that compose it, is what makes "Advisors drive HTN" a visible fact instead of
    // a claim in a comment.
    private float SimulateViability(AdvisorType advisor)
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);
        float enemyProximity = Mathf.Max(0f, W(AIAdvisorConfig.Keys.EnemyProximityMax) - simEnemyDistance);

        return advisor switch
        {
            AdvisorType.Economic => SimulatedEconomyStatus() switch
            {
                EconomyStatus.Critical => W(AIAdvisorConfig.Keys.EconomyCriticalBonus),
                EconomyStatus.Weak => W(AIAdvisorConfig.Keys.EconomyWeakBonus),
                EconomyStatus.Stable => W(AIAdvisorConfig.Keys.EconomyStableBonus),
                _ => 0f
            },
            AdvisorType.Militaristic => enemyProximity + (!simLeadingArmy
                ? W(AIAdvisorConfig.Keys.NoArmyPenalty)
                : simEnemyStrength > 0
                    ? MilitaristicEdge(W)
                    : 0f),
            AdvisorType.Intelligence => Mathf.Max(0f, W(AIAdvisorConfig.Keys.EnemyCharacterProximityMax) - simEnemyCharacterDistance)
                + enemyProximity
                + (SimulatedEconomyStatus() is EconomyStatus.Critical or EconomyStatus.Weak ? W(AIAdvisorConfig.Keys.IntelligencePoorEconomyBonus) : 0f)
                + (SimulatedOutmatched() ? W(AIAdvisorConfig.Keys.IntelligenceOutmatchedBonus) : 0f),
            AdvisorType.Magic => (1f - Mathf.Clamp01(simArtifactShare)) * W(AIAdvisorConfig.Keys.ArtifactScarcityWeight) + enemyProximity,
            AdvisorType.Diplomatic => Mathf.Max(0f, W(AIAdvisorConfig.Keys.NpcProximityMax) - simNpcDistance)
                + enemyProximity
                + (SimulatedOutmatched() ? W(AIAdvisorConfig.Keys.DiplomaticOutmatchedBonus) : 0f),
            AdvisorType.Movement => Mathf.Max(0f, W(AIAdvisorConfig.Keys.MovementProximityMax)
                - simDestinationDistance * W(AIAdvisorConfig.Keys.MovementDistancePenaltyPerHex)),
            _ => 0f
        };
    }

    private float MilitaristicEdge(Func<string, float> W)
    {
        float strengthDiff = simMyArmyStrength - simEnemyStrength;
        float farPenalty = simEnemyDistance > 1f ? W(AIAdvisorConfig.Keys.FarTargetPenalty) : 0f;
        return strengthDiff < 0
            ? Mathf.Max(-10f, strengthDiff / 10f - farPenalty)
            : Mathf.Clamp(strengthDiff / 20f, -5f, 8f) - farPenalty;
    }

    // Named threshold weight per advisor, matching HTNRegistry's Viable predicates.
    private static string ViabilityThresholdKeyFor(AdvisorType advisor) => advisor switch
    {
        AdvisorType.Militaristic => AIAdvisorConfig.Keys.MilitaristicViabilityThreshold,
        AdvisorType.Diplomatic => AIAdvisorConfig.Keys.DiplomaticViabilityThreshold,
        AdvisorType.Intelligence => AIAdvisorConfig.Keys.IntelligenceViabilityThreshold,
        AdvisorType.Magic => AIAdvisorConfig.Keys.MagicViabilityThreshold,
        AdvisorType.Movement => AIAdvisorConfig.Keys.MovementViabilityThreshold,
        _ => null
    };

    // The block that makes the Advisor → HTN connection visible: the live viability number
    // (same weights as "Scoring weights" above), the threshold, and whether HTNRegistry's
    // Viable predicate for this advisor is true right now — for Economic, the tier instead,
    // since Economic's HTN conditions are tier-based rather than a single threshold.
    // A scorecard, not a paragraph: the number, the threshold it's compared against, and the
    // resulting HTN predicate as a colored TRUE/FALSE badge, side by side — everything the old
    // single dense HelpBox said, but readable at a glance instead of parsed as one sentence.
    private void DrawHtnDrivingPanel(string advisorGroup, AdvisorType advisor)
    {
        EnsureAdvisorStyles();
        EditorGUILayout.LabelField("Drives HTN", sectionHeaderStyle);

        if (advisor == AdvisorType.Economic)
        {
            EconomyStatus tier = SimulatedEconomyStatus();
            EditorGUILayout.LabelField($"Economic tier right now: {tier}", weightLabelStyle);
            EditorGUILayout.Space(4f);

            // All four tiers, not just the active one — each is its own HTN condition, and
            // exactly one is ever TRUE at a time; showing all four makes that mutual-exclusion
            // visible instead of implied.
            EditorGUILayout.BeginHorizontal();
            DrawConditionBadge("Economic.Critical", EvaluatePredicateLive("Economic.Critical"));
            DrawConditionBadge("Economic.Weak", EvaluatePredicateLive("Economic.Weak"));
            DrawConditionBadge("Economic.Stable", EvaluatePredicateLive("Economic.Stable"));
            DrawConditionBadge("Economic.Surplus", EvaluatePredicateLive("Economic.Surplus"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            DrawConditionBadge("Economic.NeedsHelp", EvaluatePredicateLive("Economic.NeedsHelp"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Same Economy weights as above (income/gold thresholds) decide which tier is active — nothing extra to tune here.",
                weightDescStyle);
            return;
        }

        string thresholdKey = ViabilityThresholdKeyFor(advisor);
        float viability = SimulateViability(advisor);
        float threshold = thresholdKey != null
            ? (advisorWeights.TryGetValue(thresholdKey, out float t) ? t : AIAdvisorConfig.GetDefaultWeight(thresholdKey))
            : 0f;
        bool viable = viability > threshold;

        EditorGUILayout.BeginHorizontal();
        DrawMetricTile("Viability", viability.ToString("0.0"));
        DrawMetricTile("Threshold", threshold.ToString("0.0"));
        DrawStatusBadge($"{advisorGroup}.Viable", viable, GUILayout.MinWidth(180f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Viability uses the same weights shown above. Edit the Viability Threshold weight above to change when this flips — not here.",
            weightDescStyle);
    }

    private void DrawMetricTile(string caption, string value, string tooltip = null)
    {
        EditorGUILayout.BeginVertical(weightRowBoxStyle, GUILayout.Width(110f));
        EditorGUILayout.LabelField(value, metricValueStyle);
        EditorGUILayout.LabelField(new GUIContent(caption, tooltip), metricCaptionStyle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    private void DrawStatusBadge(string label, bool positive, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginVertical(options);
        GUILayout.FlexibleSpace();
        Rect r = GUILayoutUtility.GetRect(0, 36f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, positive ? BadgeTrueColor : BadgeFalseColor);
        GUI.Label(r, $"{label} = {(positive ? "TRUE" : "FALSE")}", badgeStyle);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
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
                AddSituation(Mathf.Max(0f, W(AIAdvisorConfig.Keys.MovementProximityMax)
                    - simDestinationDistance * W(AIAdvisorConfig.Keys.MovementDistancePenaltyPerHex)), "destination near");
                break;
        }

        string[] biasOptions = Enum.GetNames(typeof(AdvisorType));
        string biasedAdvisorName = biasOptions[Mathf.Clamp(simBiasedAdvisorIndex, 0, biasOptions.Length - 1)];
        float biasBonus = !string.Equals(biasedAdvisorName, nameof(AdvisorType.None), StringComparison.OrdinalIgnoreCase)
            && string.Equals(advisor.ToString(), biasedAdvisorName, StringComparison.OrdinalIgnoreCase)
            ? W(AIAdvisorConfig.Keys.HTNBiasBonus)
            : 0f;

        float total = baseScore - difficultyPenalty - costPenalty + affinity + situational + bonus + biasBonus;

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
        if (!Mathf.Approximately(biasBonus, 0f))
        {
            parts.Add($"+ {biasBonus:0.#} HTN bias ({biasedAdvisorName})");
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
