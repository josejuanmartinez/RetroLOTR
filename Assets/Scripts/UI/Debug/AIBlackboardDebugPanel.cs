using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime debug overlay — Ctrl+Tab toggles a top-right panel where typing a character's name
// and pressing Enter (or the Show button) renders that character's current AI blackboard: the
// active HTN branch/leaf, its preferred-parameter scores, advisor viability, the resolved
// target hex, and a live re-score of which cards would currently be most suitable.
//
// Entirely self-built at runtime (no prefab/scene wiring) — bootstrapped via
// RuntimeInitializeOnLoadMethod so dropping this script into the project is enough; no manual
// scene setup required.
//
// Strictly read-only: never calls AITurnController.AdvanceHtnStrategy or anything else that
// mutates AIBlackboard/HTNPlanner state. It only reads whatever the blackboard already holds
// from the AI's last real turn, plus fresh (non-mutating) AIContext/ScoreFullDeck snapshots.
public class AIBlackboardDebugPanel : MonoBehaviour
{
    private const int MaxSuitableCardsShown = 12;

    private GameObject panelRoot;
    private TMP_InputField nameInput;
    private TMP_Text reportText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<AIBlackboardDebugPanel>() != null) return;
        GameObject go = new("AIBlackboardDebugPanel");
        go.AddComponent<AIBlackboardDebugPanel>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        BuildUi();
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(KeyCode.Tab))
        {
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf) nameInput.ActivateInputField();
        }
    }

    private void OnSubmit()
    {
        try
        {
            reportText.text = BuildReport(nameInput.text);
        }
        catch (Exception e)
        {
            reportText.text = $"<color=#ff6666>Error building report: {e.Message}</color>";
        }
    }

    // ------------------------------------------------------------------
    // Report content — read-only inspection only, see class header.
    // ------------------------------------------------------------------

    private static string BuildReport(string typedName)
    {
        if (string.IsNullOrWhiteSpace(typedName)) return "Type a character name and press Enter.";

        Character character = FindObjectsByType<Character>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.characterName)
                && string.Equals(c.characterName, typedName, StringComparison.OrdinalIgnoreCase));

        if (character == null) return $"No character named \"{typedName}\" found.";

        StringBuilder sb = new();
        sb.AppendLine($"<b>{character.characterName}</b>");
        Leader owner = character.GetOwner();
        sb.AppendLine($"Owner: {(owner != null ? owner.characterName : "none")}   Hex: {(character.hex != null ? character.hex.GetHoverV2() : "none")}");
        sb.AppendLine();

        if (owner is not PlayableLeader leader)
        {
            sb.AppendLine("Not AI-controlled (owner is not a PlayableLeader) — no blackboard.");
            return sb.ToString();
        }

        if (!AIBlackboardStore.TryGet(leader, character, out AIBlackboard blackboard))
        {
            sb.AppendLine("No blackboard yet — this character hasn't been processed by an AI turn.");
            return sb.ToString();
        }

        HTNCompoundTask strategyRoot = AIStrategyLibrary.GetStrategyFor(leader);
        HTNPrimitiveTask activePrimitive = HTNPlanner.ResolveActivePrimitive(blackboard.ActiveStack, strategyRoot);
        string stackDescription = blackboard.ActiveStack is { Count: > 0 }
            ? string.Join(" > ", blackboard.ActiveStack.Select(f => $"{f.MethodTaskId}[{f.SubtaskIndex}]"))
            : "(empty)";

        sb.AppendLine("<b>Blackboard</b>");
        sb.AppendLine($"Stack: {stackDescription}");
        sb.AppendLine($"Turns on current task: {blackboard.TurnsOnCurrentTask}");
        sb.AppendLine($"Target hex: {(blackboard.TargetHex != null ? blackboard.TargetHex.GetHoverV2() : "none")}");
        sb.AppendLine();

        sb.AppendLine("<b>Active HTN task</b>");
        if (activePrimitive == null)
        {
            sb.AppendLine("(none resolved)");
        }
        else
        {
            sb.AppendLine($"Task ID: {activePrimitive.TaskId}");
            sb.AppendLine($"Advisor: {(string.IsNullOrEmpty(activePrimitive.AdvisorName) ? "(none)" : activePrimitive.AdvisorName)}");
            sb.AppendLine($"Preferred parameters: {(activePrimitive.PreferredParameters is { Count: > 0 } ? string.Join(", ", activePrimitive.PreferredParameters) : "(none)")}");
        }
        sb.AppendLine();

        // Fresh, non-mutating snapshot — AIContext construction and ScoreFullDeck only read
        // board/leader/character state, they never touch AIBlackboard or execute anything.
        AIContext.AIContextPrecomputedData precomputed = AIContextDataBuilder.Build(leader, character);
        AIContext ctx = new(leader, character, new List<CharacterAction>(), null, precomputed);

        if (activePrimitive?.PreferredParameters is { Count: > 0 })
        {
            sb.AppendLine("<b>Preferred parameter values</b>");
            foreach (string parameter in activePrimitive.PreferredParameters)
            {
                Hex targetHex = ctx.GetTargetHexForParameter(parameter);
                sb.AppendLine($"{parameter}: {ctx.GetUtilityParameter(parameter):0.##}" + (targetHex != null ? $"  @ {targetHex.GetHoverV2()}" : ""));
            }
            sb.AppendLine();
        }

        sb.AppendLine("<b>Advisor viability</b>");
        foreach (AdvisorType advisor in Enum.GetValues(typeof(AdvisorType)))
        {
            if (advisor == AdvisorType.None) continue;
            bool eligible = ctx.HasEligibleCard(advisor);
            sb.AppendLine($"{advisor}: {ctx.GetAdvisorViability(advisor):0.##}{(eligible ? "" : "  <color=#ff8080>(no eligible card)</color>")}");
        }
        sb.AppendLine();

        sb.AppendLine("<b>Cards that would be suitable now</b>");
        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (actionsManager == null || deckManager == null || !deckManager.HasDeckFor(leader))
        {
            sb.AppendLine("(deck/actions manager unavailable)");
        }
        else
        {
            float advisorBiasBonus = AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.HTNBiasBonus);
            List<(CardData card, float score)> scored = AITurnController.ScoreFullDeck(
                leader, character, actionsManager, deckManager, precomputed, new HashSet<CardData>(),
                advisorBiasBonus, activePrimitive?.AdvisorName, activePrimitive?.PreferredParameters);

            if (scored.Count == 0)
            {
                sb.AppendLine("(no playable card found)");
            }
            else
            {
                int rank = 1;
                foreach ((CardData card, float score) in scored.OrderByDescending(s => s.score).Take(MaxSuitableCardsShown))
                {
                    AdvisorType cardAdvisor = AIAdvisorConfig.ResolveAdvisor(AITurnController.ResolveActionByRef(AITurnController.NormalizeActionRef(card.GetActionRef()), actionsManager));
                    sb.AppendLine($"{rank}. {card.name} — {score:0.##}  [{cardAdvisor}]");
                    rank++;
                }
            }
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Runtime UI construction — no prefab available in this environment, so the whole
    // hierarchy (Canvas, panel, input field, scrollable report text) is built in code.
    // ------------------------------------------------------------------

    private void BuildUi()
    {
        GameObject canvasGo = new("AIBlackboardDebugCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        panelRoot = CreatePanel(canvasGo.transform, "Panel", new Color(0f, 0f, 0f, 0.85f), 460f, 620f);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);

        TMP_Text title = CreateText(panelRoot.transform, "Title", "AI Blackboard (Ctrl+Tab to close)", 16, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta = new Vector2(-20f, 26f);

        nameInput = CreateInputField(panelRoot.transform, new Vector2(0f, -44f), 300f);
        nameInput.onSubmit.AddListener(_ => OnSubmit());

        Button showButton = CreateButton(panelRoot.transform, "Show", new Vector2(160f, -44f), 90f);
        showButton.onClick.AddListener(OnSubmit);

        GameObject scrollGo = new("ScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(panelRoot.transform, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(10f, 10f);
        scrollRectTransform.offsetMax = new Vector2(-10f, -76f);
        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewportGo = new("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();

        GameObject contentGo = new("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        reportText = CreateText(contentGo.transform, "ReportText", "Type a character name and press Enter.", 14, FontStyles.Normal);
        reportText.rectTransform.anchorMin = new Vector2(0f, 1f);
        reportText.rectTransform.anchorMax = new Vector2(1f, 1f);
        reportText.rectTransform.pivot = new Vector2(0.5f, 1f);
        reportText.rectTransform.anchoredPosition = Vector2.zero;
        reportText.textWrappingMode = TextWrappingModes.Normal;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, float width, float height)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string initialText, int fontSize, FontStyles style)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        if (FontManager.Instance != null) FontManager.Instance.ApplyCurrentFont(text);
        return text;
    }

    private static TMP_InputField CreateInputField(Transform parent, Vector2 anchoredPosition, float width)
    {
        GameObject fieldGo = new("NameInput", typeof(RectTransform));
        fieldGo.transform.SetParent(parent, false);
        Image bg = fieldGo.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.9f);
        RectTransform fieldRect = fieldGo.GetComponent<RectTransform>();
        fieldRect.anchorMin = fieldRect.anchorMax = new Vector2(0f, 1f);
        fieldRect.pivot = new Vector2(0f, 1f);
        fieldRect.anchoredPosition = anchoredPosition;
        fieldRect.sizeDelta = new Vector2(width, 28f);

        GameObject viewportGo = new("TextArea", typeof(RectTransform));
        viewportGo.transform.SetParent(fieldGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(6f, 4f);
        viewportRect.offsetMax = new Vector2(-6f, -4f);
        viewportGo.AddComponent<RectMask2D>();

        TMP_Text text = CreateText(viewportGo.transform, "Text", string.Empty, 14, FontStyles.Normal);
        text.color = Color.black;
        StretchFull(text.rectTransform);

        TMP_Text placeholder = CreateText(viewportGo.transform, "Placeholder", "Character name...", 14, FontStyles.Italic);
        placeholder.color = new Color(0f, 0f, 0f, 0.5f);
        StretchFull(placeholder.rectTransform);

        TMP_InputField inputField = fieldGo.AddComponent<TMP_InputField>();
        inputField.textViewport = viewportRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        return inputField;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, float width)
    {
        GameObject buttonGo = new("Button", typeof(RectTransform));
        buttonGo.transform.SetParent(parent, false);
        Image bg = buttonGo.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.5f, 0.9f, 1f);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(width, 28f);

        TMP_Text label1 = CreateText(buttonGo.transform, "Label", label, 14, FontStyles.Bold);
        label1.alignment = TextAlignmentOptions.Center;
        StretchFull(label1.rectTransform);

        return buttonGo.AddComponent<Button>();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
