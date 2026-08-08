using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using K = AIAdvisorConfig.Keys;

public class AIWidgetWindow : EditorWindow
{
    private enum Tab
    {
        Situations = 0,
        Strategies = 1,
        Advisors = 2,
        CardBoard = 3,
        NN = 4
    }

    private static readonly string[] TabLabels = { "Situations", "HTN", "Advisors", "Card Board", "NN" };

    private const string PriorityAssetPath = "Assets/Resources/" + SituationEvaluator.PriorityResourcePath + ".json";
    private const string StrategiesAssetPath = "Assets/Resources/" + AIStrategyLibrary.ResourcePath + ".json";
    private const string AdvisorAssetPath = "Assets/Resources/" + AIAdvisorConfig.ResourcePath + ".json";

    private Tab currentTab = Tab.Situations;

    // Situations tab
    private Vector2 situationsScroll;
    private List<CardSituationEnum> situationOrder = new();
    private ReorderableList situationList;
    private bool orderDirty;
    private Dictionary<CardSituationEnum, List<string>> situationCardNames;
    private readonly HashSet<CardSituationEnum> situationPreviewActive = new();

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
    // Keyed by AIAdvisorConfig.BuildCardProfileKey(deckId, cardId) — one independent row per
    // printed card. Cards sharing an action class each get their own entry (see
    // DuplicateProfileToSiblingCards for the "seed one from another" authoring shortcut).
    private readonly Dictionary<string, CardAdvisorProfile> cardProfiles = new(StringComparer.OrdinalIgnoreCase);
    private List<(string actionClass, AdvisorType defaultAdvisor)> actionCatalog;
    private Dictionary<string, List<CardUsage>> cardsByActionRef;
    private bool advisorsDirty;
    private int simBiasedAdvisorIndex; // index into Enum.GetNames(typeof(AdvisorType)); 0 = None
    private bool scenarioFoldout;
    private readonly Dictionary<string, bool> htnParamFoldouts = new(StringComparer.OrdinalIgnoreCase);

    // Score-preview scenario: user-set assumptions for everything the real
    // scoring reads from the board at runtime.
    private int simCommander = 2;
    private int simAgent = 2;
    private int simEmissary = 2;
    private int simMage = 2;
    private int simArtifactsCarried;
    private int simHiddenArtifacts;
    private int simSpellsAvailable;
    private int simUtilityActionIndex;
    private bool simLeadingArmy;
    private bool simHostageToRescue;
    private bool simHoldingHostage;
    private int simGoldBuffer = 50;
    private int simResourceNetWorth = 20;
    private int simMyArmyStrength = 100;
    private int simEnemyStrength = 100;
    private float simEnemyDistance = 5f;
    private float simEnemyCharacterDistance = 5f;
    private float simNpcDistance = 5f;
    private float simDestinationDistance = 3f;
    private float simArtifactShare = 0.25f;
    private float simOwnPcFortificationDistance = 99f;
    private float simNplRecruitmentDistance = 99f;

    private class CardUsage
    {
        public string cardName;
        public string effect;
        public int difficulty;
        public int goldCost;
        public string deckId;
        public int cardId;
    }

    // Card Board tab — drag-and-drop reclassification, writing directly to
    // cardProfiles (the same dictionary Save/Load round-trips to AdvisorConfig.json).
    private Vector2 cardBoardScroll;
    private string cardBoardSearch = string.Empty;
    private bool cardBoardHideAssigned;
    private string dragPayloadCardKey; // deckId::cardId of the card being dragged, null when idle
    private string dragPayloadCardName;
    private string dragPayloadActionClass; // needed on drop to resolve the action's coded default advisor
    private Vector2 dragPointerPos;
    private readonly Dictionary<AdvisorType, Rect> cardBoardBucketRects = new();

    private static readonly AdvisorType[] CardBoardBuckets =
    {
        AdvisorType.None, AdvisorType.Militaristic, AdvisorType.Economic, AdvisorType.Diplomatic,
        AdvisorType.Intelligence, AdvisorType.Magic, AdvisorType.Movement
    };

    private static Color CardBoardBucketColor(AdvisorType advisor) => advisor switch
    {
        AdvisorType.Militaristic => new Color(0.63f, 0.24f, 0.20f),
        AdvisorType.Economic => new Color(0.66f, 0.47f, 0.12f),
        AdvisorType.Diplomatic => new Color(0.18f, 0.48f, 0.45f),
        AdvisorType.Intelligence => new Color(0.24f, 0.42f, 0.54f),
        AdvisorType.Magic => new Color(0.36f, 0.30f, 0.62f),
        AdvisorType.Movement => new Color(0.24f, 0.48f, 0.32f),
        _ => new Color(0.35f, 0.35f, 0.35f)
    };

    private static readonly string[] NodeTypeLabels = { "CompoundTask", "Method", "PrimitiveTask" };
    private static readonly string[] InvertLabels = { "IS TRUE", "IS FALSE" };

    [MenuItem("Tools/RetroLOTR/AI Widget")]
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
            case Tab.CardBoard:
                DrawCardBoardTab();
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
        EnsureAdvisorStyles();
        EditorGUILayout.LabelField("Situation ranking", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "A Situation is a concrete circumstance your character can be in right now — e.g. CommanderAtOwnPC "
            + "(your commander standing on your own city) or ArmyAtEnemyPC (your army parked on an enemy's). Several "
            + "can be true on the same hex at once (SituationEvaluator.GetActiveSituations checks all of them, not "
            + "just one). Every drawable card can carry ONE situation tag; when a card's tag is among the situations "
            + "active right now, DeckManager.ScoreOpportunityCard adds max(0, 10 − rank) to its score, where rank is "
            + "that situation's position counted only among the OTHER situations ALSO active right now — not its "
            + "absolute position in this list. So dragging a situation above another only changes anything on the "
            + "turns both happen to be active on the same hex at once; the preview below simulates exactly that.\n"
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
        EditorGUILayout.Space(20f);
        DrawSituationPreview();
        EditorGUILayout.EndScrollView();
    }

    // "What happens when I put one above another" — tick the situations you want to
    // simulate as simultaneously active on one hex; each row's bonus recomputes live from
    // the REAL formula (rank among only the ticked ones, per SituationEvaluator.
    // GetActiveSituations + DeckManager.ScoreOpportunityCard), not a naive "position in the
    // full list" guess — those two are only the same number when nothing else ticked outranks it.
    private void DrawSituationPreview()
    {
        situationCardNames ??= BuildSituationUsageMap();

        EditorGUILayout.LabelField("Preview — what if several were active on one hex at once?", sectionHeaderStyle);
        EditorGUILayout.LabelField(
            "Tick every situation you want to simulate as true at the same time, then drag rows above to see the "
            + "bonuses (and the winner) change live.",
            weightDescStyle);
        EditorGUILayout.Space(6f);

        List<CardSituationEnum> activeInOrder = situationOrder.Where(situationPreviewActive.Contains).ToList();

        foreach (CardSituationEnum situation in situationOrder)
        {
            bool wasActive = situationPreviewActive.Contains(situation);

            EditorGUILayout.BeginHorizontal(weightRowBoxStyle);
            bool nowActive = GUILayout.Toggle(wasActive, GUIContent.none, GUILayout.Width(18f));
            if (nowActive != wasActive)
            {
                if (nowActive) situationPreviewActive.Add(situation);
                else situationPreviewActive.Remove(situation);
                activeInOrder = situationOrder.Where(situationPreviewActive.Contains).ToList();
            }

            GUILayout.Label(ObjectNames.NicifyVariableName(situation.ToString()), weightLabelStyle, GUILayout.Width(240f));
            situationCardNames.TryGetValue(situation, out List<string> cardNames);
            GUILayout.Label(cardNames is { Count: > 0 } ? string.Join(", ", cardNames) : "(no card currently tagged with this)", weightDescStyle);
            GUILayout.FlexibleSpace();

            if (nowActive)
            {
                int effectiveRank = activeInOrder.IndexOf(situation);
                int bonus = Mathf.Max(0, 10 - effectiveRank);
                DrawConditionBadge($"active, rank {effectiveRank + 1} of {activeInOrder.Count} → +{bonus}", bonus > 0);
            }
            else
            {
                GUILayout.Label("not ticked", EditorStyles.miniLabel, GUILayout.Width(160f));
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8f);
        if (activeInOrder.Count == 0)
        {
            EditorGUILayout.LabelField("Tick at least one situation above to see how it scores.", weightDescStyle);
        }
        else
        {
            CardSituationEnum winner = activeInOrder[0];
            EditorGUILayout.HelpBox(
                $"With all {activeInOrder.Count} ticked situation(s) active on the same hex, a card tagged "
                + $"\"{ObjectNames.NicifyVariableName(winner.ToString())}\" gets the biggest situation bonus (+10) and "
                + "is the most likely Opportunity Card to be offered — remaining ties are broken by skill affinity "
                + "and base score, not by situation rank.",
                MessageType.Info);
        }
    }

    // Real card names carrying each situation tag (CardData.situation), scanned the same way
    // BuildCardUsageMap reads action refs — so "no card currently tagged with this" in the
    // preview above is a verified fact, not a guess.
    private static Dictionary<CardSituationEnum, List<string>> BuildSituationUsageMap()
    {
        Dictionary<CardSituationEnum, List<string>> map = new();

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
                CardSituationEnum situation = card.GetSituation();
                if (situation == CardSituationEnum.None) continue;

                if (!map.TryGetValue(situation, out List<string> names))
                {
                    names = new List<string>();
                    map[situation] = names;
                }
                if (!names.Contains(card.name)) names.Add(card.name);
            }
        }

