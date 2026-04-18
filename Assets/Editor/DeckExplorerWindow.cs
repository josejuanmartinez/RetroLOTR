using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class DeckExplorerWindow : EditorWindow
{
    [Serializable]
    private class FeedbackPayload
    {
        public string deckId;
        public int cardId;
        public string name;
        public string type;
        public string actionRef;
        public string spriteName;
        public string description;
        public string renderedDescription;
        public string requirementsText;
        public string renderedRequirements;
        public string requirementErrors;
        public string rawFields;
    }

    private class DeckEntryView
    {
        public DeckManifestEntry manifest;
        public DeckData deckData;
        public int depth;
    }

    private readonly List<DeckEntryView> deckViews = new();
    private readonly List<CardData> filteredCards = new();
    private readonly Dictionary<string, Sprite> spriteCache = new(StringComparer.OrdinalIgnoreCase);

    private Vector2 deckScroll;
    private Vector2 cardScroll;
    private Vector2 detailScroll;

    private string searchText = string.Empty;
    private int selectedDeckIndex;
    private int selectedCardIndex;
    private bool onlyShowCardsWithActions;
    private bool sortCardsByTypeThenName = true;
    private string editedCardKey;
    private int editedCommanderSkillRequired;
    private int editedAgentSkillRequired;
    private int editedEmissarySkillRequired;
    private int editedMageSkillRequired;
    private int editedLeatherRequired;
    private int editedMountsRequired;
    private int editedTimberRequired;
    private int editedIronRequired;
    private int editedSteelRequired;
    private int editedMithrilRequired;
    private int editedGoldRequired;
    private int editedJokerRequired;

    private TextAsset manifestAsset;
    private GameObject cardPrefabAsset;
    private GameObject previewRoot;
    private GameObject previewCanvasRoot;
    private GameObject previewCardObject;
    private Card previewCardComponent;
    private Camera previewCamera;
    private RenderTexture previewRenderTexture;
    private Rect previewRect;

    [MenuItem("Window/RetroLOTR/Deck Explorer")]
    public static void Open()
    {
        GetWindow<DeckExplorerWindow>("Deck Explorer");
    }

    private void OnEnable()
    {
        RefreshData();
    }

    private void OnDisable()
    {
        ClearPreviewInstance();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (deckViews.Count == 0)
        {
            EditorGUILayout.HelpBox("No deck data found. Check Assets/Resources/Cards.json and deck resource paths.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) RefreshData();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawDeckPane();
        DrawCardPane();
        DrawDetailPane();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            RefreshData();
        }

        bool newOnlyShowCardsWithActions = GUILayout.Toggle(onlyShowCardsWithActions, "Actions only", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (newOnlyShowCardsWithActions != onlyShowCardsWithActions)
        {
            onlyShowCardsWithActions = newOnlyShowCardsWithActions;
            RebuildFilteredCards();
        }

        bool newSort = GUILayout.Toggle(sortCardsByTypeThenName, "Sort by type", EditorStyles.toolbarButton, GUILayout.Width(85));
        if (newSort != sortCardsByTypeThenName)
        {
            sortCardsByTypeThenName = newSort;
            RebuildFilteredCards();
        }

        GUILayout.Space(8);
        GUILayout.Label("Search", GUILayout.Width(45));
        string newSearch = GUILayout.TextField(searchText, EditorStyles.toolbarTextField, GUILayout.MinWidth(180));
        if (!string.Equals(newSearch, searchText, StringComparison.Ordinal))
        {
            searchText = newSearch;
            RebuildFilteredCards();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDeckPane()
    {
        GUILayout.BeginVertical(GUILayout.Width(260));
        EditorGUILayout.LabelField("Decks", EditorStyles.boldLabel);

        deckScroll = EditorGUILayout.BeginScrollView(deckScroll, GUILayout.Width(260));
        for (int i = 0; i < deckViews.Count; i++)
        {
            DeckEntryView view = deckViews[i];
            if (view == null || view.manifest == null) continue;

            string label = BuildDeckLabel(view);
            bool selected = i == selectedDeckIndex;
            GUIStyle style = selected ? EditorStyles.helpBox : EditorStyles.label;

            if (GUILayout.Toggle(selected, label, style))
            {
                if (selectedDeckIndex != i)
                {
                    selectedDeckIndex = i;
                    selectedCardIndex = 0;
                    RebuildFilteredCards();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawCardPane()
    {
        GUILayout.BeginVertical(GUILayout.Width(320));
        EditorGUILayout.LabelField("Cards", EditorStyles.boldLabel);

        if (GetSelectedDeckView() == null)
        {
            EditorGUILayout.HelpBox("Select a deck.", MessageType.Info);
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField($"{filteredCards.Count} card(s)");

        cardScroll = EditorGUILayout.BeginScrollView(cardScroll, GUILayout.Width(320));
        for (int i = 0; i < filteredCards.Count; i++)
        {
            CardData card = filteredCards[i];
            if (card == null) continue;

            bool selected = i == selectedCardIndex;
            string finalizedPrefix = IsCardFinalized(card) ? "✔ " : string.Empty;
            string label = $"{finalizedPrefix}{FormatCardTitle(card.name)}  [{card.GetCardType()}]";
            if (GUILayout.Toggle(selected, label, selected ? EditorStyles.helpBox : EditorStyles.label))
            {
                if (selectedCardIndex != i)
                {
                    selectedCardIndex = i;
                    Repaint();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawDetailPane()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        CardData card = GetSelectedCard();

        if (card == null)
        {
            EditorGUILayout.HelpBox("Select a card to inspect its preview.", MessageType.Info);
            GUILayout.EndVertical();
            return;
        }

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        DrawCardPreview(card);

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        bool finalized = EditorGUILayout.ToggleLeft("Finalized", IsCardFinalized(card));
        if (EditorGUI.EndChangeCheck())
        {
            SetCardFinalized(card, finalized);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Provide Feedback", GUILayout.Width(130)))
        {
            CopyCardFeedbackPayload(card);
        }
        GUILayout.Space(6);
        if (GUILayout.Button("Reload Card", GUILayout.Width(100)))
        {
            ReloadSelectedCard();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Card Data", EditorStyles.boldLabel);
        DrawCardDetails(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editable Requirements", EditorStyles.boldLabel);
        DrawEditableRequirements(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
        DrawActionDetails(card);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Requirement Errors", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildRequirementErrors(card), EditorStyles.textArea, GUILayout.MinHeight(90));

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Raw Fields", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildRawSummary(card), EditorStyles.textArea, GUILayout.MinHeight(110));

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawCardPreview(CardData card)
    {
        Rect box = GUILayoutUtility.GetRect(0f, 540f, GUILayout.ExpandWidth(true));
        GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

        previewRect = new Rect(box.x + 8f, box.y + 8f, box.width - 16f, box.height - 16f);
        try
        {
            UpdateLivePreview(card, previewRect);
        }
        catch (Exception ex)
        {
            EditorGUI.HelpBox(previewRect, $"Preview failed: {ex.Message}", MessageType.Warning);
        }
    }

    private void DrawCardDetails(CardData card)
    {
        EditorGUILayout.LabelField("Name", FormatCardTitle(card.name));
        EditorGUILayout.LabelField("Type", card.GetCardType().ToString());
        EditorGUILayout.LabelField("Deck", card.deckId ?? string.Empty);
        EditorGUILayout.LabelField("Region", card.region ?? string.Empty);
        EditorGUILayout.LabelField("Tags", card.tags != null ? string.Join(", ", card.tags) : string.Empty);
        EditorGUILayout.LabelField("Gold Cost", card.GetTotalGoldCost().ToString());
        EditorGUILayout.LabelField("Costs", BuildCostSummary(card));
        EditorGUILayout.LabelField("Grants", BuildGrantSummary(card));
        EditorGUILayout.LabelField("Requirements text", card.requirementsText ?? string.Empty);
    }

    private void DrawEditableRequirements(CardData card)
    {
        if (card == null) return;

        SyncEditableRequirements(card);

        editedCommanderSkillRequired = EditorGUILayout.IntField("Commander", editedCommanderSkillRequired);
        editedAgentSkillRequired = EditorGUILayout.IntField("Agent", editedAgentSkillRequired);
        editedEmissarySkillRequired = EditorGUILayout.IntField("Emissary", editedEmissarySkillRequired);
        editedMageSkillRequired = EditorGUILayout.IntField("Mage", editedMageSkillRequired);

        GUILayout.Space(4);
        editedLeatherRequired = EditorGUILayout.IntField("Leather", editedLeatherRequired);
        editedTimberRequired = EditorGUILayout.IntField("Timber", editedTimberRequired);
        editedMountsRequired = EditorGUILayout.IntField("Mounts", editedMountsRequired);
        editedIronRequired = EditorGUILayout.IntField("Iron", editedIronRequired);
        editedSteelRequired = EditorGUILayout.IntField("Steel", editedSteelRequired);
        editedMithrilRequired = EditorGUILayout.IntField("Mithril", editedMithrilRequired);
        editedGoldRequired = EditorGUILayout.IntField("Gold", editedGoldRequired);
        editedJokerRequired = EditorGUILayout.IntField("Joker", editedJokerRequired);

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save new requirements", GUILayout.Width(180)))
        {
            SaveNewRequirements(card);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SyncEditableRequirements(CardData card)
    {
        string key = GetEditableRequirementsKey(card);
        if (string.Equals(editedCardKey, key, StringComparison.Ordinal)) return;

        editedCardKey = key;
        editedCommanderSkillRequired = Mathf.Max(0, card.commanderSkillRequired);
        editedAgentSkillRequired = Mathf.Max(0, card.agentSkillRequired);
        editedEmissarySkillRequired = Mathf.Max(0, card.emissarySkillRequired);
        editedMageSkillRequired = Mathf.Max(0, card.mageSkillRequired);
        editedLeatherRequired = Mathf.Max(0, card.leatherRequired);
        editedTimberRequired = Mathf.Max(0, card.timberRequired);
        editedMountsRequired = Mathf.Max(0, card.mountsRequired);
        editedIronRequired = Mathf.Max(0, card.ironRequired);
        editedSteelRequired = Mathf.Max(0, card.steelRequired);
        editedMithrilRequired = Mathf.Max(0, card.mithrilRequired);
        editedGoldRequired = Mathf.Max(0, card.goldRequired);
        editedJokerRequired = Mathf.Max(0, card.jokerRequired);
    }

    private static string GetEditableRequirementsKey(CardData card)
    {
        if (card == null) return string.Empty;
        string deckId = string.IsNullOrWhiteSpace(card.deckId) ? "unknownDeck" : card.deckId.Trim();
        string cardName = string.IsNullOrWhiteSpace(card.name) ? "unknownCard" : card.name.Trim();
        return $"{deckId}:{card.cardId}:{cardName}";
    }

    private void SaveNewRequirements(CardData card)
    {
        DeckEntryView deckView = GetSelectedDeckView();
        if (card == null || deckView?.deckData?.cards == null)
        {
            return;
        }

        CardData target = deckView.deckData.cards.FirstOrDefault(c => c != null && c.cardId == card.cardId);
        if (target == null)
        {
            target = card;
        }

        target.commanderSkillRequired = Mathf.Max(0, editedCommanderSkillRequired);
        target.agentSkillRequired = Mathf.Max(0, editedAgentSkillRequired);
        target.emissarySkillRequired = Mathf.Max(0, editedEmissarySkillRequired);
        target.mageSkillRequired = Mathf.Max(0, editedMageSkillRequired);
        target.leatherRequired = Mathf.Max(0, editedLeatherRequired);
        target.timberRequired = Mathf.Max(0, editedTimberRequired);
        target.mountsRequired = Mathf.Max(0, editedMountsRequired);
        target.ironRequired = Mathf.Max(0, editedIronRequired);
        target.steelRequired = Mathf.Max(0, editedSteelRequired);
        target.mithrilRequired = Mathf.Max(0, editedMithrilRequired);
        target.goldRequired = Mathf.Max(0, editedGoldRequired);
        target.jokerRequired = Mathf.Max(0, editedJokerRequired);

        string assetPath = GetDeckAssetPath(deckView.manifest?.resourcePath);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            Debug.LogWarning("DeckExplorerWindow: could not resolve deck asset path for saving requirements.");
            return;
        }

        string json = JsonUtility.ToJson(deckView.deckData, true);
        File.WriteAllText(assetPath, json);
        AssetDatabase.ImportAsset(ToAssetPath(assetPath), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        ReloadSelectedCard();
        EditorUtility.SetDirty(this);
        Debug.Log($"DeckExplorerWindow: saved new requirements for '{card.name}'.");
    }

    private static string GetDeckAssetPath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return string.Empty;
        string normalized = resourcePath.Trim().Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", $"{normalized}.json"));
    }

    private static string ToAssetPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;
        string dataPath = Path.GetFullPath(Application.dataPath);
        string normalizedFull = Path.GetFullPath(fullPath);
        if (!normalizedFull.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        string relative = normalizedFull.Substring(dataPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"Assets/{relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/')}";
    }

    private void DrawActionDetails(CardData card)
    {
        string actionRef = card.GetActionRef();
        EditorGUILayout.LabelField("Action Ref", string.IsNullOrWhiteSpace(actionRef) ? "(none)" : actionRef);

        CharacterAction action = ResolveAction(actionRef, card);
        if (action == null)
        {
            EditorGUILayout.HelpBox("No action could be resolved for this card.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Resolved Type", action.GetType().Name);
        EditorGUILayout.LabelField("Action Name", action.actionName ?? string.Empty);
        EditorGUILayout.LabelField("Action Description", action.GetDescriptionForCard() ?? string.Empty, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Required Skills", BuildSkillSummary(card));
    }

    private void UpdateLivePreview(CardData card, Rect rect)
    {
        if (card == null)
        {
            EditorGUI.HelpBox(rect, "No card selected.", MessageType.Info);
            return;
        }

        EnsurePreviewObjects(rect);
        if (previewCardComponent == null || previewCamera == null || previewRenderTexture == null)
        {
            EditorGUI.HelpBox(rect, "Card preview could not be created.", MessageType.Warning);
            return;
        }

        previewCardObject.SetActive(true);
        previewCanvasRoot.SetActive(true);
        ApplyPreviewData(card);
        previewCardObject.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        RectTransform cardRect = previewCardObject.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
        }

        previewCamera.backgroundColor = Color.black;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 6f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.Render();
        previewCamera.targetTexture = null;

        GUI.DrawTexture(rect, previewRenderTexture, ScaleMode.StretchToFill, false);
    }

    private void ApplyPreviewData(CardData card)
    {
        if (previewCardComponent == null || card == null) return;

        SetPreviewText("titleText", FormatCardTitle(card.name));
        SetPreviewText("descriptionText", BuildRenderedDescription(card));
        SetPreviewText("requirementsText", BuildRequirementsText(card));

        TextMeshProUGUI requirementsMessage = GetPreviewField<TextMeshProUGUI>("requirementsMessage");
        if (requirementsMessage != null)
        {
            requirementsMessage.text = BuildPreviewRequirementsMessage(card);
            requirementsMessage.color = Color.red;
        }

        Image cardArtImage = GetPreviewField<Image>("cardArtImage");
        if (cardArtImage != null)
        {
            Sprite sprite = ResolveCardArtwork(card);
            cardArtImage.sprite = sprite;
            cardArtImage.enabled = sprite != null;
        }

        Hover hover = GetPreviewField<Hover>("hover");
        if (hover != null)
        {
            hover.Initialize(FormatCardTypeLabel(card.GetCardType()));
        }
    }

    private void SetPreviewText(string fieldName, string text)
    {
        TextMeshProUGUI tmp = GetPreviewField<TextMeshProUGUI>(fieldName);
        if (tmp != null)
        {
            tmp.text = text ?? string.Empty;
            tmp.color = Color.white;
        }
    }

    private T GetPreviewField<T>(string fieldName) where T : class
    {
        if (previewCardComponent == null || string.IsNullOrWhiteSpace(fieldName)) return null;
        FieldInfo field = typeof(Card).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? field.GetValue(previewCardComponent) as T : null;
    }

    private static string BuildPreviewRequirementsMessage(CardData card)
    {
        if (card == null) return string.Empty;

        List<string> messages = new();

        AppendPreviewLevelMessage(messages, "Commander", card.commanderSkillRequired);
        AppendPreviewLevelMessage(messages, "Agent", card.agentSkillRequired);
        AppendPreviewLevelMessage(messages, "Emissary", card.emissarySkillRequired);
        AppendPreviewLevelMessage(messages, "Mage", card.mageSkillRequired);

        List<string> resourceParts = new();
        AppendPreviewResourcePart(resourceParts, "leather", card.leatherRequired);
        AppendPreviewResourcePart(resourceParts, "timber", card.timberRequired);
        AppendPreviewResourcePart(resourceParts, "mounts", card.mountsRequired);
        AppendPreviewResourcePart(resourceParts, "iron", card.ironRequired);
        AppendPreviewResourcePart(resourceParts, "steel", card.steelRequired);
        AppendPreviewResourcePart(resourceParts, "mithril", card.mithrilRequired);

        int goldCost = card.GetTotalGoldCost();
        if (goldCost > 0)
        {
            resourceParts.Add($"{goldCost}<sprite name=\"gold\">");
        }

        if (resourceParts.Count > 0)
        {
            messages.Add($"<sprite name=\"error\">Need {string.Join(string.Empty, resourceParts)}");
        }

        if (!string.IsNullOrWhiteSpace(card.GetActionRef()))
        {
            messages.Add("<sprite name=\"error\">Action conditions not met.");
        }

        if (messages.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", messages);
    }

    private static void AppendPreviewLevelMessage(List<string> messages, string label, int required)
    {
        if (messages == null || required <= 0) return;
        messages.Add($"<sprite name=\"error\">Need {label} {required}.");
    }

    private static void AppendPreviewResourcePart(List<string> parts, string resourceName, int required)
    {
        if (parts == null || required <= 0) return;
        parts.Add($"{required}<sprite name=\"{resourceName}\">");
    }

    private void EnsurePreviewObjects(Rect rect)
    {
        if (cardPrefabAsset == null)
        {
            cardPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObjects/Reusable/Card.prefab");
        }

        if (cardPrefabAsset == null) return;

        if (previewRoot == null)
        {
            previewRoot = new GameObject("DeckExplorerPreviewRoot") { hideFlags = HideFlags.HideAndDontSave };
            previewRoot.layer = 5;
            previewCamera = previewRoot.AddComponent<Camera>();
            previewCamera.hideFlags = HideFlags.HideAndDontSave;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 6f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.black;
            previewCamera.cullingMask = 1 << 5;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;
            previewCamera.transform.position = new Vector3(0f, 0f, -10f);
            previewCamera.transform.rotation = Quaternion.identity;
        }

        if (previewCanvasRoot == null)
        {
            previewCanvasRoot = new GameObject("DeckExplorerPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            previewCanvasRoot.hideFlags = HideFlags.HideAndDontSave;
            previewCanvasRoot.layer = 5;
            previewCanvasRoot.transform.SetParent(previewRoot.transform, false);

            RectTransform canvasRect = previewCanvasRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1200f, 1600f);

            Canvas canvas = previewCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = previewCamera;
            canvas.planeDistance = 10f;
        }

        if (previewCardObject == null)
        {
            previewCardObject = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefabAsset);
            if (previewCardObject == null) return;

            previewCardObject.hideFlags = HideFlags.HideAndDontSave;
            previewCardObject.layer = 5;
            previewCardObject.transform.SetParent(previewCanvasRoot.transform, false);
            previewCardObject.SetActive(true);

            RectTransform cardRect = previewCardObject.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(390f, 555f);
                cardRect.anchoredPosition = new Vector2(0f, -45f);
                cardRect.localScale = Vector3.one;
            }

            SetHideFlagsRecursive(previewCardObject.transform);
            SetLayerRecursive(previewCardObject.transform, 5);
            previewCardComponent = previewCardObject.GetComponent<Card>();
        }

        EnsureRenderTexture(rect);
    }

    private void EnsureRenderTexture(Rect rect)
    {
        int width = Mathf.Max(1, Mathf.CeilToInt(rect.width));
        int height = Mathf.Max(1, Mathf.CeilToInt(rect.height));
        if (previewRenderTexture != null && previewRenderTexture.width == width && previewRenderTexture.height == height)
        {
            return;
        }

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
        }

        previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static void SetHideFlagsRecursive(Transform root)
    {
        if (root == null) return;
        root.gameObject.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < root.childCount; i++)
        {
            SetHideFlagsRecursive(root.GetChild(i));
        }
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursive(root.GetChild(i), layer);
        }
    }

    private void ClearPreviewInstance()
    {
        if (previewCardObject != null)
        {
            DestroyImmediate(previewCardObject);
            previewCardObject = null;
        }

        if (previewCanvasRoot != null)
        {
            DestroyImmediate(previewCanvasRoot);
            previewCanvasRoot = null;
        }

        if (previewRoot != null)
        {
            DestroyImmediate(previewRoot);
            previewRoot = null;
            previewCamera = null;
        }

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
            previewRenderTexture = null;
        }

        previewCardComponent = null;
        cardPrefabAsset = null;
    }

    private string BuildDeckLabel(DeckEntryView view)
    {
        if (view == null || view.manifest == null) return string.Empty;

        string deckId = string.IsNullOrWhiteSpace(view.manifest.deckId) ? "(no id)" : view.manifest.deckId;
        string nation = string.IsNullOrWhiteSpace(view.manifest.nation) ? "(no nation)" : view.manifest.nation;
        string parent = string.IsNullOrWhiteSpace(view.manifest.parentDeckId) ? string.Empty : $" <- {view.manifest.parentDeckId}";
        string indent = new string(' ', Mathf.Max(0, view.depth) * 2);
        return $"{indent}{nation} / {deckId}{parent} ({view.manifest.cardCount} cards)";
    }

    private void RefreshData()
    {
        manifestAsset = Resources.Load<TextAsset>("Cards");
        deckViews.Clear();
        filteredCards.Clear();
        selectedDeckIndex = Mathf.Clamp(selectedDeckIndex, 0, int.MaxValue);
        selectedCardIndex = 0;
        spriteCache.Clear();

        if (manifestAsset == null)
        {
            Repaint();
            return;
        }

        CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (manifest?.decks == null)
        {
            Repaint();
            return;
        }

        Dictionary<string, DeckEntryView> byId = new(StringComparer.OrdinalIgnoreCase);
        foreach (DeckManifestEntry entry in manifest.decks.Where(x => x != null))
        {
            DeckEntryView view = new()
            {
                manifest = entry,
                deckData = LoadDeckData(entry.resourcePath),
                depth = 0
            };

            if (view.deckData != null && view.deckData.cards != null && view.deckData.cards.Count > 0)
            {
                entry.cardCount = view.deckData.cards.Count;
            }

            deckViews.Add(view);
            if (!string.IsNullOrWhiteSpace(entry.deckId))
            {
                byId[entry.deckId] = view;
            }
        }

        foreach (DeckEntryView view in deckViews)
        {
            if (view == null || view.manifest == null) continue;
            int depth = 0;
            string parentId = view.manifest.parentDeckId;
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            while (!string.IsNullOrWhiteSpace(parentId) && byId.TryGetValue(parentId, out DeckEntryView parent) && visited.Add(parentId))
            {
                depth++;
                parentId = parent?.manifest?.parentDeckId;
            }
            view.depth = depth;
        }

        selectedDeckIndex = deckViews.Count > 0 ? Mathf.Clamp(selectedDeckIndex, 0, deckViews.Count - 1) : 0;
        RebuildFilteredCards();
        Repaint();
    }

    private void ReloadSelectedCard()
    {
        DeckEntryView deckView = GetSelectedDeckView();
        CardData selectedCard = GetSelectedCard();
        if (deckView == null || deckView.manifest == null || selectedCard == null)
        {
            return;
        }

        int selectedCardId = selectedCard.cardId;
        string selectedCardName = selectedCard.name;
        string selectedActionRef = selectedCard.GetActionRef();

        DeckData reloadedDeck = LoadDeckData(deckView.manifest.resourcePath);
        if (reloadedDeck == null)
        {
            return;
        }

        deckView.deckData = reloadedDeck;
        if (deckView.manifest != null && reloadedDeck.cards != null)
        {
            deckView.manifest.cardCount = reloadedDeck.cards.Count;
        }

        RebuildFilteredCards();

        int matchIndex = FindCardIndexInFilteredCards(selectedCardId, selectedCardName, selectedActionRef);
        if (matchIndex >= 0)
        {
            selectedCardIndex = matchIndex;
        }

        Repaint();
    }

    private DeckData LoadDeckData(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        TextAsset deckAsset = Resources.Load<TextAsset>(resourcePath);
        if (deckAsset == null) return null;
        return JsonUtility.FromJson<DeckData>(deckAsset.text);
    }

    private void RebuildFilteredCards()
    {
        filteredCards.Clear();

        DeckEntryView deckView = GetSelectedDeckView();
        if (deckView?.deckData?.cards == null) return;

        IEnumerable<CardData> cards = deckView.deckData.cards.Where(c => c != null);
        if (onlyShowCardsWithActions)
        {
            cards = cards.Where(c => !string.IsNullOrWhiteSpace(c.GetActionRef()));
        }

        string query = string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            cards = cards.Where(card => MatchesSearch(card, query));
        }

        if (sortCardsByTypeThenName)
        {
            cards = cards
                .OrderBy(c => c.GetCardType().ToString())
                .ThenBy(c => c.name, StringComparer.OrdinalIgnoreCase);
        }

        filteredCards.AddRange(cards);
        if (filteredCards.Count == 0)
        {
            selectedCardIndex = 0;
        }
        else
        {
            selectedCardIndex = Mathf.Clamp(selectedCardIndex, 0, filteredCards.Count - 1);
        }
        Repaint();
    }

    private bool MatchesSearch(CardData card, string query)
    {
        if (card == null || string.IsNullOrWhiteSpace(query)) return true;

        StringComparison cmp = StringComparison.OrdinalIgnoreCase;
        return (card.name != null && card.name.Contains(query, cmp))
            || (card.description != null && card.description.Contains(query, cmp))
            || (card.requirementsText != null && card.requirementsText.Contains(query, cmp))
            || (card.action != null && card.action.Contains(query, cmp))
            || (card.actionClassName != null && card.actionClassName.Contains(query, cmp))
            || (card.deckId != null && card.deckId.Contains(query, cmp))
            || (card.region != null && card.region.Contains(query, cmp))
            || (card.tags != null && card.tags.Any(tag => tag != null && tag.Contains(query, cmp)));
    }

    private DeckEntryView GetSelectedDeckView()
    {
        if (deckViews.Count == 0) return null;
        selectedDeckIndex = Mathf.Clamp(selectedDeckIndex, 0, deckViews.Count - 1);
        return deckViews[selectedDeckIndex];
    }

    private CardData GetSelectedCard()
    {
        if (filteredCards.Count == 0) return null;
        selectedCardIndex = Mathf.Clamp(selectedCardIndex, 0, filteredCards.Count - 1);
        return filteredCards[selectedCardIndex];
    }

    private int FindCardIndexInFilteredCards(int cardId, string cardName, string actionRef)
    {
        if (filteredCards.Count == 0) return -1;

        for (int i = 0; i < filteredCards.Count; i++)
        {
            CardData candidate = filteredCards[i];
            if (candidate == null) continue;

            if (cardId > 0 && candidate.cardId == cardId)
            {
                return i;
            }

            if (!string.IsNullOrWhiteSpace(cardName) && string.Equals(candidate.name, cardName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(actionRef) || string.Equals(candidate.GetActionRef(), actionRef, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private Sprite ResolveCardArtwork(CardData data)
    {
        if (data == null) return null;

        string[] candidates =
        {
            data.spriteName,
            data.portraitName,
            data.name,
            data.actionClassName,
            data.action
        };

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (spriteCache.TryGetValue(candidate, out Sprite cached))
            {
                if (cached != null) return cached;
            }

            Sprite found = FindSprite(candidate);
            spriteCache[candidate] = found;
            if (found != null) return found;
        }

        return null;
    }

    private Sprite FindSprite(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        string[] searchRoots =
        {
            "Assets/Art/Cards",
            "Assets/Art/UI",
            "Assets/Art/Animation"
        };

        string normalizedTarget = Normalize(name);
        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", searchRoots);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path)) continue;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is not Sprite sprite) continue;
                if (string.Equals(sprite.name, name, StringComparison.OrdinalIgnoreCase) || Normalize(sprite.name) == normalizedTarget)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private static CharacterAction ResolveAction(string actionRef, CardData card)
    {
        if (string.IsNullOrWhiteSpace(actionRef)) return null;

        Type resolved = ResolveActionType(actionRef);
        if (resolved == null || !typeof(CharacterAction).IsAssignableFrom(resolved)) return null;

        try
        {
            CharacterAction action = Activator.CreateInstance(resolved) as CharacterAction;
            if (action == null) return null;
            action.Initialize(null, card);
            return action;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DeckExplorerWindow: failed to instantiate {actionRef}: {e.Message}");
            return null;
        }
    }

    private static Type ResolveActionType(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;

        Type direct = Type.GetType(className, false, true);
        if (direct != null) return direct;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type candidate = assembly.GetType(className, false, true);
            if (candidate != null) return candidate;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            candidate = types.FirstOrDefault(t => string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
            if (candidate != null) return candidate;
        }

        return null;
    }

    private string BuildRenderedDescription(CardData data)
    {
        if (data == null) return string.Empty;

        CardTypeEnum cardType = data.GetCardType();
        string typePrefix = FormatCardTypeLabel(cardType);
        if (cardType == CardTypeEnum.Character)
        {
            return PrefixWithCardType(typePrefix, data.GetCharacterDescription());
        }
        if (cardType == CardTypeEnum.Army)
        {
            return PrefixWithCardType(typePrefix, data.GetArmyDescription());
        }

        if (cardType == CardTypeEnum.Land || cardType == CardTypeEnum.PC)
        {
            StringBuilder sb = new();
            if (!string.IsNullOrWhiteSpace(data.region))
            {
                sb.Append(data.region).Append(". ");
            }

            List<string> grants = new();
            if (data.leatherGranted > 0) grants.Add(FormatRequirementToken("leather", data.leatherGranted));
            if (data.timberGranted > 0) grants.Add(FormatRequirementToken("timber", data.timberGranted));
            if (data.mountsGranted > 0) grants.Add(FormatRequirementToken("mounts", data.mountsGranted));
            if (data.ironGranted > 0) grants.Add(FormatRequirementToken("iron", data.ironGranted));
            if (data.steelGranted > 0) grants.Add(FormatRequirementToken("steel", data.steelGranted));
            if (data.mithrilGranted > 0) grants.Add(FormatRequirementToken("mithril", data.mithrilGranted));
            if (data.goldGranted > 0) grants.Add(FormatRequirementToken("gold", data.goldGranted));
            sb.Append(string.Join(string.Empty, grants));
            if (cardType == CardTypeEnum.PC)
            {
                sb.Append("\nOR\nLocal Effect");
            }
            return PrefixWithCardType(typePrefix, sb.ToString());
        }

        if (!string.IsNullOrWhiteSpace(data.description))
        {
            return PrefixWithCardType(typePrefix, data.description);
        }

        string actionRef = data.GetActionRef();
        CharacterAction action = ResolveAction(actionRef, data);
        return action != null ? PrefixWithCardType(typePrefix, action.GetDescriptionForCard()) : string.Empty;
    }

    private string BuildRequirementsText(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> reqs = new();

        AppendRequirement(reqs, "commander", data.commanderSkillRequired);
        AppendRequirement(reqs, "agent", data.agentSkillRequired);
        AppendRequirement(reqs, "emmissary", data.emissarySkillRequired);
        AppendRequirement(reqs, "mage", data.mageSkillRequired);

        int totalGold = data.GetTotalGoldCost();
        AppendRequirement(reqs, "gold", totalGold);

        AppendRequirement(reqs, "leather", data.leatherRequired);
        AppendRequirement(reqs, "timber", data.timberRequired);
        AppendRequirement(reqs, "mounts", data.mountsRequired);
        AppendRequirement(reqs, "iron", data.ironRequired);
        AppendRequirement(reqs, "steel", data.steelRequired);
        AppendRequirement(reqs, "mithril", data.mithrilRequired);

        return reqs.Count == 0 ? string.Empty : string.Join(" ", reqs);
    }

    private string BuildSkillSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> reqs = new();
        if (data.commanderSkillRequired > 0) reqs.Add($"Commander {data.commanderSkillRequired}");
        if (data.agentSkillRequired > 0) reqs.Add($"Agent {data.agentSkillRequired}");
        if (data.emissarySkillRequired > 0) reqs.Add($"Emissary {data.emissarySkillRequired}");
        if (data.mageSkillRequired > 0) reqs.Add($"Mage {data.mageSkillRequired}");
        return reqs.Count == 0 ? "None" : string.Join(", ", reqs);
    }

    private string BuildCostSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> parts = new();
        if (data.goldRequired > 0) parts.Add($"gold {data.goldRequired}");
        if (data.leatherRequired > 0) parts.Add($"leather {data.leatherRequired}");
        if (data.timberRequired > 0) parts.Add($"timber {data.timberRequired}");
        if (data.mountsRequired > 0) parts.Add($"mounts {data.mountsRequired}");
        if (data.ironRequired > 0) parts.Add($"iron {data.ironRequired}");
        if (data.steelRequired > 0) parts.Add($"steel {data.steelRequired}");
        if (data.mithrilRequired > 0) parts.Add($"mithril {data.mithrilRequired}");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private string BuildGrantSummary(CardData data)
    {
        if (data == null) return string.Empty;
        List<string> parts = new();
        if (data.leatherGranted > 0) parts.Add($"leather +{data.leatherGranted}");
        if (data.timberGranted > 0) parts.Add($"timber +{data.timberGranted}");
        if (data.mountsGranted > 0) parts.Add($"mounts +{data.mountsGranted}");
        if (data.ironGranted > 0) parts.Add($"iron +{data.ironGranted}");
        if (data.steelGranted > 0) parts.Add($"steel +{data.steelGranted}");
        if (data.mithrilGranted > 0) parts.Add($"mithril +{data.mithrilGranted}");
        if (data.goldGranted > 0) parts.Add($"gold +{data.goldGranted}");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private string BuildRawSummary(CardData card)
    {
        if (card == null) return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine($"cardId: {card.cardId}");
        sb.AppendLine($"name: {card.name}");
        sb.AppendLine($"type: {card.type}");
        sb.AppendLine($"action: {card.GetActionRef()}");
        sb.AppendLine($"spriteName: {card.spriteName}");
        sb.AppendLine($"portraitName: {card.portraitName}");
        sb.AppendLine($"region: {card.region}");
        sb.AppendLine($"requirementsText: {card.requirementsText}");
        sb.AppendLine($"description: {card.description}");
        sb.AppendLine($"historyText: {card.historyText}");
        sb.AppendLine($"tags: {(card.tags != null ? string.Join(", ", card.tags) : string.Empty)}");
        return sb.ToString().TrimEnd();
    }

    private string BuildFeedbackPayloadJson(CardData card)
    {
        if (card == null) return string.Empty;

        FeedbackPayload payload = new()
        {
            deckId = card.deckId,
            cardId = card.cardId,
            name = card.name,
            type = card.type,
            actionRef = card.GetActionRef(),
            spriteName = card.spriteName,
            description = card.description,
            renderedDescription = BuildRenderedDescription(card),
            requirementsText = card.requirementsText,
            renderedRequirements = BuildRequirementsText(card),
            requirementErrors = BuildRequirementErrors(card),
            rawFields = BuildRawSummary(card)
        };

        return JsonUtility.ToJson(payload, true);
    }

    private void CopyCardFeedbackPayload(CardData card)
    {
        string json = BuildFeedbackPayloadJson(card);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        EditorGUIUtility.systemCopyBuffer = $"{json}\n\nPLEASE FIX THIS:\n";
        Debug.Log($"DeckExplorerWindow: copied feedback JSON for '{card?.name}' to clipboard.");
    }

    private string BuildRequirementErrors(CardData card)
    {
        if (card == null) return string.Empty;

        List<string> errors = new();
        Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
        Character selected = board != null ? board.selectedCharacter : null;
        Leader resourceOwner = GetHumanPlayerLeader();

        if (selected == null)
        {
            errors.Add("Select a character first.");
        }
        else
        {
            if (card.commanderSkillRequired > 0 && selected.GetCommander() < card.commanderSkillRequired)
            {
                errors.Add($"Need Commander {card.commanderSkillRequired}.");
            }
            if (card.agentSkillRequired > 0 && selected.GetAgent() < card.agentSkillRequired)
            {
                errors.Add($"Need Agent {card.agentSkillRequired}.");
            }
            if (card.emissarySkillRequired > 0 && selected.GetEmmissary() < card.emissarySkillRequired)
            {
                errors.Add($"Need Emissary {card.emissarySkillRequired}.");
            }
            if (card.mageSkillRequired > 0 && selected.GetMage() < card.mageSkillRequired)
            {
                errors.Add($"Need Mage {card.mageSkillRequired}.");
            }
        }

        if (resourceOwner == null)
        {
            errors.Add("No leader is available to pay the card cost.");
        }
        else
        {
            if (card.leatherRequired > 0 && resourceOwner.leatherAmount < card.leatherRequired)
            {
                errors.Add($"Need {card.leatherRequired}<sprite name=\"leather\">");
            }
            if (card.timberRequired > 0 && resourceOwner.timberAmount < card.timberRequired)
            {
                errors.Add($"Need {card.timberRequired}<sprite name=\"timber\">");
            }
            if (card.mountsRequired > 0 && resourceOwner.mountsAmount < card.mountsRequired)
            {
                errors.Add($"Need {card.mountsRequired}<sprite name=\"mounts\">");
            }
            if (card.ironRequired > 0 && resourceOwner.ironAmount < card.ironRequired)
            {
                errors.Add($"Need {card.ironRequired}<sprite name=\"iron\">");
            }
            if (card.steelRequired > 0 && resourceOwner.steelAmount < card.steelRequired)
            {
                errors.Add($"Need {card.steelRequired}<sprite name=\"steel\">");
            }
            if (card.mithrilRequired > 0 && resourceOwner.mithrilAmount < card.mithrilRequired)
            {
                errors.Add($"Need {card.mithrilRequired}<sprite name=\"mithril\">");
            }

            int goldCost = card.GetTotalGoldCost();
            if (goldCost > 0 && resourceOwner.goldAmount < goldCost)
            {
                errors.Add($"Need {goldCost}<sprite name=\"gold\">");
            }
        }

        if (card.GetCardType() == CardTypeEnum.Land && resourceOwner is PlayableLeader playableLeader && playableLeader.HasPlayedLandThisTurn())
        {
            errors.Add("Only one land card can be played each turn.");
        }

        if (card.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(card.region) && resourceOwner is PlayableLeader playablePcLeader && !playablePcLeader.HasPlayedLandCardForRegion(card.region))
        {
            errors.Add($"{card.region} not discovered yet.");
        }

        if (!string.IsNullOrWhiteSpace(card.GetActionRef()) && selected != null)
        {
            ActionsManager actionsManager = UnityEngine.Object.FindFirstObjectByType<ActionsManager>();
            if (actionsManager != null)
            {
                CharacterAction action = actionsManager.ResolveActionByRef(card.GetActionRef(), card);
                if (action != null)
                {
                    action.Initialize(selected, card);
                    if (!action.FulfillsConditions())
                    {
                        errors.Add("Action conditions not met.");
                    }
                }
            }
        }

        return errors.Count == 0 ? "No requirement errors." : string.Join("\n", errors.Distinct());
    }

    private static string GetCardFinalizedPreferenceKey(CardData card)
    {
        if (card == null) return null;

        string deckId = string.IsNullOrWhiteSpace(card.deckId) ? "unknownDeck" : card.deckId.Trim();
        if (card.cardId > 0)
        {
            return $"RetroLOTR.DeckExplorer.Finalized.{deckId}.{card.cardId}";
        }

        string name = string.IsNullOrWhiteSpace(card.name) ? "unknownCard" : Normalize(card.name);
        string sprite = string.IsNullOrWhiteSpace(card.spriteName) ? "nosprite" : Normalize(card.spriteName);
        string action = string.IsNullOrWhiteSpace(card.GetActionRef()) ? "noaction" : Normalize(card.GetActionRef());
        return $"RetroLOTR.DeckExplorer.Finalized.{deckId}.{name}.{sprite}.{action}";
    }

    private static bool IsCardFinalized(CardData card)
    {
        string key = GetCardFinalizedPreferenceKey(card);
        return !string.IsNullOrWhiteSpace(key) && EditorPrefs.GetBool(key, false);
    }

    private static void SetCardFinalized(CardData card, bool finalized)
    {
        string key = GetCardFinalizedPreferenceKey(card);
        if (string.IsNullOrWhiteSpace(key)) return;
        EditorPrefs.SetBool(key, finalized);
    }

    private static Leader GetHumanPlayerLeader()
    {
        Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
        return game != null ? game.player : null;
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        if (sprite == null)
        {
            EditorGUI.LabelField(rect, "No art", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Texture2D tex = sprite.texture;
        if (tex == null)
        {
            EditorGUI.LabelField(rect, "No texture", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect textureRect = sprite.textureRect;
        float aspect = textureRect.width / textureRect.height;
        Rect drawRect = rect;
        if (drawRect.width / drawRect.height > aspect)
        {
            float width = drawRect.height * aspect;
            drawRect.x += (drawRect.width - width) * 0.5f;
            drawRect.width = width;
        }
        else
        {
            float height = drawRect.width / aspect;
            drawRect.y += (drawRect.height - height) * 0.5f;
            drawRect.height = height;
        }

        GUI.DrawTextureWithTexCoords(drawRect, tex, GetTexCoords(sprite), true);
    }

    private static Rect GetTexCoords(Sprite sprite)
    {
        Texture2D tex = sprite.texture;
        if (tex == null) return new Rect(0, 0, 1, 1);
        Rect r = sprite.textureRect;
        return new Rect(
            r.x / tex.width,
            r.y / tex.height,
            r.width / tex.width,
            r.height / tex.height);
    }

    private static string FormatCardTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (ShouldInsertWordSpace(value, i))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    private static bool ShouldInsertWordSpace(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (index <= 0 || index >= value.Length) return false;

        char current = value[index];
        if (!char.IsUpper(current)) return false;

        char previous = value[index - 1];
        if (char.IsWhiteSpace(previous)) return false;

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        if (!char.IsUpper(previous)) return false;

        if (index + 1 < value.Length && char.IsLower(value[index + 1]))
        {
            return true;
        }

        return false;
    }

    private static string FormatCardTypeLabel(CardTypeEnum cardType)
    {
        string label = cardType switch
        {
            CardTypeEnum.PC => "PC",
            CardTypeEnum.Land => "Land",
            CardTypeEnum.Character => "Character",
            CardTypeEnum.Army => "Army",
            CardTypeEnum.Event => "Event",
            CardTypeEnum.Action => "Action",
            CardTypeEnum.Spell => "Spell",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(label)) return string.Empty;

        Colors colors = UnityEngine.Object.FindFirstObjectByType<Colors>();
        if (colors == null)
        {
            return label;
        }

        string colorName = cardType switch
        {
            CardTypeEnum.PC => "pcCard",
            CardTypeEnum.Land => "landCard",
            CardTypeEnum.Character => "characterCard",
            CardTypeEnum.Army => "armyCard",
            CardTypeEnum.Event => "eventCard",
            CardTypeEnum.Action => "actionCard",
            CardTypeEnum.Spell => "spellCard",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(colorName))
        {
            return label;
        }

        try
        {
            return $"<color={colors.GetHexColorByName(colorName)}>{label}</color>";
        }
        catch
        {
            return label;
        }
    }

    private static string PrefixWithCardType(string typePrefix, string text)
    {
        if (string.IsNullOrWhiteSpace(typePrefix)) return text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return typePrefix;
        return $"{typePrefix}. {text}";
    }

    private static void AppendRequirement(List<string> requirements, string spriteName, int count)
    {
        if (requirements == null || string.IsNullOrWhiteSpace(spriteName) || count <= 0) return;
        requirements.Add($"{count}<sprite name=\"{spriteName}\">");
    }

    private static string FormatRequirementToken(string spriteName, int count)
    {
        return count <= 0 || string.IsNullOrWhiteSpace(spriteName)
            ? string.Empty
            : $"{count}<sprite name=\"{spriteName}\">";
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
