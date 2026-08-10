using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using K = UtilityAI.Keys;

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

    private static readonly string[] TabLabels = { "Situations", "HTN", "Utility AI", "Card Board", "NN" };

    private const string PriorityAssetPath = "Assets/Resources/" + SituationEvaluator.PriorityResourcePath + ".json";
    private const string StrategiesAssetPath = "Assets/Resources/" + AIStrategyLibrary.ResourcePath + ".json";
    private const string AdvisorAssetPath = "Assets/Resources/" + UtilityAI.ResourcePath + ".json";

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

    // Built fresh per call rather than cached on EnsureAdvisorStyles — that cache is guarded by
    // a single "already initialized" check on an unrelated field, which silently skips newly
    // added styles when domain reload is disabled and an old AIWidgetWindow instance survives a
    // recompile (this bit us once: a null GUIStyle mid-OnGUI corrupts the whole layout pass).
    private static GUIStyle BuildWarningLabelStyle() =>
        new(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.85f, 0.45f, 0.35f) } };

    // Live TRUE/FALSE for any HTNRegistry predicate key, under the current "Live effect"
    // scenario inputs — the same signal HTNRegistry's own lambda would return at runtime,
    // computed via the widget's Simulated* mirrors since there's no live UtilityAIContext here.
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

        // Every remaining predicate follows the "<Group>.Viable" pattern.
        string groupPart = key.Contains('.') ? key.Split('.')[0] : key;
        string thresholdKey = ViabilityThresholdKeyFor(groupPart);
        if (thresholdKey != null)
        {
            float threshold = advisorWeights.TryGetValue(thresholdKey, out float t) ? t : UtilityAI.GetDefaultWeight(thresholdKey);
            return SimulateViability(groupPart) > threshold;
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
    // Keyed by UtilityAI.BuildCardProfileKey(deckId, cardId) — one independent row per
    // printed card. Cards sharing an action class each get their own entry (see
    // DuplicateProfileToSiblingCards for the "seed one from another" authoring shortcut).
    private readonly Dictionary<string, CardParameterProfile> cardProfiles = new(StringComparer.OrdinalIgnoreCase);
    private List<string> actionCatalog;
    private Dictionary<string, List<CardUsage>> cardsByActionRef;
    private bool advisorsDirty;
    private int simBiasedGroupIndex; // index into ParameterGroups; 0 = Unassigned
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
    private int simUtilityActionIndex;
    private bool simLeadingArmy;
    private bool simHostageToRescue;
    private bool simHoldingHostage;
    private int simGoldBuffer = 50;
    private int simResourceNetWorth = 20;
    private int simMithrilAmount = 10;
    private int simSteelAmount = 10;
    private int simIronAmount = 10;
    private int simMountsAmount = 10;
    private int simTimberAmount = 10;
    private int simLeatherAmount = 10;
    // This leader's deck-required material distribution (NationBlackboard in the real
    // game) — what share of the deck's total material cost each one makes up, 0..1. Defaults to
    // an even 1/6 split; tweak these to preview how a mithril-heavy or timber-light deck biases
    // Buy/Sell scoring differently than the flat default.
    private float simMithrilShare = 1f / 6f;
    private float simSteelShare = 1f / 6f;
    private float simIronShare = 1f / 6f;
    private float simMountsShare = 1f / 6f;
    private float simTimberShare = 1f / 6f;
    private float simLeatherShare = 1f / 6f;
    private int simMyArmyStrength = 100;
    private int simEnemyStrength = 100;
    private float simEnemyDistance = 5f;
    private float simEnemyCharacterDistance = 5f;
    private float simNpcDistance = 5f;
    private float simDestinationDistance = 3f;
    private float simArtifactShare = 0.25f;
    private float simOwnPcFortificationDistance = 99f;
    private float simNplRecruitmentDistance = 99f;
    private int simWoundedAllies;
    // Direct signed margins (this character's estimated score minus the best eligible
    // opponent's), same "direct slider" shape as simArtifactShare above — Duel/BattleOfSongs
    // scoring depends on several skills + artifact bonuses on both sides, not worth
    // reconstructing from primitive inputs here.
    private float simDuelAdvantage;
    private float simSongDuelAdvantage;
    private int simUnrecruitedNplCount = 2;

    private class CardUsage
    {
        public string cardName;
        public string cardType; // CardData.type — Action/Event/Spell/PC/Land/Environmental
        public string effect;
        public int difficulty;
        public int goldCost;
        public string deckId;
        public int cardId;
    }

    // Card Board tab — a browsable, filterable list of every printed card, writing directly to
    // cardProfiles (the same dictionary Save/Load round-trips to UtilityAI.json). A card has no
    // single "advisor"/type-like tag driving membership here — the bucket bar below groups by
    // the card's own real CardData.type (Action/Event/Spell/PC/Land/...) purely for navigation;
    // whether a card has any Utility AI parameters configured ("assigned") is a separate,
    // orthogonal thing you check per row (and can filter down to via cardBoardHideAssigned).
    private Vector2 cardBoardScroll;
    private string cardBoardSearch = string.Empty;
    private bool cardBoardHideAssigned;
    // Empty = show every type. Set by clicking a bucket in DrawCardBoardBuckets; clicking the
    // active bucket again clears it.
    private string cardBoardTypeFilter = string.Empty;

    private const string NoCardType = "(no type)";

    // Stable color per CardData.type string — assigned once, first-seen order, from a fixed
    // palette, so a type's color never changes across an OnGUI pass or between sessions as long
    // as the type set doesn't change.
    private readonly Dictionary<string, Color> cardTypeColors = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Color[] CardTypePalette =
    {
        new(0.63f, 0.24f, 0.20f), new(0.66f, 0.47f, 0.12f), new(0.18f, 0.48f, 0.45f),
        new(0.24f, 0.42f, 0.54f), new(0.36f, 0.30f, 0.62f), new(0.55f, 0.30f, 0.55f),
        new(0.24f, 0.48f, 0.32f), new(0.45f, 0.45f, 0.20f), new(0.50f, 0.34f, 0.24f)
    };

    private Color CardTypeColor(string cardType)
    {
        string key = string.IsNullOrWhiteSpace(cardType) ? NoCardType : cardType;
        if (!cardTypeColors.TryGetValue(key, out Color color))
        {
            color = CardTypePalette[cardTypeColors.Count % CardTypePalette.Length];
            cardTypeColors[key] = color;
        }
        return color;
    }

    // "Unassigned" plus the distinct namespace prefixes in UtilityAIParameters.Known — the
    // vocabulary for the HTN bias preview simulation (Scenario inputs / Utility AI tab), not
    // related to Card Board's type buckets below. There is no enum backing either.
    private const string UnassignedGroup = "Unassigned";
    private static readonly string[] ParameterGroups =
    {
        UnassignedGroup, "Militaristic", "Economic", "Diplomatic",
        "Intelligence", "Artifacts", "Disruption", "Logistics"
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
                    new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.fallback.leaf" }
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

            string newTaskId = EditorGUILayout.DelayedTextField(node.taskId, GUILayout.Width(260f));
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
                    DrawConditionTermsInline(node.precondition, 260f);
                    break;
                }
                case HTNNodeType.PrimitiveTask:
                {
                    GUILayout.Label("until", EditorStyles.miniLabel, GUILayout.Width(28f));
                    DrawConditionTermsInline(node.completionCondition, 260f);
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

            // Which specific UtilityAIParameters this leaf's situation is about — the only
            // thing tying a card to this branch: cards whose own Card Board profile shares one
            // of these get HTNBiasBonus (see UtilityAIContext.ScoreAction). PrimitiveTask only.
            if (nodeType == HTNNodeType.PrimitiveTask)
            {
                node.preferredParameters ??= new List<string>();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8f + node.depth * 24f + 60f);
                GUILayout.Label("prefers:", EditorStyles.miniLabel, GUILayout.Width(50f));
                foreach (string param in node.preferredParameters.ToList())
                {
                    int paramIndex = Mathf.Max(0, UtilityAIParameters.Known.ToList().FindIndex(p => string.Equals(p, param, StringComparison.OrdinalIgnoreCase)));
                    int pickedParamIndex = EditorGUILayout.Popup(paramIndex, UtilityAIParameters.Known.ToArray(), GUILayout.Width(260f));
                    string pickedParam = UtilityAIParameters.Known[pickedParamIndex];
                    if (!string.Equals(pickedParam, param, StringComparison.Ordinal))
                    {
                        int paramListIndex = node.preferredParameters.IndexOf(param);
                        if (paramListIndex >= 0) node.preferredParameters[paramListIndex] = pickedParam;
                        strategiesDirty = true;
                    }
                    if (GUILayout.Button("✕", GUILayout.Width(20f)))
                    {
                        node.preferredParameters.Remove(param);
                        strategiesDirty = true;
                    }
                }
                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    string firstUnused = UtilityAIParameters.Known.FirstOrDefault(p => !node.preferredParameters.Contains(p, StringComparer.OrdinalIgnoreCase))
                        ?? UtilityAIParameters.Known[0];
                    node.preferredParameters.Add(firstUnused);
                    strategiesDirty = true;
                }
                GUILayout.FlexibleSpace();
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
        => new() { depth = depth, type = HTNNodeType.PrimitiveTask.ToString(), completionCondition = Cond("Global.Never"), taskId = string.Empty };

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

    // Data mirror of HTNStrategyBuilder.BuildDefault() — kept in exact sync by hand since the
    // two are independent representations (this one drives the HTN tab's visual editor and
    // Strategies.json; BuildDefault() drives the live game whenever no Strategies.json exists).
    private static HTNStrategyData BuildDefaultStrategyData()
    {
        return new HTNStrategyData
        {
            strategyId = AIStrategyLibrary.DefaultStrategyId,
            nodes = new List<HTNNodeData>
            {
                new() { depth = 0, type = "CompoundTask", taskId = "root" },

                new() { depth = 1, type = "Method", precondition = Cond("Global.ImmediateDanger"), taskId = "root.immediatedanger" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.immediatedanger.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.OwnPcFortificationNeedReady"), taskId = "root.immediatedanger.pick.fortify" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.immediatedanger.pick.fortify.leaf", preferredParameters = new() { "Militaristic.OwnPcFortificationNeed" } },
                new() { depth = 3, type = "Method", precondition = Cond("Intelligence.EnemyCharacterReady"), taskId = "root.immediatedanger.pick.intelligence" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.immediatedanger.pick.intelligence.leaf", preferredParameters = new() { "Intelligence.EnemyCharacter", "Intelligence.IndirectSafety", "Logistics.ReachEnemyCharacter" } },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.DuelOpportunityReady"), taskId = "root.immediatedanger.pick.duel" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.immediatedanger.pick.duel.leaf", preferredParameters = new() { "Militaristic.DuelAdvantage" } },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.SongDuelOpportunityReady"), taskId = "root.immediatedanger.pick.songduel" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.immediatedanger.pick.songduel.leaf", preferredParameters = new() { "Militaristic.SongDuelAdvantage" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.immediatedanger.pick.conscript" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.immediatedanger.pick.conscript.leaf", preferredParameters = new() { "Militaristic.OwnPcDefenderNeed" } },

                new() { depth = 1, type = "Method", precondition = Cond("Militaristic.Danger"), taskId = "root.danger" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.danger.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.OwnPcFortificationNeedReady"), taskId = "root.danger.pick.fortify" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.danger.pick.fortify.leaf", preferredParameters = new() { "Militaristic.OwnPcFortificationNeed" } },
                new() { depth = 3, type = "Method", precondition = Cond("Intelligence.EnemyCharacterReady"), taskId = "root.danger.pick.intelligence" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.danger.pick.intelligence.leaf", preferredParameters = new() { "Intelligence.EnemyCharacter", "Intelligence.IndirectSafety", "Logistics.ReachEnemyCharacter" } },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.DuelOpportunityReady"), taskId = "root.danger.pick.duel" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.danger.pick.duel.leaf", preferredParameters = new() { "Militaristic.DuelAdvantage" } },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.SongDuelOpportunityReady"), taskId = "root.danger.pick.songduel" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.danger.pick.songduel.leaf", preferredParameters = new() { "Militaristic.SongDuelAdvantage" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.danger.pick.conscript" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.danger.pick.conscript.leaf", preferredParameters = new() { "Militaristic.OwnPcDefenderNeed" } },

                new() { depth = 1, type = "Method", precondition = Cond("Economic.Critical", "Economic.Weak"), taskId = "root.recover" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.recover.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.MithrilReady"), taskId = "root.recover.pick.mithril" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.mithril.leaf", preferredParameters = new() { "Economic.MithrilInsufficient", "Economic.MithrilSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.SteelReady"), taskId = "root.recover.pick.steel" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.steel.leaf", preferredParameters = new() { "Economic.SteelInsufficient", "Economic.SteelSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.IronReady"), taskId = "root.recover.pick.iron" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.iron.leaf", preferredParameters = new() { "Economic.IronInsufficient", "Economic.IronSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.MountsReady"), taskId = "root.recover.pick.mounts" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.mounts.leaf", preferredParameters = new() { "Economic.MountsInsufficient", "Economic.MountsSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.TimberReady"), taskId = "root.recover.pick.timber" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.timber.leaf", preferredParameters = new() { "Economic.TimberInsufficient", "Economic.TimberSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Economic.LeatherReady"), taskId = "root.recover.pick.leather" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.leather.leaf", preferredParameters = new() { "Economic.LeatherInsufficient", "Economic.LeatherSurplus" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.recover.pick.fallback" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.recover.pick.fallback.leaf", preferredParameters = new() { "Economic.LiquidWealth" } },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.LowNplsReady"), taskId = "root.diplomacy.lownpls" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.lownpls.leaf", preferredParameters = new() { "Diplomatic.NplRecruitment", "Diplomatic.NplScarcity" } },

                new() { depth = 1, type = "Method", precondition = Cond("Artifacts.ArtifactScarcityReady"), taskId = "root.artifacts.lowartifacts" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.artifacts.lowartifacts.leaf", preferredParameters = new() { "Artifacts.ArtifactScarcity", "Artifacts.HiddenArtifacts" } },

                new() { depth = 1, type = "Method", precondition = Cond("Militaristic.OffenseWinRatioReady"), taskId = "root.offense" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.offense.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.OwnPcFortificationNeedReady"), taskId = "root.offense.pick.fortify" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.offense.pick.fortify.leaf", preferredParameters = new() { "Militaristic.OwnPcFortificationNeed" } },
                new() { depth = 3, type = "Method", precondition = Cond("Disruption.EnemyPressureReady"), taskId = "root.offense.pick.disrupt" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.offense.pick.disrupt.leaf", preferredParameters = new() { "Disruption.EnemyPressure" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.offense.pick.attack" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.offense.pick.attack.leaf", preferredParameters = new() { "Militaristic.MilitaryEdge", "Militaristic.EnemyPressure", "Logistics.InterceptEnemy" } },

                new() { depth = 1, type = "Method", precondition = Cond("Intelligence.HighValueEnemyCharacterReady", "Intelligence.EnemyPcVulnerabilityReady"), taskId = "root.intelligence.offense" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.intelligence.offense.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Intelligence.HighValueEnemyCharacterReady"), taskId = "root.intelligence.offense.pick.highvalue" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.intelligence.offense.pick.highvalue.leaf", preferredParameters = new() { "Intelligence.HighValueEnemyCharacter", "Logistics.ReachEnemyCharacter" } },
                new() { depth = 3, type = "Method", precondition = Cond("Intelligence.EnemyPcVulnerabilityReady"), taskId = "root.intelligence.offense.pick.sabotage" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.intelligence.offense.pick.sabotage.leaf", preferredParameters = new() { "Intelligence.EnemyPcVulnerability", "Logistics.InterceptEnemy" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.intelligence.offense.pick.fallback" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.intelligence.offense.pick.fallback.leaf" },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.NplsNearReady"), taskId = "root.diplomacy.nplsnear" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.nplsnear.leaf", preferredParameters = new() { "Diplomatic.NplRecruitment" } },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.NplsMidReady"), taskId = "root.diplomacy.nplsmid" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.nplsmid.leaf", preferredParameters = new() { "Diplomatic.NplRecruitment" } },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.EnemyPcOpportunityNearReady"), taskId = "root.diplomacy.enemiesnear" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.enemiesnear.leaf", preferredParameters = new() { "Diplomatic.EnemyPcOpportunity" } },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.EnemyPcOpportunityMidReady"), taskId = "root.diplomacy.enemiesmid" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.enemiesmid.leaf", preferredParameters = new() { "Diplomatic.EnemyPcOpportunity" } },

                new() { depth = 1, type = "Method", precondition = Cond("Diplomatic.OwnPcLoyaltyRiskReady"), taskId = "root.diplomacy.shore" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.diplomacy.shore.leaf", preferredParameters = new() { "Diplomatic.OwnPcLoyaltyRisk" } },

                new() { depth = 1, type = "Method", precondition = Cond("Artifacts.ArtifactTransferReady"), taskId = "root.artifacts.surplus" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.artifacts.surplus.leaf", preferredParameters = new() { "Artifacts.ArtifactTransfer" } },

                new() { depth = 1, type = "Method", precondition = Cond("Global.Always"), taskId = "root.militaristic.build" },
                new() { depth = 2, type = "CompoundTask", taskId = "root.militaristic.build.pick" },
                new() { depth = 3, type = "Method", precondition = Cond("Logistics.HealingNeedReady"), taskId = "root.militaristic.build.pick.heal" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.militaristic.build.pick.heal.leaf", preferredParameters = new() { "Logistics.HealingNeed" } },
                new() { depth = 3, type = "Method", precondition = Cond("Militaristic.OwnPcFortificationNeedReady"), taskId = "root.militaristic.build.pick.fortify" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.militaristic.build.pick.fortify.leaf", preferredParameters = new() { "Militaristic.OwnPcFortificationNeed" } },
                new() { depth = 3, type = "Method", precondition = Cond("Global.Always"), taskId = "root.militaristic.build.pick.conscript" },
                new() { depth = 4, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.militaristic.build.pick.conscript.leaf", preferredParameters = new() { "Militaristic.OwnPcDefenderNeed" } },

                new() { depth = 1, type = "Method", precondition = Cond("Global.Always"), taskId = "root.intelligence.build" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.intelligence.build.leaf", preferredParameters = new() { "Intelligence.EnemyCharacter", "Logistics.ReachNpc" } },

                new() { depth = 1, type = "Method", precondition = Cond("Global.Always"), taskId = "root.fallback" },
                new() { depth = 2, type = "PrimitiveTask", completionCondition = Cond("Global.Never"), taskId = "root.fallback.leaf" }
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

    // A card's Card Board group membership is derived live from which parameter-name prefixes
    // its own configured utilityParameters touch — there is no stored "advisor" field to read.
    // A card can belong to more than one group, or none (Unassigned) if it has no parameters
    // configured yet.
    private static List<string> ResolvedGroupsFor(string cardKey, Dictionary<string, CardParameterProfile> profiles)
    {
        if (!profiles.TryGetValue(cardKey, out CardParameterProfile p) || p.utilityParameters == null || p.utilityParameters.Count == 0)
        {
            return new List<string> { UnassignedGroup };
        }

        List<string> groups = p.utilityParameters
            .Where(m => m != null && !string.IsNullOrWhiteSpace(m.parameter) && m.parameter.Contains('.'))
            .Select(m => m.parameter.Split('.')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return groups.Count > 0 ? groups : new List<string> { UnassignedGroup };
    }

    private List<string> ResolvedGroupsFor(string cardKey) => ResolvedGroupsFor(cardKey, cardProfiles);

    private static bool IsProfileEmpty(CardParameterProfile p) =>
        p == null || (Mathf.Approximately(p.scoreBonus, 0f)
            && !p.ignoreSituation && (p.utilityParameters == null || p.utilityParameters.Count == 0));

    private void SetOrPruneProfile(string cardKey, CardParameterProfile profile)
    {
        if (IsProfileEmpty(profile)) cardProfiles.Remove(cardKey);
        else cardProfiles[cardKey] = profile;
    }

    // Every (card, actionClass) pair the Card Board currently lists — the flattened view
    // DrawCardBoardBuckets/DrawHtnBiasSimulation count over.
    private IEnumerable<(CardUsage card, string actionClass)> AllCardBoardCards()
    {
        foreach (string actionClass in actionCatalog)
        {
            if (!cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> cards)) continue;
            foreach (CardUsage card in cards) yield return (card, actionClass);
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
            "Click a bucket below to filter the list to that card type; click it again to show every type. Each "
            + "card's own Utility AI parameters are edited in the profile editor under its row — the dot color here "
            + "is just the card's type, it has nothing to do with whether parameters are assigned. Use \"Hide cards "
            + "with parameters assigned\" to see only the ones still needing attention.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        cardBoardSearch = EditorGUILayout.TextField("Filter", cardBoardSearch);
        cardBoardHideAssigned = GUILayout.Toggle(cardBoardHideAssigned, "Hide cards with parameters assigned", GUILayout.Width(260f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6f);

        DrawCardBoardBuckets();
        EditorGUILayout.Space(10f);

        cardBoardScroll = EditorGUILayout.BeginScrollView(cardBoardScroll);
        DrawCardBoardList();
        EditorGUILayout.EndScrollView();
    }

    // Buckets by the card's own real CardData.type — pure navigation, not an assignable
    // property, so clicking one sets/clears cardBoardTypeFilter rather than accepting a drop.
    private void DrawCardBoardBuckets()
    {
        GUIStyle nameStyle = new(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUIStyle countStyle = new(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        List<(CardUsage card, string actionClass)> allCards = AllCardBoardCards().ToList();
        List<string> types = allCards
            .Select(e => string.IsNullOrWhiteSpace(e.card.cardType) ? NoCardType : e.card.cardType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        EditorGUILayout.BeginHorizontal();
        foreach (string type in types)
        {
            int count = allCards.Count(e => string.Equals(string.IsNullOrWhiteSpace(e.card.cardType) ? NoCardType : e.card.cardType, type, StringComparison.OrdinalIgnoreCase));
            Color color = CardTypeColor(type);
            bool selected = string.Equals(cardBoardTypeFilter, type, StringComparison.OrdinalIgnoreCase);

            Rect r = GUILayoutUtility.GetRect(0, 46f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, selected ? Color.Lerp(color, Color.white, 0.35f) : color);
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3f), Color.white);
            }
            GUI.Label(new Rect(r.x, r.y + 4f, r.width, 18f), type, nameStyle);
            GUI.Label(new Rect(r.x, r.y + 24f, r.width, 18f), $"{count} card{(count == 1 ? "" : "s")}", countStyle);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                cardBoardTypeFilter = selected ? string.Empty : type;
                Event.current.Use();
                Repaint();
            }

            GUILayout.Space(4f);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCardBoardList()
    {
        string filter = cardBoardSearch?.Trim();
        foreach (string actionClass in actionCatalog.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
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
                    if (!string.IsNullOrEmpty(cardBoardTypeFilter))
                    {
                        string type = string.IsNullOrWhiteSpace(card.cardType) ? NoCardType : card.cardType;
                        if (!string.Equals(type, cardBoardTypeFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    }

                    string cardKey = UtilityAI.BuildCardProfileKey(card.deckId, card.cardId);
                    List<string> resolved = ResolvedGroupsFor(cardKey);
                    bool unassigned = resolved.Count == 1 && resolved[0] == UnassignedGroup;
                    if (cardBoardHideAssigned && !unassigned) continue;
                    DrawCardBoardRow(card, actionClass, resolved, unassigned);
                }
                continue;
            }

            // No printed card uses this action class — nothing to attach a per-card parameter
            // profile to, so this is a read-only "this action class exists but no card
            // currently references it" notice. Not subject to the type filter (there's no
            // CardUsage.cardType to filter by without a card).
            if (cardBoardHideAssigned || !string.IsNullOrEmpty(cardBoardTypeFilter)) continue;

            EditorGUILayout.BeginVertical(weightRowBoxStyle);

            EditorGUILayout.BeginHorizontal();
            Rect dotRect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.Width(10f));
            EditorGUI.DrawRect(dotRect, CardTypeColor(NoCardType));
            GUILayout.Space(6f);

            GUILayout.Label(ObjectNames.NicifyVariableName(actionClass), weightLabelStyle, GUILayout.Width(200f));
            GUILayout.Label("(no card references this class)", GUILayout.Width(260f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("(no effect text on this card)", weightDescStyle);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawCardBoardRow(CardUsage card, string actionClass, List<string> resolved, bool unassigned)
    {
        string cardKey = UtilityAI.BuildCardProfileKey(card.deckId, card.cardId);
        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.BeginHorizontal();
        Rect dotRect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.Width(10f));
        EditorGUI.DrawRect(dotRect, CardTypeColor(card.cardType));
        GUILayout.Space(6f);
        GUILayout.Label(card.cardName, weightLabelStyle, GUILayout.Width(260f));
        GUILayout.Label(ObjectNames.NicifyVariableName(actionClass), EditorStyles.miniLabel, GUILayout.Width(180f));
        GUILayout.Label(string.IsNullOrWhiteSpace(card.cardType) ? NoCardType : card.cardType, EditorStyles.miniLabel, GUILayout.Width(90f));
        GUILayout.FlexibleSpace();
        // Assigned/none is orthogonal to the type dot above — spelled out here since color no
        // longer carries that meaning.
        GUIStyle statusStyle = unassigned ? BuildWarningLabelStyle() : EditorStyles.miniLabel;
        GUILayout.Label(unassigned ? "No parameters" : string.Join(", ", resolved), statusStyle, GUILayout.Width(160f));
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
    }

    // Copies this card's full profile (bonus/flags/utility params) as an independent copy onto
    // every other printed card sharing its action class. Cards keep their own rows after this
    // — nothing at runtime ever shares one.
    private void DuplicateProfileToSiblingCards(string sourceCardKey, string actionClass)
    {
        if (!cardProfiles.TryGetValue(sourceCardKey, out CardParameterProfile source)) return;
        if (!cardsByActionRef.TryGetValue(actionClass, out List<CardUsage> siblings)) return;

        List<CardUsage> targets = siblings
            .Where(c => !string.Equals(UtilityAI.BuildCardProfileKey(c.deckId, c.cardId), sourceCardKey, StringComparison.Ordinal))
            .ToList();
        if (targets.Count == 0) return;

        int overwriteCount = targets.Count(c => cardProfiles.ContainsKey(UtilityAI.BuildCardProfileKey(c.deckId, c.cardId)));
        string message = $"Copy this card's parameter tuning to {targets.Count} other card(s) using {actionClass}"
            + (overwriteCount > 0 ? $" ({overwriteCount} already have their own tuning and will be overwritten)" : "")
            + "?";
        if (!EditorUtility.DisplayDialog("Duplicate Profile", message, "Duplicate", "Cancel")) return;

        foreach (CardUsage target in targets)
        {
            string key = UtilityAI.BuildCardProfileKey(target.deckId, target.cardId);
            cardProfiles[key] = new CardParameterProfile
            {
                deckId = target.deckId,
                cardId = target.cardId,
                cardName = target.cardName,
                actionClass = actionClass,
                scoreBonus = source.scoreBonus,
                ignoreSituation = source.ignoreSituation,
                utilityParameters = source.utilityParameters?
                    .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                    .ToList() ?? new List<ActionUtilityParameterModifier>()
            };
        }
        advisorsDirty = true;
    }

    // This is intentionally on Card Board: these are card/action authoring choices, not
    // sensing weights. Every value appears verbatim in UtilityAI.json and contributes exactly
    // parameter * multiplier + bonus. A card's Card Board group membership (the dot color and
    // bucket counts above) is entirely derived from this list — there is nothing else to it.
    private void DrawCardUtilityProfile(string cardKey, string deckId, int cardId, string cardName, string actionClass)
    {
        cardProfiles.TryGetValue(cardKey, out CardParameterProfile existing);
        float scoreBonus = existing?.scoreBonus ?? 0f;
        bool ignoreSituation = existing?.ignoreSituation ?? false;
        List<ActionUtilityParameterModifier> modifiers = existing?.utilityParameters
            ?.Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus }).ToList()
            ?? new List<ActionUtilityParameterModifier>();
        bool changedProfile = false;

        // The complete per-card score formula, top to bottom: scoreBonus always applies; the
        // utility profile list below only applies when Ignore Situation is off. See
        // UtilityAIContext.ScoreAction for the authoritative implementation.
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Score = Score bonus + (utility profile below, unless Ignore Situation is on)", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        scoreBonus = EditorGUILayout.FloatField(new GUIContent("Score bonus", "Flat amount added to this card's score every time it's scored, regardless of board state or Ignore Situation."), scoreBonus, GUILayout.MinWidth(240f));
        ignoreSituation = EditorGUILayout.ToggleLeft(new GUIContent("Ignore situation", "When on, the entire utility profile below is skipped — this card always scores exactly Score bonus, never anything situational."), ignoreSituation, GUILayout.Width(220f));
        changedProfile |= EditorGUI.EndChangeCheck();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Utility profile — each line is: parameter × multiplier + bonus"
            + (ignoreSituation ? "  (currently ignored — Ignore Situation is on)" : ""), EditorStyles.miniBoldLabel);
        foreach (ActionUtilityParameterModifier modifier in modifiers.ToList())
        {
            EditorGUILayout.BeginVertical(weightRowBoxStyle);
            EditorGUI.BeginChangeCheck();
            int index = Mathf.Max(0, UtilityAIParameters.Known.ToList().FindIndex(p => string.Equals(p, modifier.parameter, StringComparison.OrdinalIgnoreCase)));
            int changed = EditorGUILayout.Popup("Utility parameter", index, UtilityAIParameters.Known.ToArray());
            modifier.parameter = UtilityAIParameters.Known[changed];
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
            string firstUnused = UtilityAIParameters.Known.FirstOrDefault(p => !modifiers.Any(m => string.Equals(m.parameter, p, StringComparison.OrdinalIgnoreCase)))
                ?? UtilityAIParameters.Known[0];
            modifiers.Add(new ActionUtilityParameterModifier { parameter = firstUnused, multiplier = 1f, bonus = 0f });
            changedProfile = true;
        }

        CardParameterProfile profile = existing ?? new CardParameterProfile { deckId = deckId, cardId = cardId, cardName = cardName, actionClass = actionClass };
        profile.scoreBonus = scoreBonus;
        profile.ignoreSituation = ignoreSituation;
        profile.utilityParameters = modifiers;
        SetOrPruneProfile(cardKey, profile);
        if (changedProfile) advisorsDirty = true;
    }

    // "Shared" holds everything not specific to one parameter group (base score, difficulty,
    // HTN bias bonus, Always/Never, ...). The rest match the ParameterGroups names exactly, so
    // a single string doubles as both the toolbar label and the group filter — there is no
    // enum backing any of this.
    private static readonly string[] AdvisorProfileNames = { "Shared", "Militaristic", "Economic", "Diplomatic", "Intelligence", "Artifacts", "Disruption", "Logistics" };
    private int selectedAdvisorProfile;

    private void DrawAdvisorsSection()
    {
        EditorGUILayout.HelpBox(
            "Pick a parameter group to see everything about it in one place: its scoring weights, the HTN conditions "
            + "that read its state, which authored HTN tasks prefer its parameters, and a live score example. "
            + "\"Shared\" holds the handful of things every group uses.\n\n"
            + $"Saved to {AdvisorAssetPath}.",
            MessageType.Info);

        if (GUILayout.Button("Reset Utility AI Tuning To Default", GUILayout.Width(260f)))
        {
            foreach (UtilityWeightDefinition definition in UtilityAI.KnownWeights)
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

    // UtilityAI.Keys groups don't all match an advisor name literally (Affinity.X.*,
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
    // predicate's real formula (see UtilityAIContext.GetAdvisorViability / HTNRegistry), not derived
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

        scenarioFoldout = EditorGUILayout.Foldout(scenarioFoldout, "Scenario inputs (used by every simulation below)", true, EditorStyles.foldoutHeader);
        if (scenarioFoldout) DrawScenarioInputs();
        EditorGUILayout.Space(14f);

        DrawCardUtilityScoreSimulation();
        EditorGUILayout.Space(10f);

        List<HtnParamGroup> groups = BuildHtnParamGroups(advisorGroup);
        foreach (HtnParamGroup group in groups) DrawHtnParamFoldout(advisorGroup, group);

        List<UtilityWeightDefinition> other = GetOtherWeights(advisorGroup);
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
        string cardKey = UtilityAI.BuildCardProfileKey(deckId, cardId);
        if (!cardProfiles.TryGetValue(cardKey, out CardParameterProfile profile) || profile.utilityParameters == null || profile.utilityParameters.Count == 0)
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
        float W(string key) => advisorWeights.TryGetValue(key, out float value) ? value : UtilityAI.GetDefaultWeight(key);
        float enemyPressure = Mathf.Max(0f, W(K.EnemyProximityMax) - simEnemyDistance);
        return parameter switch
        {
            UtilityAIParameters.MilitaristicEnemyPressure => enemyPressure,
            UtilityAIParameters.MilitaristicMilitaryEdge => !simLeadingArmy ? W(K.NoArmyPenalty) : MilitaristicEdge(W),
            UtilityAIParameters.EconomicLiquidWealth => simGoldBuffer + simResourceNetWorth,
            UtilityAIParameters.DiplomaticIndirectSafety => SimulatedOutmatched() ? W(K.DiplomaticOutmatchedBonus) : 0f,
            UtilityAIParameters.DiplomaticEnemyPressure => enemyPressure,
            UtilityAIParameters.DiplomaticEmissaryStrength => simEmissary,
            UtilityAIParameters.IntelligenceEnemyCharacter => Mathf.Max(0f, W(K.EnemyCharacterProximityMax) - simEnemyCharacterDistance),
            UtilityAIParameters.IntelligenceIndirectSafety => SimulatedOutmatched() ? W(K.IntelligenceOutmatchedBonus) : 0f,
            UtilityAIParameters.IntelligenceEnemyPressure => enemyPressure,
            UtilityAIParameters.IntelligenceAgentStrength => simAgent,
            UtilityAIParameters.ArtifactsArtifactScarcity => (1f - Mathf.Clamp01(simArtifactShare)) * W(K.ArtifactScarcityWeight),
            UtilityAIParameters.ArtifactsArtifactTransfer => 0f,
            UtilityAIParameters.ArtifactsEnemyPressure => enemyPressure,
            UtilityAIParameters.ArtifactsHiddenArtifacts => simHiddenArtifacts,
            UtilityAIParameters.ArtifactsMageStrength => simMage,
            UtilityAIParameters.LogisticsReachNpc => Mathf.Max(0f, W(K.LogisticsProximityMax) - simNpcDistance * W(K.LogisticsDistancePenaltyPerHex)),
            UtilityAIParameters.LogisticsInterceptEnemy => Mathf.Max(0f, W(K.LogisticsProximityMax) - simEnemyDistance * W(K.LogisticsDistancePenaltyPerHex)),
            UtilityAIParameters.LogisticsReachEnemyCharacter => Mathf.Max(0f, W(K.LogisticsProximityMax) - simEnemyCharacterDistance * W(K.LogisticsDistancePenaltyPerHex)),
            UtilityAIParameters.LogisticsHealingNeed => simWoundedAllies,
            UtilityAIParameters.DisruptionEnemyPressure => enemyPressure,
            UtilityAIParameters.MilitaristicDuelAdvantage => simDuelAdvantage,
            UtilityAIParameters.MilitaristicSongDuelAdvantage => simSongDuelAdvantage,
            UtilityAIParameters.DiplomaticNplScarcity => Mathf.Max(0f, W(K.DiplomaticLowNplsCountAtMost) - simUnrecruitedNplCount),
            UtilityAIParameters.MilitaristicOwnPcFortificationNeed => Mathf.Max(0f, W(K.MilitaristicOwnPcFortificationProximityMax) - simOwnPcFortificationDistance),
            UtilityAIParameters.DiplomaticNplRecruitment => Mathf.Max(0f, W(K.DiplomaticNplRecruitmentProximityMax) - simNplRecruitmentDistance),
            // Same formula as MilitaristicOwnPcFortificationNeed above — see the constant's doc
            // comment in AdvisorConfig.cs.
            UtilityAIParameters.MilitaristicOwnPcDefenderNeed => Mathf.Max(0f, W(K.MilitaristicOwnPcFortificationProximityMax) - simOwnPcFortificationDistance),
            // Deviation from this leader's deck-required share (simMithrilShare etc.), not a
            // flat unit threshold — mirrors UtilityAIContext.GetResourceInsufficientScore/
            // GetResourceSurplusScore. The per-material weight now scales a percentage-point
            // deviation rather than marking an absolute unit floor/ceiling.
            UtilityAIParameters.EconomicMithrilInsufficient => Mathf.Max(0f, simMithrilShare - SimulatedResourceShare(simMithrilAmount)) * 100f * W(K.EconomicMithrilInsufficientBelow),
            UtilityAIParameters.EconomicMithrilSurplus => Mathf.Max(0f, SimulatedResourceShare(simMithrilAmount) - simMithrilShare) * 100f * W(K.EconomicMithrilSurplusAbove),
            UtilityAIParameters.EconomicSteelInsufficient => Mathf.Max(0f, simSteelShare - SimulatedResourceShare(simSteelAmount)) * 100f * W(K.EconomicSteelInsufficientBelow),
            UtilityAIParameters.EconomicSteelSurplus => Mathf.Max(0f, SimulatedResourceShare(simSteelAmount) - simSteelShare) * 100f * W(K.EconomicSteelSurplusAbove),
            UtilityAIParameters.EconomicIronInsufficient => Mathf.Max(0f, simIronShare - SimulatedResourceShare(simIronAmount)) * 100f * W(K.EconomicIronInsufficientBelow),
            UtilityAIParameters.EconomicIronSurplus => Mathf.Max(0f, SimulatedResourceShare(simIronAmount) - simIronShare) * 100f * W(K.EconomicIronSurplusAbove),
            UtilityAIParameters.EconomicMountsInsufficient => Mathf.Max(0f, simMountsShare - SimulatedResourceShare(simMountsAmount)) * 100f * W(K.EconomicMountsInsufficientBelow),
            UtilityAIParameters.EconomicMountsSurplus => Mathf.Max(0f, SimulatedResourceShare(simMountsAmount) - simMountsShare) * 100f * W(K.EconomicMountsSurplusAbove),
            UtilityAIParameters.EconomicTimberInsufficient => Mathf.Max(0f, simTimberShare - SimulatedResourceShare(simTimberAmount)) * 100f * W(K.EconomicTimberInsufficientBelow),
            UtilityAIParameters.EconomicTimberSurplus => Mathf.Max(0f, SimulatedResourceShare(simTimberAmount) - simTimberShare) * 100f * W(K.EconomicTimberSurplusAbove),
            UtilityAIParameters.EconomicLeatherInsufficient => Mathf.Max(0f, simLeatherShare - SimulatedResourceShare(simLeatherAmount)) * 100f * W(K.EconomicLeatherInsufficientBelow),
            UtilityAIParameters.EconomicLeatherSurplus => Mathf.Max(0f, SimulatedResourceShare(simLeatherAmount) - simLeatherShare) * 100f * W(K.EconomicLeatherSurplusAbove),
            UtilityAIParameters.EconomicGoldInsufficient => Mathf.Max(0f, W(K.EconomyCriticalBelow) - simGoldBuffer),
            UtilityAIParameters.EconomicGoldSurplus => Mathf.Max(0f, simGoldBuffer - W(K.EconomyStableBelow)),
            _ => 0f
        };
    }

    // Hand-mapped: which HTN predicates does this group drive, and which weight keys does
    // each predicate's real formula actually read? See UtilityAIContext's named
    // GetMilitaristicViability/GetIntelligenceViability/etc. methods, IsEnemyNear/IsOutmatched,
    // and HTNRegistry's predicate lambdas for the source of truth this mirrors.
    private List<HtnParamGroup> BuildHtnParamGroups(string advisorGroup)
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

        switch (advisorGroup)
        {
            case "Militaristic":
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
                AddDirectUtilityGroup(groups, "Militaristic.OwnPcFortificationNeed", "Militaristic.OwnPcFortificationNeedReady", "Proximity to the nearest own PC whose PC.GetDefense() is below Militaristic.OwnPcDefenseBelow — needs fortifying. Gates root.offense.pick.fortify and root.danger.pick.fortify in the default HTN strategy.",
                    K.MilitaristicOwnPcDefenseBelow, K.MilitaristicOwnPcFortificationProximityMax, K.MilitaristicOwnPcFortificationNeedThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.OwnPcDefenderNeed",
                    description = "Same formula as Militaristic.OwnPcFortificationNeed above (proximity to the nearest own PC below Militaristic.OwnPcDefenseBelow) under a distinct name, so ConscriptArmy/TrainArmy/Block can be targeted independently of FortifyPC. Gates nothing directly — always considered via root.danger.pick.conscript's Global.Always fallback branch, not a dedicated Ready predicate.",
                    weightKeys = new[] { K.MilitaristicOwnPcDefenseBelow, K.MilitaristicOwnPcFortificationProximityMax }
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.Viable",
                    description = "True when Militaristic's viability (enemy proximity + army edge) is above its threshold. Not used by the default HTN tree (superseded by Militaristic.OffenseWinRatioReady) — kept for a hand-authored Strategies.json.",
                    weightKeys = new[] { K.EnemyProximityMax, K.NoArmyPenalty, K.FarTargetPenalty, K.MilitaristicViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Militaristic")
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Militaristic.OffenseWinRatioReady",
                    description = "True when not in danger AND this character's army offence is at least Militaristic.MinWinRatioToAttack times the nearest enemy's estimated strength — gates root.offense in the default HTN strategy, replacing the old fuzzy Militaristic.Viable gate so a losing/marginal matchup never routes into Attack.",
                    weightKeys = new[] { K.MilitaristicMinWinRatioToAttack }
                });
                AddDirectUtilityGroup(groups, "Militaristic.DuelAdvantage", "Militaristic.DuelOpportunityReady", "This character's estimated Duel.EstimateDuelScore minus the best eligible opponent's — signed, so a losing matchup contributes negatively. Gates the ImmediateDanger/Danger duel pick branches in the default HTN strategy.",
                    K.MilitaristicDuelSafetyMargin);
                AddDirectUtilityGroup(groups, "Militaristic.SongDuelAdvantage", "Militaristic.SongDuelOpportunityReady", "Same as Militaristic.DuelAdvantage, for Battle of Songs (mage-vs-mage). Gates the ImmediateDanger/Danger song-duel pick branches in the default HTN strategy.",
                    K.MilitaristicSongDuelSafetyMargin);
                break;

            case "Economic":
                groups.Add(new HtnParamGroup
                {
                    title = "Economic Tier (Critical / Weak / Stable / Surplus)",
                    description = "Exactly one tier is ever true at once, decided by liquid wealth (gold + resources at current market sell price — this game has no per-turn income of any kind). Which Methods these tiers gate (e.g. root.recover) is authored in the Strategies tab, not duplicated here — Economic has no Utility viability formula of its own to simulate.",
                    weightKeys = new[] { K.EconomyCriticalBelow, K.EconomyWeakBelow, K.EconomyStableBelow },
                    drawSimulation = DrawEconomicTierSimulation
                });
                AddDirectUtilityGroup(groups, "Economic.MithrilInsufficient", "Economic.MithrilReady", "max(0, deck's target mithril share − this leader's current mithril share of its stockpile) × 100 × Economic.MithrilInsufficientBelow (now a deviation scale, not a unit floor). Deck target share comes from NationBlackboard — sum of mithrilRequired across the leader's whole deck, normalized. Biases toward BuyMithril. Gates root.recover.pick.mithril in the default HTN strategy.",
                    K.EconomicMithrilInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.MithrilSurplus", "Economic.MithrilReady", "max(0, current mithril share − deck's target mithril share) × 100 × Economic.MithrilSurplusAbove (now a deviation scale, not a unit ceiling). Biases toward SellMithril. Gates root.recover.pick.mithril in the default HTN strategy.",
                    K.EconomicMithrilSurplusAbove);
                AddDirectUtilityGroup(groups, "Economic.SteelInsufficient", "Economic.SteelReady", "max(0, deck's target steel share − current steel share) × 100 × Economic.SteelInsufficientBelow (deviation scale). Biases toward BuySteel. Gates root.recover.pick.steel in the default HTN strategy.",
                    K.EconomicSteelInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.SteelSurplus", "Economic.SteelReady", "max(0, current steel share − deck's target steel share) × 100 × Economic.SteelSurplusAbove (deviation scale). Biases toward SellSteel. Gates root.recover.pick.steel in the default HTN strategy.",
                    K.EconomicSteelSurplusAbove);
                AddDirectUtilityGroup(groups, "Economic.IronInsufficient", "Economic.IronReady", "max(0, deck's target iron share − current iron share) × 100 × Economic.IronInsufficientBelow (deviation scale). Biases toward BuyIron. Gates root.recover.pick.iron in the default HTN strategy.",
                    K.EconomicIronInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.IronSurplus", "Economic.IronReady", "max(0, current iron share − deck's target iron share) × 100 × Economic.IronSurplusAbove (deviation scale). Biases toward SellIron. Gates root.recover.pick.iron in the default HTN strategy.",
                    K.EconomicIronSurplusAbove);
                AddDirectUtilityGroup(groups, "Economic.MountsInsufficient", "Economic.MountsReady", "max(0, deck's target mounts share − current mounts share) × 100 × Economic.MountsInsufficientBelow (deviation scale). Biases toward BuyMounts. Gates root.recover.pick.mounts in the default HTN strategy.",
                    K.EconomicMountsInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.MountsSurplus", "Economic.MountsReady", "max(0, current mounts share − deck's target mounts share) × 100 × Economic.MountsSurplusAbove (deviation scale). Biases toward SellMounts. Gates root.recover.pick.mounts in the default HTN strategy.",
                    K.EconomicMountsSurplusAbove);
                AddDirectUtilityGroup(groups, "Economic.TimberInsufficient", "Economic.TimberReady", "max(0, deck's target timber share − current timber share) × 100 × Economic.TimberInsufficientBelow (deviation scale). Biases toward BuyTimber. Gates root.recover.pick.timber in the default HTN strategy.",
                    K.EconomicTimberInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.TimberSurplus", "Economic.TimberReady", "max(0, current timber share − deck's target timber share) × 100 × Economic.TimberSurplusAbove (deviation scale). Biases toward SellTimber. Gates root.recover.pick.timber in the default HTN strategy.",
                    K.EconomicTimberSurplusAbove);
                AddDirectUtilityGroup(groups, "Economic.LeatherInsufficient", "Economic.LeatherReady", "max(0, deck's target leather share − current leather share) × 100 × Economic.LeatherInsufficientBelow (deviation scale). Biases toward BuyLeather. Gates root.recover.pick.leather in the default HTN strategy.",
                    K.EconomicLeatherInsufficientBelow);
                AddDirectUtilityGroup(groups, "Economic.LeatherSurplus", "Economic.LeatherReady", "max(0, current leather share − deck's target leather share) × 100 × Economic.LeatherSurplusAbove (deviation scale). Biases toward SellLeather. Gates root.recover.pick.leather in the default HTN strategy.",
                    K.EconomicLeatherSurplusAbove);
                groups.Add(new HtnParamGroup
                {
                    title = "Economic.GoldInsufficient",
                    description = "max(0, Economy.CriticalBelow − gold). No Buy/SellGold card exists, so this rides along on every Sell{X} card's own authored profile instead of gating a dedicated HTN branch — sell anything to raise cash.",
                    weightKeys = new[] { K.EconomyCriticalBelow }
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Economic.GoldSurplus",
                    description = "max(0, gold − Economy.StableBelow). No Buy/SellGold card exists, so this rides along on every Buy{X} card's own authored profile instead of gating a dedicated HTN branch — spend excess cash on anything.",
                    weightKeys = new[] { K.EconomyStableBelow }
                });
                break;

            case "Diplomatic":
                AddDirectUtilityGroup(groups, "Diplomatic.IndirectSafety", "Diplomatic.IndirectSafetyReady", "Outmatched-response value. It is either zero or Diplomatic.OutmatchedBonus, using the shared outmatched definition.",
                    K.OutmatchedStrengthRatio, K.DiplomaticOutmatchedBonus, K.DiplomaticIndirectSafetyThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.EnemyPcOpportunity", "Diplomatic.EnemyPcOpportunityReady", "Proximity to the nearest enemy-owned PC whose loyalty is below Diplomatic.EnemyPcLoyaltyBelow — an influence-out target. Gates root.diplomacy.pick.flip in the default HTN strategy.",
                    K.DiplomaticEnemyPcLoyaltyBelow, K.DiplomaticEnemyPcOpportunityProximityMax, K.DiplomaticEnemyPcOpportunityThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.OwnPcLoyaltyRisk", "Diplomatic.OwnPcLoyaltyRiskReady", "Proximity to the nearest own PC whose loyalty is below Diplomatic.OwnPcLoyaltyBelow — needs influencing up. Gates root.diplomacy.pick.shore in the default HTN strategy.",
                    K.DiplomaticOwnPcLoyaltyBelow, K.DiplomaticOwnPcLoyaltyRiskProximityMax, K.DiplomaticOwnPcLoyaltyRiskThreshold);
                AddDirectUtilityGroup(groups, "Diplomatic.NplRecruitment", "Diplomatic.NplRecruitmentReady", "Proximity to the nearest NPL capital currently eligible for StateAllegiance (AFriendOrThree) recruitment — same eligibility gate the card itself uses (alignment match + capital's PC card already played), not a fabricated relationship counter. Also gates root.diplomacy.nplsnear/nplsmid (near/mid distance banding) in the default HTN strategy.",
                    K.DiplomaticNplRecruitmentProximityMax, K.DiplomaticNplRecruitmentThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Diplomatic.NplScarcity",
                    description = "max(0, Diplomatic.LowNplsCountAtMost − board-wide count of same-alignment, not-yet-joined NonPlayableLeaders). Board-wide, not proximity-based — gates root.diplomacy.lownpls (a wide-radius recruit push) in the default HTN strategy, the top diplomatic priority.",
                    weightKeys = new[] { K.DiplomaticLowNplsCountAtMost, K.DiplomaticNplScarcityThreshold }
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Diplomatic Near/Mid Banding",
                    description = "Diplomatic.NplsNearReady/NplsMidReady and Diplomatic.EnemyPcOpportunityNearReady/MidReady split the continuous NplRecruitment/EnemyPcOpportunity proximity signals above into two discrete HTN priority tiers (root.diplomacy.nplsnear/nplsmid, root.diplomacy.enemiesnear/enemiesmid) instead of one fading score.",
                    weightKeys = new[] { K.DiplomaticNplNearDistance, K.DiplomaticNplMidDistance, K.DiplomaticEnemyPcOpportunityNearDistance, K.DiplomaticEnemyPcOpportunityMidDistance }
                });
                groups.Add(new HtnParamGroup
                {
                    title = "Diplomatic.Viable",
                    description = "True when Diplomatic's viability (NPC proximity + outmatched bonus) is above its threshold. Not used by the default HTN tree — kept for a hand-authored Strategies.json.",
                    weightKeys = new[] { K.NpcProximityMax, K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.OutmatchedStrengthRatio, K.DiplomaticOutmatchedBonus, K.DiplomaticEnemyPressureWeight, K.DiplomaticEmissaryStrengthWeight, K.DiplomaticViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Diplomatic")
                });
                break;

            case "Intelligence":
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
                    drawSimulation = () => DrawViableSimulation("Intelligence")
                });
                break;

            case "Artifacts":
                AddDirectUtilityGroup(groups, "Artifacts.ArtifactScarcity", "Artifacts.ArtifactScarcityReady", "Nation artifact scarcity multiplied by Artifacts.ArtifactScarcityWeight. Gates root.artifacts.lowartifacts in the default HTN strategy.",
                    K.ArtifactScarcityWeight, K.ArtifactsArtifactScarcityThreshold);
                AddDirectUtilityGroup(groups, "Artifacts.ArtifactTransfer", "Artifacts.ArtifactTransferReady", "Best legal artifact-transfer opportunity published by the Artifacts Advisor — \"mages have many artifacts, consolidate/protect them.\" Gates root.artifacts.surplus in the default HTN strategy.",
                    K.ArtifactsArtifactTransferThreshold);
                AddDirectUtilityGroup(groups, "Artifacts.EnemyPressure", "Artifacts.EnemyPressureReady", "Enemy proximity, including the shared neutral-target adjustment.",
                    K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.ArtifactsEnemyPressureThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Artifacts.Viable",
                    description = "True when Artifacts's viability (artifact scarcity + enemy proximity) is above its threshold. Not used by the default HTN tree (superseded by Artifacts.ArtifactScarcityReady/ArtifactTransferReady) — kept for a hand-authored Strategies.json.",
                    weightKeys = new[] { K.ArtifactScarcityWeight, K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.ArtifactsHiddenArtifactsWeight, K.ArtifactsMageStrengthWeight, K.ArtifactsViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Artifacts")
                });
                break;

            case "Logistics":
                AddDirectUtilityGroup(groups, "Logistics.ReachNpc", "Logistics.ReachNpcReady", "Distance to the nearest unrevealed NPC destination.",
                    K.LogisticsProximityMax, K.LogisticsDistancePenaltyPerHex, K.LogisticsReachNpcThreshold);
                AddDirectUtilityGroup(groups, "Logistics.InterceptEnemy", "Logistics.InterceptEnemyReady", "Distance to the closest enemy destination.",
                    K.LogisticsProximityMax, K.LogisticsDistancePenaltyPerHex, K.LogisticsInterceptEnemyThreshold);
                AddDirectUtilityGroup(groups, "Logistics.ReachEnemyCharacter", "Logistics.ReachEnemyCharacterReady", "Distance to the nearest enemy character destination.",
                    K.LogisticsProximityMax, K.LogisticsDistancePenaltyPerHex, K.LogisticsReachEnemyCharacterThreshold);
                AddDirectUtilityGroup(groups, "Logistics.HealingNeed", "Logistics.HealingNeedReady", "Count of wounded allies (Character.health below Logistics.HealingNeedHealthBelow) sharing this character's hex. Gates root.logistics.pick.heal in the default HTN strategy.",
                    K.LogisticsHealingNeedHealthBelow, K.LogisticsHealingNeedThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Logistics.Viable",
                    description = "True when Logistics's viability (proximity to the preferred destination) is above its threshold.",
                    weightKeys = new[] { K.LogisticsProximityMax, K.LogisticsDistancePenaltyPerHex, K.LogisticsViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Logistics")
                });
                break;

            case "Disruption":
                AddDirectUtilityGroup(groups, "Disruption.EnemyPressure", "Disruption.EnemyPressureReady", "Enemy proximity — is there someone nearby to halt/block/debuff.",
                    K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.DisruptionEnemyPressureThreshold);
                groups.Add(new HtnParamGroup
                {
                    title = "Disruption.Viable",
                    description = "True when Disruption's viability (enemy proximity) is above its threshold.",
                    weightKeys = new[] { K.EnemyProximityMax, K.NeutralTargetExtraDistance, K.DisruptionViabilityThreshold },
                    drawSimulation = () => DrawViableSimulation("Disruption")
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

    // Union of every weight key referenced by ANY group's HTN parameters — computed fresh
    // each call (cheap: ~30 keys, only runs while the "leftover" check is being drawn) so a
    // weight only ever shows up as uncategorized if it's truly unaccounted-for everywhere.
    // In the normal case this covers every known weight — there is no other role left for
    // a weight to play (see HtnParamGroup).
    private HashSet<string> AllHtnConnectedWeightKeys()
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in AdvisorProfileNames)
        {
            foreach (HtnParamGroup group in BuildHtnParamGroups(name))
                foreach (string key in group.weightKeys) keys.Add(key);
        }
        return keys;
    }

    private List<UtilityWeightDefinition> GetOtherWeights(string advisorGroup)
    {
        HashSet<string> connected = AllHtnConnectedWeightKeys();
        return UtilityAI.KnownWeights
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
                UtilityWeightDefinition definition = UtilityAI.KnownWeights
                    .FirstOrDefault(d => string.Equals(d.key, key, StringComparison.OrdinalIgnoreCase));
                if (definition != null) DrawWeightRow(definition);
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    // Skill-affinity (a flat bonus per card based on the playing character's own skill,
    // unrelated to world-state) used to live here — removed from UtilityAIContext.ScoreAction
    // entirely, since it structurally can't connect to any HTN predicate (HTN reads
    // situations, not a character's innate stats) and that's now a hard requirement: every
    // scoring weight either gates an HTN condition or shows up here as a rare, clearly-labeled
    // exception (currently just Shared's difficulty-penalty shape, which has no per-advisor
    // equivalent to speak of).
    private void DrawOtherWeightsFoldout(string advisorGroup, List<UtilityWeightDefinition> weights)
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
            foreach (UtilityWeightDefinition definition in weights) DrawWeightRow(definition);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    // --- Simulation renderers, one per predicate shape --------------------------------------

    private void DrawBooleanSimulation(string label, bool value)
    {
        EnsureAdvisorStyles();
        EditorGUILayout.BeginHorizontal();
        DrawStatusBadge($"{label} = {(value ? "TRUE" : "FALSE")}", value, GUILayout.MinWidth(260f), GUILayout.ExpandWidth(true));
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
        DrawStatusBadge($"{label} = {(overall ? "TRUE" : "FALSE")}", overall, GUILayout.MinWidth(260f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawViableSimulation(string advisorGroup)
    {
        EnsureAdvisorStyles();
        string thresholdKey = ViabilityThresholdKeyFor(advisorGroup);
        float viability = SimulateViability(advisorGroup);
        float threshold = thresholdKey != null
            ? (advisorWeights.TryGetValue(thresholdKey, out float t) ? t : UtilityAI.GetDefaultWeight(thresholdKey))
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

    // Simulates the HTN's active leaf preferring some parameter from this group — the same
    // match UtilityAIContext.ScoreAction checks a card's own utilityParameters against
    // PreferredParameters. There is no "advisor" to pick anymore, just which group's
    // parameters would overlap.
    private void DrawHtnBiasSimulation()
    {
        EnsureAdvisorStyles();
        actionCatalog ??= BuildActionCatalog();
        cardsByActionRef ??= BuildCardUsageMap();

        EditorGUILayout.LabelField(new GUIContent("HTN bias preview", "Simulate the HTN tab's currently-active leaf preferring a parameter from this group."));
        simBiasedGroupIndex = EditorGUILayout.Popup(Mathf.Clamp(simBiasedGroupIndex, 0, ParameterGroups.Length - 1), ParameterGroups, GUILayout.Width(160f));

        string biasedGroup = ParameterGroups[Mathf.Clamp(simBiasedGroupIndex, 0, ParameterGroups.Length - 1)];
        bool active = !string.Equals(biasedGroup, UnassignedGroup, StringComparison.OrdinalIgnoreCase);
        float bonus = advisorWeights.TryGetValue(UtilityAI.Keys.HTNBiasBonus, out float b)
            ? b : UtilityAI.GetDefaultWeight(UtilityAI.Keys.HTNBiasBonus);
        int affectedCount = active
            ? AllCardBoardCards().Count(e => ResolvedGroupsFor(UtilityAI.BuildCardProfileKey(e.card.deckId, e.card.cardId)).Contains(biasedGroup, StringComparer.OrdinalIgnoreCase))
            : 0;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        DrawStatusBadge(
            active ? $"+{bonus:0.#} to {affectedCount} {biasedGroup} card(s)" : "No group selected — affects nothing right now",
            active, GUILayout.MinWidth(240f), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    // One card per weight: a top row with the label and the number field big enough to read,
    // and the plain-English description on its own full-width wrapped line underneath —
    // replaces the old single-line layout that squeezed label + field + default + description
    // into one row and clipped the description on anything but a very wide window.
    private void DrawWeightRow(UtilityWeightDefinition definition)
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
        simWoundedAllies = DrawStatField("Wounded allies here", simWoundedAllies, "How many allies in this character's hex are below Logistics.HealingNeedHealthBelow.", width: 130f);
        simDuelAdvantage = DrawStatFieldF("Duel advantage", simDuelAdvantage, "This character's estimated Duel.EstimateDuelScore minus the best eligible opponent's — negative means this character would likely lose.", width: 130f);
        simSongDuelAdvantage = DrawStatFieldF("Song duel advantage", simSongDuelAdvantage, "Same as Duel advantage, for Battle of Songs.", width: 150f);
        simUnrecruitedNplCount = DrawStatField("Unrecruited NPLs", simUnrecruitedNplCount, "Board-wide count of same-alignment, not-yet-joined NonPlayableLeaders.", width: 130f);
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
        simArtifactShare = EditorGUILayout.Slider(simArtifactShare, 0f, 1f, GUILayout.Width(260f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Materials (this leader's own stockpile, not market stock)", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simMithrilAmount = DrawStatField("Mithril", simMithrilAmount, width: 90f);
        simSteelAmount = DrawStatField("Steel", simSteelAmount, width: 90f);
        simIronAmount = DrawStatField("Iron", simIronAmount, width: 90f);
        simMountsAmount = DrawStatField("Mounts", simMountsAmount, width: 90f);
        simTimberAmount = DrawStatField("Timber", simTimberAmount, width: 90f);
        simLeatherAmount = DrawStatField("Leather", simLeatherAmount, width: 90f);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(new GUIContent("Deck target share (0..1)", "What share of this leader's deck's total material cost each material makes up — NationBlackboard in the real game. Insufficient/Surplus below are driven by the gap between this and the stockpile shares above, not by the raw amounts alone."), weightDescStyle);
        EditorGUILayout.BeginHorizontal();
        simMithrilShare = DrawShareField("Mithril", simMithrilShare);
        simSteelShare = DrawShareField("Steel", simSteelShare);
        simIronShare = DrawShareField("Iron", simIronShare);
        simMountsShare = DrawShareField("Mounts", simMountsShare);
        simTimberShare = DrawShareField("Timber", simTimberShare);
        simLeatherShare = DrawShareField("Leather", simLeatherShare);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("Distances (hexes) — 0 = adjacent/right here, high = far or none at all", conditionKeyStyle);
        EditorGUILayout.BeginHorizontal();
        simEnemyDistance = DrawStatFieldF("Enemy PC / army", simEnemyDistance, "Hexes to the nearest enemy PC or army. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, UtilityAI.Keys.EnemyProximityMax));
        simEnemyCharacterDistance = DrawStatFieldF("Enemy character", simEnemyCharacterDistance, "Hexes to the nearest enemy character. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, UtilityAI.Keys.EnemyCharacterProximityMax));
        simNpcDistance = DrawStatFieldF("Unrevealed NPC", simNpcDistance, "Hexes to the nearest unrevealed NPC. 0 = adjacent. Use 99 for none.",
            hint: d => ProximityHint(d, UtilityAI.Keys.NpcProximityMax));
        simDestinationDistance = DrawStatFieldF("Move destination", simDestinationDistance, "Hexes to the preferred Logistics destination. 0 = arrived.",
            hint: d => ProximityHint(d, UtilityAI.Keys.LogisticsProximityMax, UtilityAI.Keys.LogisticsDistancePenaltyPerHex));
        simOwnPcFortificationDistance = DrawStatFieldF("Own PC needing fort", simOwnPcFortificationDistance, "Hexes to the nearest own PC below Militaristic.OwnPcDefenseBelow. Use 99 for none.",
            hint: d => ProximityHint(d, UtilityAI.Keys.MilitaristicOwnPcFortificationProximityMax));
        simNplRecruitmentDistance = DrawStatFieldF("NPL recruitment-ready", simNplRecruitmentDistance, "Hexes to the nearest NPL capital eligible for StateAllegiance recruitment. Use 99 for none.",
            hint: d => ProximityHint(d, UtilityAI.Keys.DiplomaticNplRecruitmentProximityMax));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(weightRowBoxStyle);
        EditorGUILayout.LabelField("HTN bias preview", conditionKeyStyle);
        EditorGUILayout.LabelField("Simulate the HTN tab's currently-active leaf preferring a parameter from this group.", weightDescStyle);
        simBiasedGroupIndex = EditorGUILayout.Popup(Mathf.Clamp(simBiasedGroupIndex, 0, ParameterGroups.Length - 1), ParameterGroups, GUILayout.Width(160f));
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

    private float DrawShareField(string caption, float value, float width = 90f)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(caption, EditorStyles.miniLabel);
        float result = Mathf.Clamp01(EditorGUILayout.FloatField(value, GUILayout.Width(width), GUILayout.Height(20f)));
        EditorGUILayout.EndVertical();
        GUILayout.Space(10f);
        return result;
    }

    // Mirrors UtilityAIContext.GetOwnResourceShare: what share of the leader's stockpile (across the
    // six tradeable materials) this one amount makes up right now.
    private float SimulatedResourceShare(int amount)
    {
        float total = simMithrilAmount + simSteelAmount + simIronAmount + simMountsAmount + simTimberAmount + simLeatherAmount;
        return total > 0f ? amount / total : 0f;
    }

    // Live-computed so the field's polarity (0 = adjacent/closest possible, high = far or
    // none) is unmissable without hunting for a tooltip — this is exactly the confusion a
    // "0 enemies nearby" reading of a 0-hex-distance field caused.
    private string ProximityHint(float distance, string proximityMaxKey, string penaltyPerHexKey = null)
    {
        float max = advisorWeights.TryGetValue(proximityMaxKey, out float v) ? v : UtilityAI.GetDefaultWeight(proximityMaxKey);
        float penalty = penaltyPerHexKey != null
            ? (advisorWeights.TryGetValue(penaltyPerHexKey, out float p) ? p : UtilityAI.GetDefaultWeight(penaltyPerHexKey))
            : 1f;
        if (distance <= 0f) return "= right here (closest possible)";
        float fadesAt = penalty > 0f ? max / penalty : max;
        return distance * penalty < max
            ? $"in range — fades to 0 by {fadesAt:0.#}"
            : "out of range — 0 bonus (too far / none)";
    }

    // Derived, mirroring GetMilitaryEdgeScore: only an army commander whose
    // army is weaker than the enemy's counts as outmatched.
    // Mirrors UtilityAIContextDataBuilder.CacheEnemyTargets exactly: 0 strength while leading no
    // army (so "no army" always counts as outmatched against any enemy), compared against
    // enemy strength via the single OutmatchedStrengthRatio weight — not a second, looser
    // threshold invented just for the widget.
    private bool SimulatedOutmatched()
    {
        float ratio = advisorWeights.TryGetValue(UtilityAI.Keys.OutmatchedStrengthRatio, out float r)
            ? r : UtilityAI.GetDefaultWeight(UtilityAI.Keys.OutmatchedStrengthRatio);
        float myStrength = simLeadingArmy ? simMyArmyStrength : 0f;
        return simEnemyStrength > myStrength * ratio;
    }

    // Mirrors UtilityAIContext.IsEnemyNear (GetDistanceScore(false) > 0).
    private bool SimulatedEnemyNear()
    {
        float proximityMax = advisorWeights.TryGetValue(UtilityAI.Keys.EnemyProximityMax, out float p)
            ? p : UtilityAI.GetDefaultWeight(UtilityAI.Keys.EnemyProximityMax);
        return proximityMax - simEnemyDistance > 0f;
    }

    // Mirrors HTNRegistry's Militaristic.Danger predicate.
    private bool SimulatedDanger() => SimulatedEnemyNear() && SimulatedOutmatched();

    // Economy status derived from liquid wealth (Gold + Resources), using the CURRENT
    // (possibly unsaved) threshold weights — mirrors UtilityAI.EvaluateEconomyStatus.
    private EconomyStatus SimulatedEconomyStatus()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : UtilityAI.GetDefaultWeight(key);
        float liquidWealth = simGoldBuffer + simResourceNetWorth;

        if (liquidWealth < W(UtilityAI.Keys.EconomyCriticalBelow)) return EconomyStatus.Critical;
        if (liquidWealth < W(UtilityAI.Keys.EconomyWeakBelow)) return EconomyStatus.Weak;
        if (liquidWealth < W(UtilityAI.Keys.EconomyStableBelow)) return EconomyStatus.Stable;
        return EconomyStatus.Surplus;
    }

    private string EconomyThresholdsTooltip()
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : UtilityAI.GetDefaultWeight(key);
        float liquidWealth = simGoldBuffer + simResourceNetWorth;

        return $"Derived from Gold + Resources = {liquidWealth:0.#} liquid wealth — this game has no per-turn income of any kind.\n"
            + $"Critical: below {W(UtilityAI.Keys.EconomyCriticalBelow):0.#}\n"
            + $"Weak: below {W(UtilityAI.Keys.EconomyWeakBelow):0.#}\n"
            + $"Stable: below {W(UtilityAI.Keys.EconomyStableBelow):0.#}\n"
            + "Surplus: anything above.\n"
            + "Edit these thresholds in the 'Economic' tab's HTN Parameter.";
    }

    // Exact mirror of UtilityAIContext.GetAdvisorViability under the scenario assumptions above —
    // literally the same terms SimulateScore adds as an advisor's situational bonus, minus
    // the handful tied to one specific action. This is the number HTNRegistry's Viable
    // predicates compare against a threshold at runtime; showing it here, live, next to the
    // weights that compose it, is what makes "Advisors drive HTN" a visible fact instead of
    // a claim in a comment.
    private float SimulateViability(string group)
    {
        float W(string key) => advisorWeights.TryGetValue(key, out float v) ? v : UtilityAI.GetDefaultWeight(key);
        float enemyProximity = Mathf.Max(0f, W(UtilityAI.Keys.EnemyProximityMax) - simEnemyDistance);

        return group switch
        {
            // Economic has no formula: its old situational term was the now-removed
            // tier-reactive Bonus weights, which decided nothing — matches
            // UtilityAIContext's Economic-less set of named viability methods exactly.
            "Militaristic" => enemyProximity + (!simLeadingArmy
                ? W(UtilityAI.Keys.NoArmyPenalty)
                : simEnemyStrength > 0
                    ? MilitaristicEdge(W)
                    : 0f),
            "Intelligence" => Mathf.Max(0f, W(UtilityAI.Keys.EnemyCharacterProximityMax) - simEnemyCharacterDistance)
                + enemyProximity
                + (SimulatedOutmatched() ? W(UtilityAI.Keys.IntelligenceOutmatchedBonus) : 0f),
            "Artifacts" => (1f - Mathf.Clamp01(simArtifactShare)) * W(UtilityAI.Keys.ArtifactScarcityWeight) + enemyProximity,
            "Diplomatic" => Mathf.Max(0f, W(UtilityAI.Keys.NpcProximityMax) - simNpcDistance)
                + enemyProximity
                + (SimulatedOutmatched() ? W(UtilityAI.Keys.DiplomaticOutmatchedBonus) : 0f),
            "Logistics" => Mathf.Max(0f, W(UtilityAI.Keys.LogisticsProximityMax)
                - simDestinationDistance * W(UtilityAI.Keys.LogisticsDistancePenaltyPerHex)),
            "Disruption" => enemyProximity,
            _ => 0f
        };
    }

    private float MilitaristicEdge(Func<string, float> W)
    {
        float strengthDiff = simMyArmyStrength - simEnemyStrength;
        float farPenalty = simEnemyDistance > 1f ? W(UtilityAI.Keys.FarTargetPenalty) : 0f;
        return strengthDiff < 0
            ? Mathf.Max(-10f, strengthDiff / 10f - farPenalty)
            : Mathf.Clamp(strengthDiff / 20f, -5f, 8f) - farPenalty;
    }

    // Named threshold weight per group, matching HTNRegistry's Viable predicates.
    private static string ViabilityThresholdKeyFor(string group) => group switch
    {
        "Militaristic" => UtilityAI.Keys.MilitaristicViabilityThreshold,
        "Diplomatic" => UtilityAI.Keys.DiplomaticViabilityThreshold,
        "Intelligence" => UtilityAI.Keys.IntelligenceViabilityThreshold,
        "Artifacts" => UtilityAI.Keys.ArtifactsViabilityThreshold,
        "Logistics" => UtilityAI.Keys.LogisticsViabilityThreshold,
        "Disruption" => UtilityAI.Keys.DisruptionViabilityThreshold,
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


    private static readonly Regex SpriteTagRegex = new("<sprite name=\"([^\"]+)\">", RegexOptions.IgnoreCase);
    private static readonly Regex OtherTagRegex = new("<[^>]+>");

    // Card rules text embeds TMP <sprite name="X"> tags to show an icon in-game — IMGUI (this
    // editor window) can't render those, and the icon's meaning lives ONLY in the tag's
    // attribute (many cards never repeat the word as plain text, e.g. "gain +1<sprite
    // name=\"mage\"> for 1 turn"), so blank-stripping the tag silently deletes what the bonus
    // was actually for. Show the icon's name in brackets instead of dropping it.
    private static string CleanEffectTextForDisplay(string effect)
    {
        string withNames = SpriteTagRegex.Replace(effect, m => $"[{ObjectNames.NicifyVariableName(m.Groups[1].Value)}]");
        return OtherTagRegex.Replace(withNames, string.Empty).Trim();
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

                // Canonical identity, matching UtilityAI.BuildCardProfileKey(CardData)
                // exactly: reference/injected clones (DeckExplorer's "copy to another subdeck"
                // stamps referenceDeckId/referenceCardId onto a FULL clone — name/action and
                // all — not just an empty stub, contrary to what the runtime-only
                // InjectMissingStartingPcAndLandReferences path produces) must collapse onto
                // the template they point back to, or a designer editing the wrong duplicate
                // row silently authors a profile runtime never reads. deck.deckId is used
                // instead of the per-card card.deckId field for the same robustness reason as
                // before — the per-card field can be blank in hand-edited JSON.
                bool isReference = !string.IsNullOrWhiteSpace(card.referenceDeckId) && card.referenceCardId > 0;
                string canonicalDeckId = isReference ? card.referenceDeckId : deck.deckId;
                int canonicalCardId = isReference ? card.referenceCardId : card.cardId;
                string cardKey = UtilityAI.BuildCardProfileKey(canonicalDeckId, canonicalCardId);
                if (!string.IsNullOrEmpty(cardKey) && !set.ContainsKey(cardKey))
                {
                    string effect = card.GetActionEffectText();
                    set[cardKey] = new CardUsage
                    {
                        cardName = card.name,
                        cardType = card.type,
                        effect = string.IsNullOrWhiteSpace(effect) ? string.Empty : CleanEffectTextForDisplay(effect),
                        difficulty = Mathf.Max(0, card.difficulty),
                        goldCost = Mathf.Max(0, card.goldRequired),
                        deckId = canonicalDeckId,
                        cardId = canonicalCardId
                    };
                }
            }
        }

        return byAction.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Values.OrderBy(c => c.cardName, StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    // All concrete CharacterAction class names — there is no coded default to read anymore
    // (a card's group membership comes entirely from its own configured utilityParameters,
    // see ResolvedGroupsFor), so this no longer needs to instantiate anything.
    private static List<string> BuildActionCatalog()
    {
        return TypeCache.GetTypesDerivedFrom<CharacterAction>()
            .Where(type => type != null && !type.IsAbstract)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadAdvisorConfig()
    {
        advisorWeights.Clear();
        cardProfiles.Clear();
        foreach (UtilityWeightDefinition definition in UtilityAI.KnownWeights)
        {
            advisorWeights[definition.key] = definition.defaultValue;
        }

        if (File.Exists(AdvisorAssetPath))
        {
            try
            {
                UtilityConfigData data = JsonUtility.FromJson<UtilityConfigData>(File.ReadAllText(AdvisorAssetPath));
                if (data?.weights != null)
                {
                    foreach (UtilityWeightEntry entry in data.weights)
                    {
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.key) && advisorWeights.ContainsKey(entry.key))
                        {
                            advisorWeights[entry.key] = entry.value;
                        }
                    }
                }
                if (data?.cardProfiles != null)
                {
                    foreach (CardParameterProfile entry in data.cardProfiles)
                    {
                        if (entry == null) continue;
                        string key = UtilityAI.BuildCardProfileKey(entry.deckId, entry.cardId);
                        if (string.IsNullOrEmpty(key)) continue;

                        List<ActionUtilityParameterModifier> valid = entry.utilityParameters
                            ?.Where(p => p != null && UtilityAIParameters.IsKnown(p.parameter))
                            .Select(p => new ActionUtilityParameterModifier { parameter = p.parameter, multiplier = p.multiplier, bonus = p.bonus })
                            .ToList() ?? new List<ActionUtilityParameterModifier>();

                        CardParameterProfile profile = new()
                        {
                            deckId = entry.deckId,
                            cardId = entry.cardId,
                            cardName = entry.cardName,
                            actionClass = entry.actionClass,
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
        UtilityConfigData data = new()
        {
            weights = UtilityAI.KnownWeights
                .Select(d => new UtilityWeightEntry
                {
                    key = d.key,
                    value = advisorWeights.TryGetValue(d.key, out float v) ? v : d.defaultValue
                })
                .ToList(),
            cardProfiles = cardProfiles.Values
                .OrderBy(p => p.deckId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.cardId)
                .Select(p => new CardParameterProfile
                {
                    deckId = p.deckId,
                    cardId = p.cardId,
                    cardName = p.cardName,
                    actionClass = p.actionClass,
                    scoreBonus = p.scoreBonus,
                    ignoreSituation = p.ignoreSituation,
                    utilityParameters = p.utilityParameters?
                        .Select(m => new ActionUtilityParameterModifier { parameter = m.parameter, multiplier = m.multiplier, bonus = m.bonus })
                        .ToList() ?? new List<ActionUtilityParameterModifier>()
                })
                .ToList()
        };

        WriteJsonAsset(AdvisorAssetPath, JsonUtility.ToJson(data, true));
        UtilityAI.Reload();
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