        return map;
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
                    new() { depth = 1, type = "Method", precondition = Cond("Global.Always"), taskId = "root.fallback" },
                    new() { depth = 2, type = "PrimitiveTask", advisor = string.Empty, completionCondition = Cond("Global.Never"), taskId = "root.fallback.leaf" }
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

            string newTaskId = EditorGUILayout.DelayedTextField(node.taskId, GUILayout.Width(220f));
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
                    DrawConditionTermsInline(node.precondition, 110f);
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
                    DrawConditionTermsInline(node.completionCondition, 110f);
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

    // Renders a term list as "<term> OR <term> OR ..." inline, each term a predicate popup +
    // invert popup, with a "+OR" to add another term and a "✕" per term (once there's more
    // than one) to remove it. This is the actual authoring surface for HTN-level OR — a
    // Method's precondition is built here from named predicates directly, never through a
    // bespoke alias predicate.
    private void DrawConditionTermsInline(List<HTNConditionTerm> terms, float popupWidth)
    {
        int removeIndex = -1;
        for (int t = 0; t < terms.Count; t++)
        {
            if (t > 0) GUILayout.Label("OR", EditorStyles.miniBoldLabel, GUILayout.Width(22f));

            HTNConditionTerm term = terms[t];
            string picked = DrawNamePopup(term.name, HTNRegistry.PredicateNames, popupWidth);
            if (!string.Equals(picked, term.name, StringComparison.Ordinal)) { term.name = picked; strategiesDirty = true; }

            int invertIndex = EditorGUILayout.Popup(term.invert ? 1 : 0, InvertLabels, GUILayout.Width(70f));
            bool newInvert = invertIndex == 1;
            if (newInvert != term.invert) { term.invert = newInvert; strategiesDirty = true; }

            if (terms.Count > 1 && GUILayout.Button("✕", GUILayout.Width(20f))) removeIndex = t;
        }

        if (GUILayout.Button("+OR", GUILayout.Width(38f)))
        {
            terms.Add(new HTNConditionTerm { name = "Global.Always" });
            strategiesDirty = true;
        }

        if (removeIndex >= 0)
        {
            terms.RemoveAt(removeIndex);
            strategiesDirty = true;
        }
    }

    private static HTNNodeType ParseNodeType(string type)
        => Enum.TryParse(type, true, out HTNNodeType parsed) ? parsed : HTNNodeType.PrimitiveTask;

    // The always-visible line under a Method/PrimitiveTask row explaining what its
    // precondition/completion condition actually means — sourced from
    // HTNRegistry.KnownPredicates.Description, never left as a bare unexplained name.
    private static string BuildConditionSummary(HTNNodeData node)
    {
        HTNNodeType type = ParseNodeType(node.type);
        List<HTNConditionTerm> terms = type switch
        {
            HTNNodeType.Method => node.precondition,
            HTNNodeType.PrimitiveTask => node.completionCondition,
            _ => null
        };
        List<HTNConditionTerm> validTerms = terms?.FindAll(t => t != null && !string.IsNullOrWhiteSpace(t.name));
        if (validTerms == null || validTerms.Count == 0) return null;

        string roleLabel = type == HTNNodeType.Method ? "Precondition" : "Completes when";
        List<string> parts = validTerms.ConvertAll(t =>
        {
            string description = HTNRegistry.TryGetDescription(t.name, out string desc)
                ? desc
                : $"Unknown condition '{t.name}' — not in HTNRegistry.KnownPredicates.";
            string name = t.invert ? $"NOT {t.name}" : t.name;
            return $"{name} ({description})";
        });
        return $"{roleLabel}: {string.Join("  OR  ", parts)}";
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
        => new() { depth = depth, type = HTNNodeType.PrimitiveTask.ToString(), advisor = string.Empty, completionCondition = Cond("Global.Never"), taskId = string.Empty };

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
            if (type == HTNNodeType.Method)
            {
                foreach (HTNConditionTerm term in node.precondition ?? new List<HTNConditionTerm>())
                {
                    if (term != null && !string.IsNullOrWhiteSpace(term.name) && !HTNRegistry.TryGetPredicate(term.name, out _))
                        issues.Add($"Row {i + 1}: unknown precondition term '{term.name}'.");
                }
            }

            List<HTNConditionTerm> completionTerms = type == HTNNodeType.PrimitiveTask
                ? (node.completionCondition ?? new List<HTNConditionTerm>()).FindAll(t => t != null && !string.IsNullOrWhiteSpace(t.name))
                : null;
            if (completionTerms != null)
            {
                foreach (HTNConditionTerm term in completionTerms)
                {
                    if (!HTNRegistry.TryGetPredicate(term.name, out _))
                        issues.Add($"Row {i + 1}: unknown completion condition term '{term.name}'.");
                }

                bool completesNever = completionTerms.Count == 0
                    || (completionTerms.Count == 1 && !completionTerms[0].invert && string.Equals(completionTerms[0].name, "Global.Never", StringComparison.OrdinalIgnoreCase));
                if (completesNever && NextSiblingIndex(nodes, i) >= 0)
                {
                    issues.Add($"Row {i + 1}: completion condition is 'Never', but it isn't the last step in its sequence — later rows will never be reached through normal advancement (only via interrupt).");
                }
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

    // A precondition/completion condition is a list of terms OR'd together — Cond(a, b) means
    // "a OR b", authored directly on the row, not through a named alias predicate.
    private static List<HTNConditionTerm> Cond(params string[] names)
        => names.Select(n => new HTNConditionTerm { name = n }).ToList();

    // Data mirror of HTNStrategyBuilder.BuildDefault().
    private static HTNStrategyData BuildDefaultStrategyData()
    {
        return new HTNStrategyData
        {
            strategyId = AIStrategyLibrary.DefaultStrategyId,
            nodes = new List<HTNNodeData>
            {
                new() { depth = 0, type = "CompoundTask", taskId = "root" },
                new() { depth = 1, type = "Method", precondition = Cond("Militaristic.Danger"), taskId = "root.danger" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Militaristic", completionCondition = Cond("Global.Never"), taskId = "root.danger.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Economic.Critical", "Economic.Weak"), taskId = "root.recover" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Economic", completionCondition = Cond("Economic.Weak"), taskId = "root.recover.build" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Economic", completionCondition = Cond("Economic.Stable"), taskId = "root.recover.trade" },
                new() { depth = 1, type = "Method", precondition = Cond("Militaristic.Viable"), taskId = "root.offense" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.offense.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.offense.pick.mil" },
                new() { depth = 4, type = "PrimitiveTask", advisor = "Militaristic", completionCondition = Cond("Global.Never"), taskId = "root.offense.pick.mil.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Magic.Viable"), taskId = "root.magic" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Magic", completionCondition = Cond("Global.Never"), taskId = "root.magic.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.Viable"), taskId = "root.diplomacy" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Diplomatic", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Intelligence.Viable"), taskId = "root.intelligence" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Intelligence", completionCondition = Cond("Global.Never"), taskId = "root.intelligence.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Movement.Viable"), taskId = "root.movement" },
                new() { depth = 2, type = "PrimitiveTask", advisor = "Movement", completionCondition = Cond("Global.Never"), taskId = "root.movement.leaf" },
                new() { depth = 1, type = "Method", precondition = Cond("Global.Always"), taskId = "root.fallback" },
                new() { depth = 2, type = "PrimitiveTask", advisor = string.Empty, completionCondition = Cond("Global.Never"), taskId = "root.fallback.leaf" }
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

    // ------------------------------------------------------------------
    // Card Board tab — drag a card onto an advisor bucket to set its override, writing
    // directly to cardProfiles (same Save/Revert as the rest of this window).
    // Unity's DragAndDrop API is for OS/Project-window drags; reassigning rows within one
    // editor window is a plain custom drag: track a payload string across MouseDown/
    // MouseDrag/MouseUp and hit-test against Rects captured fresh every OnGUI call.
    // ------------------------------------------------------------------

    private AdvisorType ResolvedAdvisorFor(string cardKey, AdvisorType defaultAdvisor) =>
        cardProfiles.TryGetValue(cardKey, out CardAdvisorProfile p)
            && !string.IsNullOrWhiteSpace(p.advisor)
            && Enum.TryParse(p.advisor, true, out AdvisorType advisor)
            ? advisor
            : defaultAdvisor;

    private static bool IsProfileEmpty(CardAdvisorProfile p) =>
        p == null || (string.IsNullOrWhiteSpace(p.advisor) && Mathf.Approximately(p.scoreBonus, 0f)
            && !p.ignoreSituation && (p.utilityParameters == null || p.utilityParameters.Count == 0));

    private void SetOrPruneProfile(string cardKey, CardAdvisorProfile profile)
    {
        if (IsProfileEmpty(profile)) cardProfiles.Remove(cardKey);
        else cardProfiles[cardKey] = profile;
    }

    // Every (card, actionClass) pair the Card Board currently lists, with its coded default
    // advisor — the flattened view DrawCardBoardBuckets/DrawHtnBiasSimulation count over, now
    // that advisor assignment is per-card rather than per-action-class.
    private IEnumerable<(CardUsage card, string actionClass, AdvisorType defaultAdvisor)> AllCardBoardCards()
    {
        foreach ((string actionClass, AdvisorType defaultAdvisor) in actionCatalog)
        {
            if (!cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards)) continue;
            foreach (CardUsage card in cards) yield return (card, actionClass, defaultAdvisor);
        }
    }

    private void DrawCardBoardTab()
    {
        actionCatalog ??= BuildActionCatalog();
        cardsByActionRef ??= BuildCardUsageMap();
        EnsureAdvisorStyles();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!advisorsDirty))
        {
            if (GUILayout.Button("Save", GUILayout.Width(90f))) SaveAdvisorConfig();
            if (GUILayout.Button("Revert", GUILayout.Width(90f))) LoadAdvisorConfig();
        }
        if (advisorsDirty) GUILayout.Label("Unsaved changes", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "Drag a row onto a bucket below to set its advisor override — same data the Advisors tab's per-card "
            + "dropdowns edit, just dragged instead of picked. Drop on \"Unadvised\" to force no advisor; dropping "
            + "on a card's own coded default clears the override entirely (shown as the colored dot going back to grey).",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        cardBoardSearch = EditorGUILayout.TextField("Filter", cardBoardSearch);
        cardBoardHideAssigned = GUILayout.Toggle(cardBoardHideAssigned, "Hide already-assigned cards", GUILayout.Width(190f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);

        DrawCardBoardBuckets();
        EditorGUILayout.Space(10f);

        cardBoardScroll = EditorGUILayout.BeginScrollView(cardBoardScroll);
        DrawCardBoardList();
        EditorGUILayout.EndScrollView();

        HandleCardBoardDragGlobal();
    }

    private void DrawCardBoardBuckets()
    {
        cardBoardBucketRects.Clear();
        GUIStyle nameStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUIStyle countStyle = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        List<(CardUsage card, string actionClass, AdvisorType defaultAdvisor)> allCards = AllCardBoardCards().ToList();

        EditorGUILayout.BeginHorizontal();
        foreach (AdvisorType advisor in CardBoardBuckets)
        {
            int count = allCards.Count(e => ResolvedAdvisorFor(AIAdvisorConfig.BuildCardProfileKey(e.card.deckId, e.card.cardId), e.defaultAdvisor) == advisor);
            Color color = CardBoardBucketColor(advisor);

            Rect r = GUILayoutUtility.GetRect(0, 46f, GUILayout.ExpandWidth(true));
            bool hovered = dragPayloadCardKey != null && r.Contains(dragPointerPos);
            EditorGUI.DrawRect(r, hovered ? Color.Lerp(color, Color.white, 0.35f) : color);
            GUI.Label(new Rect(r.x, r.y + 4f, r.width, 18f), advisor == AdvisorType.None ? "Unadvised" : advisor.ToString(), nameStyle);
            GUI.Label(new Rect(r.x, r.y + 24f, r.width, 18f), $"{count} card{(count == 1 ? "" : "s")}", countStyle);

            cardBoardBucketRects[advisor] = r;
            GUILayout.Space(4f);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCardBoardList()
    {
        string filter = cardBoardSearch?.Trim();
        foreach ((string actionClass, AdvisorType defaultAdvisor) in actionCatalog.OrderBy(e => e.actionClass, StringComparer.OrdinalIgnoreCase))
        {
            cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards);
            bool used = cards != null && cards.Count > 0;

            if (!string.IsNullOrEmpty(filter))
            {
                bool matches = actionClass.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || (used && cards.Any(c => c.cardName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!matches) continue;
            }

            if (used)
            {
                foreach (CardUsage card in cards.OrderBy(c => c.cardName, StringComparer.OrdinalIgnoreCase))
                {
                    string cardKey = AIAdvisorConfig.BuildCardProfileKey(card.deckId, card.cardId);
                    AdvisorType resolved = ResolvedAdvisorFor(cardKey, defaultAdvisor);
                    if (cardBoardHideAssigned && resolved != AdvisorType.None) continue;
                    DrawCardBoardRow(card, actionClass, resolved);
                }
                continue;
            }

            // No printed card uses this action class — there's no card identity to attach a
            // per-card override to, so this is a read-only display of the coded default only.
            if (cardBoardHideAssigned && defaultAdvisor != AdvisorType.None) continue;

            EditorGUILayout.BeginVertical(weightRowBoxStyle);

            EditorGUILayout.BeginHorizontal();
            Rect dotRect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.Width(10f));
            EditorGUI.DrawRect(dotRect, CardBoardBucketColor(defaultAdvisor));
            GUILayout.Space(6f);

            GUILayout.Label(ObjectNames.NicifyVariableName(actionClass), weightLabelStyle, GUILayout.Width(200f));
            GUILayout.Label("(no card references this class)", GUILayout.Width(220f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(defaultAdvisor == AdvisorType.None ? "Unadvised" : defaultAdvisor.ToString(), EditorStyles.miniLabel, GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("(no effect text on this card)", weightDescStyle);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawCardBoardRow(CardUsage card, string actionClass, AdvisorType resolved)
    {
        string cardKey = AIAdvisorConfig.BuildCardProfileKey(card.deckId, card.cardId);
        bool isDragging = string.Equals(dragPayloadCardKey, cardKey, StringComparison.Ordinal);
        if (isDragging) GUI.color = new Color(1f, 1f, 1f, 0.35f);
        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.BeginHorizontal();
        Rect dotRect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.Width(10f));
        EditorGUI.DrawRect(dotRect, CardBoardBucketColor(resolved));
        GUILayout.Space(6f);
        GUILayout.Label(card.cardName, weightLabelStyle, GUILayout.Width(220f));
        GUILayout.Label(ObjectNames.NicifyVariableName(actionClass), EditorStyles.miniLabel, GUILayout.Width(180f));
        GUILayout.FlexibleSpace();
        GUILayout.Label(resolved == AdvisorType.None ? "Unadvised" : resolved.ToString(), EditorStyles.miniLabel, GUILayout.Width(90f));
        bool hasSiblings = cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> siblings) && siblings.Count > 1;
        using (new EditorGUI.DisabledScope(!hasSiblings || !cardProfiles.ContainsKey(cardKey)))
        {
            if (GUILayout.Button("Duplicate to siblings", EditorStyles.miniButton, GUILayout.Width(140f)))
            {
                DuplicateProfileToSiblingCards(cardKey, actionClass);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(string.IsNullOrEmpty(card.effect) ? "(no effect text on this card)" : card.effect, weightDescStyle);
        DrawCardUtilityProfile(cardKey, card.deckId, card.cardId, card.cardName, actionClass);
        EditorGUILayout.EndVertical();
        if (isDragging) GUI.color = Color.white;
        HandleCardBoardDragSource(GUILayoutUtility.GetLastRect(), cardKey, card.cardName, actionClass);
    }

    // Copies this card's full profile (advisor/bonus/flags/utility params) as an independent
    // copy onto every other printed card sharing its action class. Cards keep their own rows
    // after this — nothing at runtime ever shares one.
    private void DuplicateProfileToSiblingCards(string sourceCardKey, string actionClass)
    {
        if (!cardProfiles.TryGetValue(sourceCardKey, out CardAdvisorProfile source)) return;
        if (!cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> siblings)) return;

        List<CardUsage> targets = siblings
            .Where(c => !string.Equals(AIAdvisorConfig.BuildCardProfileKey(c.deckId, c.cardId), sourceCardKey, StringComparison.Ordinal))
            .ToList();
        if (targets.Count == 0) return;

        int overwriteCount = targets.Count(c => cardProfiles.ContainsKey(AIAdvisorConfig.BuildCardProfileKey(c.deckId, c.cardId)));
        string message = $"Copy this card's advisor tuning to {targets.Count} other card(s) using {actionClass}"
            + (overwriteCount > 0 ? $" ({overwriteCount} already have their own tuning and will be overwritten)" : "")
            + "?";
        if (!EditorUtility.DisplayDialog("Duplicate Profile", message, "Duplicate", "Cancel")) return;

        foreach (CardUsage target in targets)
        {
            string key = AIAdvisorConfig.BuildCardProfileKey(target.deckId, target.cardId);
            cardProfiles[key] = new CardAdvisorProfile
            {
                deckId = target.deckId,
                cardId = target.cardId,
                cardName = target.cardName,
                actionClass = actionClass,
                advisor = source.advisor,
                scoreBonus = source.scoreBonus,
                ignoreSituation = source.ignoreSituation,
                utilityParameters = source.utilityParameters?
                    .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                    .ToList() ?? new List<ActionUtilityParameterModifier>()
            };
        }
        advisorsDirty = true;
    }

    // This is intentionally on Card Board: these are card/action authoring
    // choices, not Advisor sensing weights. Every value appears verbatim in
    // AdvisorConfig.json and contributes exactly parameter * multiplier + bonus.
    private void DrawCardUtilityProfile(string cardKey, string deckId, int cardId, string cardName, string actionClass)
    {
        cardProfiles.TryGetValue(cardKey, out CardAdvisorProfile existing);
        List<ActionUtilityParameterModifier> modifiers = existing?.utilityParameters
            ?.Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus }).ToList()
            ?? new List<ActionUtilityParameterModifier>();
        bool changedProfile = false;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Utility profile — each line is: parameter × multiplier + bonus", EditorStyles.miniBoldLabel);
        foreach (ActionUtilityParameterModifier modifier in modifiers.ToList())
        {
            EditorGUILayout.BeginVertical(weightRowBoxStyle);
            EditorGUI.BeginChangeCheck();
            int index = Mathf.Max(0, AIUtilityParameters.Known.ToList().FindIndex(p => string.Equals(p, modifier.parameter, StringComparison.OrdinalIgnoreCase)));
            int changed = EditorGUILayout.Popup("Advisor parameter", index, AIUtilityParameters.Known.ToArray());
            modifier.parameter = AIUtilityParameters.Known[changed];
            EditorGUILayout.BeginHorizontal();
            modifier.multiplier = EditorGUILayout.FloatField("Multiplier", modifier.multiplier, GUILayout.MinWidth(160f));
            modifier.bonus = EditorGUILayout.FloatField("Bonus", modifier.bonus, GUILayout.MinWidth(160f));
            changedProfile |= EditorGUI.EndChangeCheck();
            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                modifiers.Remove(modifier);
                changedProfile = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add utility parameter", GUILayout.Width(180f), GUILayout.Height(24f)))
        {
            string firstUnused = AIUtilityParameters.Known.FirstOrDefault(p => !modifiers.Any(m => string.Equals(m.parameter, p, StringComparison.OrdinalIgnoreCase)))
                ?? AIUtilityParameters.Known[0];
            modifiers.Add(new ActionUtilityParameterModifier { parameter = firstUnused, multiplier = 1f, bonus = 0f });
            changedProfile = true;
        }

        CardAdvisorProfile profile = existing ?? new CardAdvisorProfile { deckId = deckId, cardId = cardId, cardName = cardName, actionClass = actionClass };
        profile.utilityParameters = modifiers;
        SetOrPruneProfile(cardKey, profile);
        if (changedProfile) advisorsDirty = true;
    }

    private void HandleCardBoardDragSource(Rect rowRect, string cardKey, string cardName, string actionClass)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
        {
            dragPayloadCardKey = cardKey;
            dragPayloadCardName = cardName;
            dragPayloadActionClass = actionClass;
            dragPointerPos = e.mousePosition;
            e.Use();
            Repaint();
        }
    }

    private void HandleCardBoardDragGlobal()
    {
        if (dragPayloadCardKey == null) return;
        Event e = Event.current;

        if (e.type == EventType.MouseDrag)
        {
            dragPointerPos = e.mousePosition;
            Repaint();
        }
        else if (e.type == EventType.MouseUp)
        {
            foreach (KeyValuePair<AdvisorType, Rect> bucket in cardBoardBucketRects)
            {
                if (bucket.Value.Contains(e.mousePosition))
                {
                    ApplyCardBoardDrop(dragPayloadCardKey, dragPayloadActionClass, bucket.Key);
                    break;
                }
            }
            dragPayloadCardKey = null;
            dragPayloadCardName = null;
            dragPayloadActionClass = null;
            Repaint();
        }

        if (dragPayloadCardKey != null)
        {
            GUIContent content = new(dragPayloadCardName);
            Vector2 size = EditorStyles.helpBox.CalcSize(content);
            Rect labelRect = new(dragPointerPos.x + 12f, dragPointerPos.y + 12f, size.x + 16f, size.y);
            GUI.Box(labelRect, content, EditorStyles.helpBox);
        }
    }

    private void ApplyCardBoardDrop(string cardKey, string actionClass, AdvisorType target)
    {
        var entry = actionCatalog.FirstOrDefault(e => string.Equals(e.actionClass, actionClass, StringComparison.Ordinal));
        if (entry.actionClass == null) return;
        if (!cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards)) return;
        CardUsage card = cards.FirstOrDefault(c => string.Equals(AIAdvisorConfig.BuildCardProfileKey(c.deckId, c.cardId), cardKey, StringComparison.Ordinal));
        if (card == null) return;

        cardProfiles.TryGetValue(cardKey, out CardAdvisorProfile profile);
        profile ??= new CardAdvisorProfile { deckId = card.deckId, cardId = card.cardId, cardName = card.cardName, actionClass = actionClass };
        profile.advisor = target == entry.defaultAdvisor ? string.Empty : target.ToString();
        SetOrPruneProfile(cardKey, profile);
        advisorsDirty = true;
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
            cardProfiles.Clear();
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

    // One HTN Parameter (predicate) this advisor drives: its live simulation, and exactly the
    // weights that feed its formula — nothing more. "Params involved" is hand-mapped to each
    // predicate's real formula (see AIContext.GetAdvisorViability / HTNRegistry), not derived
    // from AdvisorGroupForWeightKey's naming-prefix grouping, since a weight like
    // Targeting.EnemyProximityMax genuinely feeds five different advisors' predicates at once —
    // it shows up under every one of them, not just wherever its key prefix happens to live.
    // Every remaining weight in the game feeds the formula that decides some HTN predicate's
    // truth value — full stop. There is no other role left for a weight to play (skill
    // affinity, difficulty penalty, cost pressure, and the tier-reactive Economy bonuses were
    // all removed rather than kept as some lesser "connected" category).
    private class HtnParamGroup
    {
        public string title;
        public string description;
        public string[] weightKeys;
        public Action drawSimulation;
    }

    // Everything connected to one advisor (or "Shared"): its HTN parameters — each collapsible,
    // each showing a live simulation, the weights that decide it, and the weights that merely
    // react to it once decided. Every scoring weight in the game ends up under exactly one of
    // these two lists on exactly one HTN Parameter — there is no leftover "unconnected" bucket.
    // No card list here anymore either (Card Board owns advisor assignment).
    private void DrawAdvisorProfile(string advisorGroup)
    {
        EnsureAdvisorStyles();
        Enum.TryParse(advisorGroup, out AdvisorType advisor); // AdvisorType.None if "Shared" or unparseable

        scenarioFoldout = EditorGUILayout.Foldout(scenarioFoldout, "Scenario inputs (used by every simulation below)", true, EditorStyles.foldoutHeader);
        if (scenarioFoldout) DrawScenarioInputs();
        EditorGUILayout.Space(14f);

        DrawCardUtilityScoreSimulation();
        EditorGUILayout.Space(10f);

        List<HtnParamGroup> groups = BuildHtnParamGroups(advisorGroup, advisor);
        foreach (HtnParamGroup group in groups) DrawHtnParamFoldout(advisorGroup, group);

        List<AdvisorWeightDefinition> other = GetOtherWeights(advisorGroup);
        if (other.Count > 0) DrawOtherWeightsFoldout(advisorGroup, other);
    }

    // The score preview intentionally begins at zero. It only gains utility
    // when the selected Card Board entry explicitly lists a parameter.
    private void DrawCardUtilityScoreSimulation()
    {
        cardsByActionRef ??= BuildCardUsageMap();
        List<(string cardName, string actionClass, string deckId, int cardId)> cards = cardsByActionRef
            .SelectMany(pair => pair.Value.Select(card => (card.cardName, pair.Key, card.deckId, card.cardId)))
            .OrderBy(card => card.cardName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(card => card.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cards.Count == 0) return;
        string[] names = new[] { "(No card selected — utility = 0)" }
            .Concat(cards.Select(card => $"{card.cardName}  [{card.Item2}]")).ToArray();
        simUtilityActionIndex = EditorGUILayout.Popup("Card utility simulation", Mathf.Clamp(simUtilityActionIndex, 0, names.Length - 1), names);
        if (simUtilityActionIndex == 0)
        {
            EditorGUILayout.HelpBox("No card selected: Advisor utility contribution is exactly 0.", MessageType.None);
            return;
        }

        (string cardName, string actionClass, string deckId, int cardId) = cards[simUtilityActionIndex - 1];
        string cardKey = AIAdvisorConfig.BuildCardProfileKey(deckId, cardId);
        if (!cardProfiles.TryGetValue(cardKey, out CardAdvisorProfile profile) || profile.utilityParameters == null || profile.utilityParameters.Count == 0)
        {
            EditorGUILayout.HelpBox($"{cardName} has no Card Board utility parameters: Advisor utility contribution is exactly 0.", MessageType.None);
            return;
        }
        List<ActionUtilityParameterModifier> modifiers = profile.utilityParameters;

        float total = 0f;
        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        foreach (ActionUtilityParameterModifier modifier in modifiers)
        {
            float raw = SimulateUtilityParameter(modifier.parameter);
            float contribution = raw * modifier.multiplier + modifier.bonus;
            total += contribution;
            EditorGUILayout.LabelField($"{modifier.parameter}: {raw:0.##} × {modifier.multiplier:0.##} + {modifier.bonus:0.##} = {contribution:0.##}", EditorStyles.miniLabel);
        }
        EditorGUILayout.LabelField($"Card utility total: {total:0.##}", weightLabelStyle);
        EditorGUILayout.EndVertical();
    }

    private float SimulateUtilityParameter(string parameter)
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float value) ? value : AIAdvisorConfig.GetDefaultWeight(key);
        float enemyPressure = Mathf.Max(0f, W(K.EnemyProximityMax) - simEnemyDistance);
        return parameter switch
        {
            AIUtilityParameters.MilitaristicEnemyPressure => enemyPressure,
            AIUtilityParameters.MilitaristicMilitaryEdge => !simLeadingArmy ? W(K.NoArmyPenalty) : MilitaristicEdge(W),
            AIUtilityParameters.EconomicLiquidWealth => simGoldBuffer + simResourceNetWorth,
            AIUtilityParameters.DiplomaticNpcDiscovery => Mathf.Max(0f, W(K.NpcProximityMax) - simNpcDistance),
            AIUtilityParameters.DiplomaticIndirectSafety => SimulatedOutmatched() ? W(K.DiplomaticOutmatchedBonus) : 0f,
            AIUtilityParameters.DiplomaticEnemyPressure => enemyPressure,
            AIUtilityParameters.DiplomaticEmissaryStrength => simEmissary,
            AIUtilityParameters.IntelligenceEnemyCharacter => Mathf.Max(0f, W(K.EnemyCharacterProximityMax) - simEnemyCharacterDistance),
            AIUtilityParameters.IntelligenceIndirectSafety => SimulatedOutmatched() ? W(K.IntelligenceOutmatchedBonus) : 0f,
            AIUtilityParameters.IntelligenceEnemyPressure => enemyPressure,
            AIUtilityParameters.IntelligenceAgentStrength => simAgent,
            AIUtilityParameters.MagicArtifactScarcity => (1f - Mathf.Clamp01(simArtifactShare)) * W(K.ArtifactScarcityWeight),
            AIUtilityParameters.MagicArtifactTransfer => 0f,
            AIUtilityParameters.MagicEnemyPressure => enemyPressure,
            AIUtilityParameters.MagicHiddenArtifacts => simHiddenArtifacts,
            AIUtilityParameters.MagicMageStrength => simMage,
            AIUtilityParameters.MovementReachNpc => Mathf.Max(0f, W(K.MovementProximityMax) - simNpcDistance * W(K.MovementDistancePenaltyPerHex)),
            AIUtilityParameters.MovementInterceptEnemy => Mathf.Max(0f, W(K.MovementProximityMax) - simEnemyDistance * W(K.MovementDistancePenaltyPerHex)),
            AIUtilityParameters.MovementReachEnemyCharacter => Mathf.Max(0f, W(K.MovementProximityMax) - simEnemyCharacterDistance * W(K.MovementDistancePenaltyPerHex)),
            AIUtilityParameters.MagicSpellOpportunity => simSpellsAvailable,
            AIUtilityParameters.MilitaristicOwnPcFortificationNeed => Mathf.Max(0f, W(K.MilitaristicOwnPcFortificationProximityMax) - simOwnPcFortificationDistance),
            AIUtilityParameters.DiplomaticNplRecruitment => Mathf.Max(0f, W(K.DiplomaticNplRecruitmentProximityMax) - simNplRecruitmentDistance),
            _ => 0f
        };
    }

    // Hand-mapped: which HTN predicates does this advisor drive, and which weight keys does
    // each predicate's real formula actually read? See AIContext.GetAdvisorViability,
    // AIContext.IsEnemyNear/IsOutmatched, and HTNRegistry's predicate lambdas for the source
    // of truth this mirrors.
    private List<HtnParamGroup> BuildHtnParamGroups(string advisorGroup, AdvisorType advisor)
    {
        List<HtnParamGroup> groups = new();

        if (string.Equals(advisorGroup, "Shared", StringComparison.OrdinalIgnoreCase))
        {
            groups.Add(new HtnParamGroup
            {
                title = "Global.HTNBiasBonus",
                description = "The one Shared weight with a real HTN connection: a flat bonus added to every card "
                    + "whose advisor matches the HTN's currently-active task's advisor. Everything else Shared owns "
                    + "(Base Score, Difficulty Divisor, Max Difficulty Penalty, Cost Pressure When Poor) is pure "
                    + "card-scoring math with no HTN involvement at all — see \"Other scoring weights\" below.",
                weightKeys = new[] { K.HTNBiasBonus },
                drawSimulation = DrawHtnBiasSimulation
            });
            return groups;
        }

        switch (advisor)
        {
            case AdvisorType.Militaristic:
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.EnemyNear",
                    description = "True when the nearest non-neutral enemy PC or army is within range — proximity alone, independent of who'd win a fight.",
                    weightKeys = new[] { K.EnemyProximityMax },
                    drawSimulation = () => DrawBooleanSimulation("Militaristic.EnemyNear", SimulatedEnemyNear())
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.Danger",
                    description = "True when an enemy is near AND that enemy outguns this leader's army — the top-priority Method in the default HTN strategy, checked above even economic recovery.",
                    weightKeys = new[] { K.EnemyProximityMax, K.OutmatchedStrengthRatio },
                    drawSimulation = () => DrawCompositeBooleanSimulation("Militaristic.Danger",
                        ("EnemyNear", SimulatedEnemyNear()), ("Outmatched", SimulatedOutmatched()))
                });
                AddDirectUtilityGroup(groups, "Militaristic.OwnPcFortificationNeed", "Militaristic.OwnPcFortificationNeedReady", "Proximity to the nearest own PC whose PC.GetDefense() is below Militaristic.OwnPcDefenseBelow — needs fortifying. Gates root.offense.pick.fortify in the default HTN strategy.",
                    K.MilitaristicOwnPcDefenseBelow, K.MilitaristicOwnPcFortificationProximityMax, K.MilitaristicOwnPcFortificationNeedThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.Viable",
                    description = "True when Militaristic's viability (enemy proximity + army edge) is above its threshold — the HTN switches to an offense Method.",
                    weightKeys = new[] { K.EnemyProximityMax, K.NoArmyPenalty, K.FarTargetPenalty, K.MilitaristicViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Militaristic", AdvisorType.Militaristic)
                });
                break;

            case AdvisorType.Economic:
                groups.Add(new HtnParamGroup
                {
                    title = "Economic Tier (Critical / Weak / Stable / Surplus)",
                    description = "Exactly one tier is ever true at once, decided by liquid wealth (gold + resources at current market sell price — this game has no per-turn income of any kind). Which Methods these tiers gate (e.g. root.recover) is authored in the Strategies tab, not duplicated here — Economic has no Utility viability formula of its own to simulate.",
                    weightKeys = new[] { K.EconomyCriticalBelow, K.EconomyWeakBelow, K.EconomyStableBelow },
                    drawSimulation = DrawEconomicTierSimulation
                });
                break;

            case AdvisorType.Diplomatic:
                AddDirectUtilityGroup(groups, "Diplomatic.NpcDiscovery", "Diplomatic.NpcDiscoveryReady", "Nearest unrevealed NPC proximity. HTN reads this direct Advisor value; it is not part of a hidden aggregate.",
                    K.NpcProximityMax, K.DiplomaticNpcDiscoveryThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.IndirectSafety", "Diplomatic.IndirectSafetyReady", "Outmatched-response value. It is either zero or Diplomatic.OutmatchedBonus, using the shared outmatched definition.",
                    K.OutmatchedStrengthRatio, K.DiplomaticOutmatchedBonus, K.DiplomaticIndirectSafetyThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.EnemyPcOpportunity", "Diplomatic.EnemyPcOpportunityReady", "Proximity to the nearest enemy-owned PC whose loyalty is below Diplomatic.EnemyPcLoyaltyBelow — an influence-out target. Gates root.diplomacy.pick.flip in the default HTN strategy.",
                    K.DiplomaticEnemyPcLoyaltyBelow, K.DiplomaticEnemyPcOpportunityProximityMax, K.DiplomaticEnemyPcOpportunityThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.OwnPcLoyaltyRisk", "Diplomatic.OwnPcLoyaltyRiskReady", "Proximity to the nearest own PC whose loyalty is below Diplomatic.OwnPcLoyaltyBelow — needs influencing up. Gates root.diplomacy.pick.shore in the default HTN strategy.",
                    K.DiplomaticOwnPcLoyaltyBelow, K.DiplomaticOwnPcLoyaltyRiskProximityMax, K.DiplomaticOwnPcLoyaltyRiskThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.NplRecruitment", "Diplomatic.NplRecruitmentReady", "Proximity to the nearest NPL capital currently eligible for StateAllegiance (AFriendOrThree) recruitment — same eligibility gate the card itself uses (alignment match + capital's PC card already played), not a fabricated relationship counter. Gates root.diplomacy.pick.recruit in the default HTN strategy.",
                    K.DiplomaticNplRecruitmentProximityMax, K.DiplomaticNplRecruitmentThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Diplomatic.Viable",
                    description = "True when Diplomatic's viability (NPC proximity + outmatched bonus) is above its threshold.",
                    weightKeys = new[] { K.NpcProximityMax, K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.OutmatchedStrengthRatio, K.DiplomaticOutmatchedBonus, K.DiplomaticEnemyPressureWeight, K.DiplomaticEmissaryStrengthWeight, K.DiplomaticViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Diplomatic", AdvisorType.Diplomatic)
                });
                break;

            case AdvisorType.Intelligence:
                AddDirectUtilityGroup(groups, "Intelligence.EnemyCharacter", "Intelligence.EnemyCharacterReady", "Nearest enemy-character proximity, published directly by the Intelligence Advisor.",
                    K.EnemyCharacterProximityMax, K.IntelligenceEnemyCharacterThreshold);
                AddDirectUtilityGroup(groups, "Intelligence.IndirectSafety", "Intelligence.IndirectSafetyReady", "Outmatched-response value. It is either zero or Intelligence.OutmatchedBonus, using the shared outmatched definition.",
                    K.OutmatchedStrengthRatio, K.IntelligenceOutmatchedBonus, K.IntelligenceIndirectSafetyThreshold);
                AddDirectUtilityGroup(groups, "Intelligence.EnemyPcVulnerability", "Intelligence.EnemyPcVulnerabilityReady", "Proximity to the nearest enemy-owned PC whose PC.GetDefense() is below Intelligence.EnemyPcDefenseBelow — a sabotage/theft target. Gates root.intelligence.pick.sabotage in the default HTN strategy.",
                    K.IntelligenceEnemyPcDefenseBelow, K.IntelligenceEnemyPcVulnerabilityProximityMax, K.IntelligenceEnemyPcVulnerabilityThreshold);
                AddDirectUtilityGroup(groups, "Intelligence.HighValueEnemyCharacter", "Intelligence.HighValueEnemyCharacterReady", "Proximity to the nearest enemy character whose Commander+Agent+Emmissary+Mage sum is at least Intelligence.HighValueSkillAtLeast — an assassination/kidnap target. Gates root.intelligence.pick.highvalue in the default HTN strategy.",
                    K.IntelligenceHighValueSkillAtLeast, K.IntelligenceHighValueEnemyCharacterProximityMax, K.IntelligenceHighValueEnemyCharacterThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Intelligence.Viable",
                    description = "True when Intelligence's viability (enemy-character proximity + outmatched bonus) is above its threshold.",
                    weightKeys = new[]
                    {
                        K.EnemyCharacterProximityMax, K.EnemyProximityMax, K.NeutralTargetExtraDistance,
                        K.OutmatchedStrengthRatio, K.IntelligenceOutmatchedBonus, K.IntelligenceEnemyPressureWeight, K.IntelligenceAgentStrengthWeight, K.IntelligenceViabilityThreshold
                    },
                    drawSimulation = () => DrawViableSimulation("Intelligence", AdvisorType.Intelligence)
                });
                break;

            case AdvisorType.Magic:
                AddDirectUtilityGroup(groups, "Magic.ArtifactScarcity", "Magic.ArtifactScarcityReady", "Nation artifact scarcity multiplied by Magic.ArtifactScarcityWeight.",
                    K.ArtifactScarcityWeight, K.MagicArtifactScarcityThreshold);
                AddDirectUtilityGroup(groups, "Magic.ArtifactTransfer", "Magic.ArtifactTransferReady", "Best legal artifact-transfer opportunity published by the Magic Advisor.",
                    K.MagicArtifactTransferThreshold);
                AddDirectUtilityGroup(groups, "Magic.EnemyPressure", "Magic.EnemyPressureReady", "Enemy proximity, including the shared neutral-target adjustment.",
                    K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.MagicEnemyPressureThreshold);
                AddDirectUtilityGroup(groups, "Magic.SpellOpportunity", "Magic.SpellOpportunityReady", "Count of Spell-derived actions this character can currently play (AvailableActions.Count(a => a is Spell)) — the same 'is there a legal opportunity of this shape' gate Magic.ArtifactTransfer uses, published as a count instead of a bool. Gates root.magic.pick.cast in the default HTN strategy.",
                    K.MagicSpellOpportunityThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Magic.Viable",
                    description = "True when Magic's viability (artifact scarcity + enemy proximity) is above its threshold.",
                    weightKeys = new[] { K.ArtifactScarcityWeight, K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.MagicHiddenArtifactsWeight, K.MagicMageStrengthWeight, K.MagicViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Magic", AdvisorType.Magic)
                });
                break;

            case AdvisorType.Movement:
                AddDirectUtilityGroup(groups, "Movement.ReachNpc", "Movement.ReachNpcReady", "Distance to the nearest unrevealed NPC destination.",
                    K.MovementProximityMax, K.MovementDistancePenaltyPerHex, K.MovementReachNpcThreshold);
                AddDirectUtilityGroup(groups, "Movement.InterceptEnemy", "Movement.InterceptEnemyReady", "Distance to the closest enemy destination.",
                    K.MovementProximityMax, K.MovementDistancePenaltyPerHex, K.MovementInterceptEnemyThreshold);
                AddDirectUtilityGroup(groups, "Movement.ReachEnemyCharacter", "Movement.ReachEnemyCharacterReady", "Distance to the nearest enemy character destination.",
                    K.MovementProximityMax, K.MovementDistancePenaltyPerHex, K.MovementReachEnemyCharacterThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Movement.Viable",
                    description = "True when Movement's viability (proximity to the preferred destination) is above its threshold.",
                    weightKeys = new[] { K.MovementProximityMax, K.MovementDistancePenaltyPerHex, K.MovementViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Movement", AdvisorType.Movement)
                });
                break;
        }

        return groups;
    }

    private static void AddDirectUtilityGroup(List<HtnParamGroup> groups, string parameter, string predicate, string formula, params string[] weightKeys)
    {
        groups.Add(new HtnParamGroup
        {
            title = parameter,
            description = $"HTN predicate: {predicate}. Formula: {formula} Threshold is explicit and shown below; Card Board may separately apply this same parameter using its visible multiplier + bonus profile.",
            weightKeys = weightKeys
        });
    }

    // Union of every weight key referenced by ANY advisor's HTN parameters — computed fresh
    // each call (cheap: ~30 keys, only runs while the "leftover" check is being drawn) so a
    // weight only ever shows up as uncategorized if it's truly unaccounted-for everywhere.
    // In the normal case this covers every known weight — there is no other role left for
    // a weight to play (see HtnParamGroup).
    private HashSet<string> AllHtnConnectedWeightKeys()
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in AdvisorProfileNames)
        {
            Enum.TryParse(name, out AdvisorType adv);
            foreach (HtnParamGroup group in BuildHtnParamGroups(name, adv))
                foreach (string key in group.weightKeys) keys.Add(key);
        }
        return keys;
    }

    private List<AdvisorWeightDefinition> GetOtherWeights(string advisorGroup)
    {
        HashSet<string> connected = AllHtnConnectedWeightKeys();
        return AIAdvisorConfig.KnownWeights
            .Where(d => string.Equals(AdvisorGroupForWeightKey(d.key), advisorGroup, StringComparison.OrdinalIgnoreCase)
                && !connected.Contains(d.key))
            .ToList();
    }

    private void DrawHtnParamFoldout(string advisorGroup, HtnParamGroup group)
    {
        EnsureAdvisorStyles();
        string foldoutKey = advisorGroup + ":" + group.title;
        bool expanded = htnParamFoldouts.TryGetValue(foldoutKey, out bool e) && e;

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        bool nowExpanded = EditorGUILayout.Foldout(expanded, group.title, true, EditorStyles.foldoutHeader);
        if (nowExpanded != expanded) htnParamFoldouts[foldoutKey] = nowExpanded;

        if (nowExpanded)
        {
            EditorGUILayout.LabelField(group.description, weightDescStyle);
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Simulation", conditionKeyStyle);
            group.drawSimulation?.Invoke();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Params involved", conditionKeyStyle);
            foreach (string key in group.weightKeys)
            {
                AdvisorWeightDefinition definition = AIAdvisorConfig.KnownWeights
                    .FirstOrDefault(d => string.Equals(d.key, key, StringComparison.OrdinalIgnoreCase));
                if (definition != null) DrawWeightRow(definition);
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    // Skill-affinity (a flat bonus per card based on the playing character's own skill,
    // unrelated to world-state) used to live here — removed from AIContext.ScoreAction
    // entirely, since it structurally can't connect to any HTN predicate (HTN reads
    // situations, not a character's innate stats) and that's now a hard requirement: every
    // scoring weight either gates an HTN condition or shows up here as a rare, clearly-labeled
    // exception (currently just Shared's difficulty-penalty shape, which has no per-advisor
    // equivalent to speak of).
    private void DrawOtherWeightsFoldout(string advisorGroup, List<AdvisorWeightDefinition> weights)
    {
        EnsureAdvisorStyles();
        string foldoutKey = advisorGroup + ":__other";
        bool expanded = htnParamFoldouts.TryGetValue(foldoutKey, out bool e) && e;

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        bool nowExpanded = EditorGUILayout.Foldout(expanded, "Other scoring weights (no HTN connection)", true, EditorStyles.foldoutHeader);
        if (nowExpanded != expanded) htnParamFoldouts[foldoutKey] = nowExpanded;

        if (nowExpanded)
        {
            EditorGUILayout.LabelField(
                "These affect card scores but don't gate any HTN condition — pure card-scoring math (difficulty "
                + "penalty shape) with no natural world-state connection to make HTN-legible.",
                weightDescStyle);
            EditorGUILayout.Space(4f);
            foreach (AdvisorWeightDefinition definition in weights) DrawWeightRow(definition);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    // --- Simulation renderers, one per predicate shape --------------------------------------

    private void DrawBooleanSimulation(string label, bool value)
    {
        EnsureAdvisorStyles();
        EditorGUILayout.BeginHorizontal();
        DrawStatusBadge($"{label} = {(value ? "TRUE" : "FALSE")}", value, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCompositeBooleanSimulation(string label, params (string name, bool value)[] parts)
    {
        EnsureAdvisorStyles();
        EditorGUILayout.BeginHorizontal();
        foreach ((string name, bool value) in parts) DrawConditionBadge($"{name} = {(value ? "TRUE" : "FALSE")}", value);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        bool overall = parts.All(p => p.value);
        EditorGUILayout.BeginHorizontal();
        DrawStatusBadge($"{label} = {(overall ? "TRUE" : "FALSE")}", overall, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawViableSimulation(string advisorGroup, AdvisorType advisor)
    {
        EnsureAdvisorStyles();
        string thresholdKey = ViabilityThresholdKeyFor(advisor);
        float viability = SimulateViability(advisor);
        float threshold = thresholdKey != null
            ? (advisorWeights.TryGetValue(thresholdKey, out float t) ? t : AIAdvisorConfig.GetDefaultWeight(thresholdKey))
            : 0f;
        bool viable = viability > threshold;

        EditorGUILayout.BeginHorizontal();
        DrawMetricTile("Viability", viability.ToString("0.0"));
        DrawMetricTile("Threshold", threshold.ToString("0.0"));
        DrawStatusBadge($"{advisorGroup}.Viable = {(viable ? "TRUE" : "FALSE")}", viable, GUILayout.MinWidth(180f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEconomicTierSimulation()
    {
        EnsureAdvisorStyles();
        EconomyStatus tier = SimulatedEconomyStatus();
        EditorGUILayout.LabelField($"Economic tier right now: {tier}", weightLabelStyle);
        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        DrawConditionBadge("Economic.Critical", EvaluatePredicateLive("Economic.Critical"));
        DrawConditionBadge("Economic.Weak", EvaluatePredicateLive("Economic.Weak"));
        DrawConditionBadge("Economic.Stable", EvaluatePredicateLive("Economic.Stable"));
        DrawConditionBadge("Economic.Surplus", EvaluatePredicateLive("Economic.Surplus"));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHtnBiasSimulation()
    {
        EnsureAdvisorStyles();
        actionCatalog ??= BuildActionCatalog();
        cardsByActionRef ??= BuildCardUsageMap();
        string[] biasOptions = Enum.GetNames(typeof(AdvisorType));

        EditorGUILayout.LabelField(new GUIContent("HTN bias preview", "Simulate the HTN tab's currently-active task biasing scoring toward this advisor."));
        simBiasedAdvisorIndex = EditorGUILayout.Popup(Mathf.Clamp(simBiasedAdvisorIndex, 0, biasOptions.Length - 1), biasOptions, GUILayout.Width(160f));

        string biasedName = biasOptions[Mathf.Clamp(simBiasedAdvisorIndex, 0, biasOptions.Length - 1)];
        bool active = !string.Equals(biasedName, nameof(AdvisorType.None), StringComparison.OrdinalIgnoreCase);
        float bonus = advisorWeights.TryGetValue(AIAdvisorConfig.Keys.HTNBiasBonus, out float b)
            ? b : AIAdvisorConfig.GetDefaultWeight(AIAdvisorConfig.Keys.HTNBiasBonus);
        int affectedCount = active && Enum.TryParse(biasedName, out AdvisorType parsedBias)
            ? AllCardBoardCards().Count(e => ResolvedAdvisorFor(AIAdvisorConfig.BuildCardProfileKey(e.card.deckId, e.card.cardId), e.defaultAdvisor) == parsedBias)
            : 0;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        DrawStatusBadge(
            active ? $"+{bonus:0.#} to {affectedCount} {biasedName} card(s)" : "No bias set — affects nothing right now",
            active, GUILayout.MinWidth(240f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
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
        simHiddenArtifacts = DrawStatField("Hidden artifacts", simHiddenArtifacts, width: 110f);
        simSpellsAvailable = DrawStatField("Spells available", simSpellsAvailable, "How many Spell actions are currently playable by this character.", width: 110f);
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
        simResourceNetWorth = DrawStatField("Resources (net worth)", simResourceNetWorth,
            "Gold-equivalent value of held leather/mounts/timber/iron/steel/mithril at current market sell price "
            + "(StoresManager) — this game has no per-turn income of any kind, so liquid wealth is gold + this.",
            width: 150f);
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
        EditorGUILayout.LabelField("Distances (hexes) — 0 = adjacent/right here, high = far or none at all", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simEnemyDistance = DrawStatFieldF("Enemy PC / army", simEnemyDistance, "Hexes to the nearest enemy PC or army. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.EnemyProximityMax));
        simEnemyCharacterDistance = DrawStatFieldF("Enemy character", simEnemyCharacterDistance, "Hexes to the nearest enemy character. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.EnemyCharacterProximityMax));
        simNpcDistance = DrawStatFieldF("Unrevealed NPC", simNpcDistance, "Hexes to the nearest unrevealed NPC. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.NpcProximityMax));
        simDestinationDistance = DrawStatFieldF("Move destination", simDestinationDistance, "Hexes to the preferred movement destination. 0 = arrived.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.MovementProximityMax, AIAdvisorConfig.Keys.MovementDistancePenaltyPerHex));
        simOwnPcFortificationDistance = DrawStatFieldF("Own PC needing fort", simOwnPcFortificationDistance, "Hexes to the nearest own PC below Militaristic.OwnPcDefenseBelow. Use 99 for none.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.MilitaristicOwnPcFortificationProximityMax));
        simNplRecruitmentDistance = DrawStatFieldF("NPL recruitment-ready", simNplRecruitmentDistance, "Hexes to the nearest NPL capital eligible for StateAllegiance recruitment. Use 99 for none.",
            hint: d => ProximityHint(d, AIAdvisorConfig.Keys.DiplomaticNplRecruitmentProximityMax));
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

    private float DrawStatFieldF(string caption, float value, string tooltip = null, float width = 120f, Func<float, string> hint = null)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(new GUIContent(caption, tooltip), EditorStyles.miniLabel);
        float result = EditorGUILayout.FloatField(value, GUILayout.Width(width), GUILayout.Height(20f));
        if (hint != null)
        {
            GUIStyle hintStyle = new(EditorStyles.miniLabel) { wordWrap = true, fontStyle = FontStyle.Italic };
            GUILayout.Label(hint(result), hintStyle, GUILayout.Width(width));
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(10f);
        return result;
    }

    // Live-computed so the field's polarity (0 = adjacent/closest possible, high = far or
    // none) is unmissable without hunting for a tooltip — this is exactly the confusion a
    // "0 enemies nearby" reading of a 0-hex-distance field caused.
    private string ProximityHint(float distance, string proximityMaxKey, string penaltyPerHexKey = null)
    {
        float max = advisorWeights.TryGetValue(proximityMaxKey, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(proximityMaxKey);
        float penalty = penaltyPerHexKey != null
            ? (advisorWeights.TryGetValue(penaltyPerHexKey, out float p) ? p : AIAdvisorConfig.GetDefaultWeight(penaltyPerHexKey))
            : 1f;
        if (distance <= 0f) return "= right here (closest possible)";
        float fadesAt = penalty > 0f ? max / penalty : max;
        return distance * penalty < max
            ? $"in range — fades to 0 by {fadesAt:0.#}"
            : "out of range — 0 bonus (too far / none)";
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

    // Economy status derived from liquid wealth (Gold + Resources), using the CURRENT
    // (possibly unsaved) threshold weights — mirrors AIAdvisorConfig.EvaluateEconomyStatus.
    private EconomyStatus SimulatedEconomyStatus()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);
        float liquidWealth = simGoldBuffer + simResourceNetWorth;

        if (liquidWealth < W(AIAdvisorConfig.Keys.EconomyCriticalBelow)) return EconomyStatus.Critical;
        if (liquidWealth < W(AIAdvisorConfig.Keys.EconomyWeakBelow)) return EconomyStatus.Weak;
        if (liquidWealth < W(AIAdvisorConfig.Keys.EconomyStableBelow)) return EconomyStatus.Stable;
        return EconomyStatus.Surplus;
    }

    private string EconomyThresholdsTooltip()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : AIAdvisorConfig.GetDefaultWeight(key);
        float liquidWealth = simGoldBuffer + simResourceNetWorth;

        return $"Derived from Gold + Resources = {liquidWealth:0.#} liquid wealth — this game has no per-turn income of any kind.\n"
            + $"Critical: below {W(AIAdvisorConfig.Keys.EconomyCriticalBelow):0.#}\n"
            + $"Weak: below {W(AIAdvisorConfig.Keys.EconomyWeakBelow):0.#}\n"
            + $"Stable: below {W(AIAdvisorConfig.Keys.EconomyStableBelow):0.#}\n"
            + "Surplus: anything above.\n"
            + "Edit these thresholds in the 'Economic' tab's HTN Parameter.";
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
            // Economic has no formula: its old situational term was the now-removed
            // tier-reactive Bonus weights, which decided nothing — matches
            // AIContext.GetAdvisorViability's Economic-less switch exactly.
            AdvisorType.Militaristic => enemyProximity + (!simLeadingArmy
                ? W(AIAdvisorConfig.Keys.NoArmyPenalty)
                : simEnemyStrength > 0
                    ? MilitaristicEdge(W)
                    : 0f),
            AdvisorType.Intelligence => Mathf.Max(0f, W(AIAdvisorConfig.Keys.EnemyCharacterProximityMax) - simEnemyCharacterDistance)
                + enemyProximity
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

    private void DrawMetricTile(string caption, string value, string tooltip = null)
    {
        EditorGUILayout.BeginVertical(weightRowBoxStyle, GUILayout.Width(110f));
        EditorGUILayout.LabelField(value, metricValueStyle);
        EditorGUILayout.LabelField(new GUIContent(caption, tooltip), metricCaptionStyle);
        EditorGUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    // Fixed-height rect, drawn directly — no BeginVertical/FlexibleSpace wrapper. That
    // centering trick only behaves when this sits beside a taller sibling that bounds the
    // row's height (e.g. DrawMetricTile boxes in DrawViableSimulation); called alone — as
    // DrawHtnBiasSimulation and DrawBooleanSimulation do — the unbounded FlexibleSpace had
    // nothing to size against and expanded to fill the rest of the page.
    // label is shown verbatim — no implicit " = TRUE/FALSE" suffix, since not every caller
    // is stating a predicate (DrawHtnBiasSimulation's is a full sentence); callers that want
    // the suffix add it themselves.
    private void DrawStatusBadge(string label, bool positive, params GUILayoutOption[] options)
    {
        Rect r = GUILayoutUtility.GetRect(0, 40f, options);
        EditorGUI.DrawRect(r, positive ? BadgeTrueColor : BadgeFalseColor);
        GUI.Label(r, label, badgeStyle);
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

                // Keyed by deckId::cardId, not card.name — a display name is not guaranteed
                // unique across decks, but this identity is (mirrors AIAdvisorConfig.BuildCardProfileKey).
                string cardKey = AIAdvisorConfig.BuildCardProfileKey(deck.deckId, card.cardId);
                if (!string.IsNullOrEmpty(cardKey) && !set.ContainsKey(cardKey))
                {
                    string effect = card.GetActionEffectText();
                    set[cardKey] = new CardUsage
                    {
                        cardName = card.name,
                        effect = string.IsNullOrWhiteSpace(effect) ? string.Empty : Regex.Replace(effect, "<[^>]+>", string.Empty).Trim(),
                        difficulty = Mathf.Max(0, card.difficulty),
                        goldCost = Mathf.Max(0, card.goldRequired),
                        deckId = deck.deckId,
                        cardId = card.cardId
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
        cardProfiles.Clear();
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
                if (data?.cardProfiles != null)
                {
                    foreach (CardAdvisorProfile entry in data.cardProfiles)
                    {
                        if (entry == null) continue;
                        string key = AIAdvisorConfig.BuildCardProfileKey(entry.deckId, entry.cardId);
                        if (string.IsNullOrEmpty(key)) continue;

                        List<ActionUtilityParameterModifier> valid = entry.utilityParameters
                            ?.Where(p => p != null && AIUtilityParameters.IsKnown(p.parameter))
                            .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                            .ToList() ?? new List<ActionUtilityParameterModifier>();

                        CardAdvisorProfile profile = new()
                        {
                            deckId = entry.deckId,
                            cardId = entry.cardId,
                            cardName = entry.cardName,
                            actionClass = entry.actionClass,
                            advisor = entry.advisor,
                            scoreBonus = entry.scoreBonus,
                            ignoreSituation = entry.ignoreSituation,
                            utilityParameters = valid
                        };
                        if (!IsProfileEmpty(profile)) cardProfiles[key] = profile;
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
            cardProfiles = cardProfiles.Values
                .OrderBy(p => p.deckId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.cardId)
                .Select(p => new CardAdvisorProfile
                {
                    deckId = p.deckId,
                    cardId = p.cardId,
                    cardName = p.cardName,
                    actionClass = p.actionClass,
                    advisor = p.advisor,
                    scoreBonus = p.scoreBonus,
                    ignoreSituation = p.ignoreSituation,
                    utilityParameters = p.utilityParameters?
                        .Select(m => new ActionUtilityParameterModifier { parameter = m.parameter, multiplier = m.multiplier, bonus = m.bonus })
                        .ToList() ?? new List<ActionUtilityParameterModifier>()
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
