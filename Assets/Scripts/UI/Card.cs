using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Image image;
    [SerializeField] CanvasGroup disabledImageCanvasGroup;
    [SerializeField] Image borderImage;
    [SerializeField] Image alignmentImage;
    [SerializeField] Image cardTypeImage;
    [SerializeField] TextMeshProUGUI description;
    [SerializeField] TextMeshProUGUI title;
    [SerializeField] TextMeshProUGUI requirements;
    [SerializeField] Button button;
    [SerializeField] Hover disabledReasonHover;
    [SerializeField] float holdToDragSeconds = 0.08f;
    [SerializeField] float dragScaleMultiplier = 1.15f;
    [SerializeField] float dragTiltDegrees = 7f;
    [SerializeField] float dragJitterPixels = 4f;
    [SerializeField] float draggingAlpha = 0.92f;
    private Illustrations illustrations;
    private Colors colors;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;

    private CardData cardData;
    private bool isConsuming;
    private RectTransform rectTransform;
    private Transform originalParent;
    private int originalSiblingIndexBeforeDrag = -1;
    private Vector3 originalWorldPositionBeforeDrag;
    private Quaternion originalRotationBeforeDrag;
    private Vector3 originalScaleBeforeDrag = Vector3.one;
    private Vector3 dragPointerOffsetWorld;
    private float pointerDownTime;
    private bool pointerIsDown;
    private bool isDragging;
    private float nextInteractionRefreshTime;
    private Vector3 defaultScale = Vector3.one;
    private Vector2 defaultPivot = new Vector2(0.5f, 0.5f);
    private bool isHovered;
    private bool disabled = false;
    private Canvas hoverCanvas;
    private bool hoverCanvasDefaultOverrideSorting;
    private int hoverCanvasDefaultSortingOrder;
    private SelectedCharacterIcon selectedCharacterIcon;

    private const float HoverScaleMultiplier = 1.5f;
    private const float InteractionRefreshInterval = 0.15f;
    private static readonly Vector2 HoverPivot = new Vector2(0.5f, 0f);
    private static Dictionary<string, string> actionDescriptionsByClass;
    private static Dictionary<int, string> actionDescriptionsById;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            defaultScale = rectTransform.localScale;
            defaultPivot = rectTransform.pivot;
        }
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(OnCardClicked);
            button.onClick.AddListener(OnCardClicked);
        }

        if (disabledReasonHover == null)
        {
            disabledReasonHover = GetComponent<Hover>();
        }
        hoverCanvas = GetComponent<Canvas>();
        if (hoverCanvas != null)
        {
            hoverCanvasDefaultOverrideSorting = hoverCanvas.overrideSorting;
            hoverCanvasDefaultSortingOrder = hoverCanvas.sortingOrder;
        }
        else
        {
            hoverCanvasDefaultOverrideSorting = false;
            hoverCanvasDefaultSortingOrder = 0;
        }
        if (disabledReasonHover != null)
        {
            disabledReasonHover.gameObject.SetActive(false);
        }

    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnCardClicked);
    }

    private void OnDisable()
    {
        pointerIsDown = false;
        isDragging = false;
        SetSelectedCharacterDropHint(false);
        RestoreHoverVisuals();
        RestoreCardTransformAfterDrag();
        SetDisabled(false, null);
        SetDisabledOverlayVisible();
    }

    public void Initialize(CardData data)
    {
        cardData = data;
        if (data == null)
        {
            ClearVisuals();
            return;
        }

        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        if (colors == null) colors = FindFirstObjectByType<Colors>();

        if (title != null) title.text = data.name ?? string.Empty;
        CardTypeEnum cardType = data.GetCardType();
        string cardTypeKey = cardType.ToString();
        if (description != null)
        {
            string typeColorHex = "FFFFFF";
            if (TryGetCardTypeColor(cardType, out Color typeColor))
            {
                typeColorHex = ColorUtility.ToHtmlStringRGB(typeColor);
            }
            description.text = BuildCardDescriptionText(data, cardTypeKey, typeColorHex.ToLower());
        }

        if (image != null)
        {
            image.sprite = ResolveCardImage(data);
        }

        AlignmentEnum alignmentValue = (AlignmentEnum)data.alignment;
        string alignmentKey = alignmentValue.ToString();
        if (alignmentImage != null) alignmentImage.sprite = GetSprite(alignmentKey);

        if (cardTypeImage != null) cardTypeImage.sprite = GetSprite(cardTypeKey);

        if (borderImage != null && TryGetCardTypeColor(cardType, out Color borderColor))
        {
            borderImage.color = new Color (borderColor.r, borderColor.g, borderColor.g, 0.25f);
        }

        RefreshRequirementsText(data);
        RefreshInteractionState(force: true);
    }

    private void Update()
    {
        if (isDragging || cardData == null) return;
        if (Time.unscaledTime < nextInteractionRefreshTime) return;
        nextInteractionRefreshTime = Time.unscaledTime + InteractionRefreshInterval;
        RefreshInteractionState();
    }

    private bool TryGetCardTypeColor(CardTypeEnum cardType, out Color color)
    {
        color = Color.white;
        if (colors == null) return false;

        switch (cardType)
        {
            case CardTypeEnum.Action:
                color = colors.actionCard;
                return true;
            case CardTypeEnum.Army:
                color = colors.armyCard;
                return true;
            case CardTypeEnum.Character:
                color = colors.characterCard;
                return true;
            case CardTypeEnum.Event:
                color = colors.eventCard;
                return true;
            case CardTypeEnum.Encounter:
                color = colors.eventCard;
                return true;
            case CardTypeEnum.Land:
                color = colors.landCard;
                return true;
            case CardTypeEnum.PC:
                color = colors.pcCard;
                return true;
            case CardTypeEnum.Rest:
                color = colors.spellCard;
                return true;
            default:
            {
                return false;
            }
        }
    }

    private Sprite GetSprite(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || illustrations == null) return null;
        return illustrations.GetIllustrationByName(key);
    }

    private Sprite ResolveCardImage(CardData data)
    {
        if (data == null) return null;
        if (!string.IsNullOrWhiteSpace(data.spriteName))
        {
            Sprite overrideSprite = GetSprite(data.spriteName);
            if (overrideSprite != null) return overrideSprite;
        }

        CardTypeEnum cardType = data.GetCardType();
        if (cardType == CardTypeEnum.Action || cardType == CardTypeEnum.Event || cardType == CardTypeEnum.Encounter)
        {
            return GetSprite(data.GetActionRef()) ?? GetSprite(data.name);
        }

        return GetSprite(data.name);
    }

    private string BuildCardDescriptionText(CardData data, string cardTypeKey, string typeColorHex)
    {
        if (data == null) return string.Empty;

        string actionDescription = TryGetActionDescription(data);
        if (!string.IsNullOrWhiteSpace(actionDescription))
        {
            return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{actionDescription}";
        }

        string jsonDescription = data.description ?? string.Empty;
        return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{jsonDescription}";
    }

    private string TryGetActionDescription(CardData data)
    {
        if (data == null) return null;
        string actionRef = NormalizeActionRef(data.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef) && data.actionId <= 0) return null;

        // Prefer runtime caravan descriptions so gold values reflect current market prices.
        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        CharacterAction runtimeAction = ResolveActionByRef(actionRef, actionsManager);
        if (runtimeAction != null && (runtimeAction.isBuyCaravans || runtimeAction.isSellCaravans))
        {
            Board board = FindFirstObjectByType<Board>();
            Character selected = board != null ? board.selectedCharacter : null;
            runtimeAction.character = selected;

            string runtimeDescription = runtimeAction.GetDescriptionForCard();
            if (!string.IsNullOrWhiteSpace(runtimeDescription))
            {
                return runtimeDescription;
            }
        }

        EnsureActionDescriptionsLoaded();
        if (!string.IsNullOrWhiteSpace(actionRef) && actionDescriptionsByClass.TryGetValue(actionRef, out string byClass))
        {
            return byClass;
        }

        if (data.actionId > 0 && actionDescriptionsById.TryGetValue(data.actionId, out string byId))
        {
            return byId;
        }

        return null;
    }

    private static void EnsureActionDescriptionsLoaded()
    {
        if (actionDescriptionsByClass != null && actionDescriptionsById != null) return;

        actionDescriptionsByClass = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        actionDescriptionsById = new Dictionary<int, string>();

        TextAsset actionsAsset = Resources.Load<TextAsset>("Actions");
        if (actionsAsset == null) return;

        ActionDefinitionCollection collection = JsonUtility.FromJson<ActionDefinitionCollection>(actionsAsset.text);
        if (collection?.actions == null) return;

        foreach (ActionDefinition definition in collection.actions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.description)) continue;
            if (!string.IsNullOrWhiteSpace(definition.className))
            {
                actionDescriptionsByClass[definition.className] = definition.description;
            }
            if (definition.actionId > 0)
            {
                actionDescriptionsById[definition.actionId] = definition.description;
            }
        }
    }

    private void ClearVisuals()
    {
        if (title != null) title.text = string.Empty;
        if (description != null) description.text = string.Empty;

        if (image != null) image.sprite = null;
        if (borderImage != null) borderImage.sprite = null;
        if (alignmentImage != null) alignmentImage.sprite = null;
        if (cardTypeImage != null) cardTypeImage.sprite = null;
        if (requirements != null) requirements.text = string.Empty;
        if (button != null) button.interactable = false;
    }

    private async void OnCardClicked()
    {
        if (isDragging || isConsuming || cardData == null) return;
        await TryPlayCardAsync(promptForConfirmation: true);
    }

    private async Task<bool> TryPlayCardAsync(bool promptForConfirmation)
    {
        if (isDragging || isConsuming || cardData == null) return false;
        if (!TryResolvePlayableAction(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out CharacterAction action))
        {
            RefreshInteractionState();
            return false;
        }

        bool canUse = cardData.EvaluatePlayability(selectedCharacter, null, _ => action.FulfillsConditions());
        if (!canUse)
        {
            RefreshInteractionState();
            return false;
        }

        if (promptForConfirmation)
        {
            string cardLabel = string.IsNullOrWhiteSpace(cardData.name) ? action.actionName : cardData.name;
            bool confirm = await ConfirmationDialog.Ask($"Use {cardLabel} ?", "Yes", "No");
            if (!confirm) return false;
        }

        TutorialManager tutorial = TutorialManager.Instance;
        bool tutorialActive = tutorial != null && tutorial.IsActiveFor(playerLeader);
        int stepIndexBefore = tutorialActive ? tutorial.GetActiveRequiredStepIndex(playerLeader) : -1;
        bool drawReplacementCard = !tutorialActive;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (deckManager == null)
        {
            RefreshInteractionState();
            return false;
        }

        if (!deckManager.TryConsumeActionCard(playerLeader, cardData.GetActionRef(), cardData.actionId, drawReplacementCard, out _, cardData.cardId))
        {
            RefreshInteractionState();
            return false;
        }

        isConsuming = true;
        RefreshInteractionState(force: true);

        await action.Execute();

        if (tutorialActive)
        {
            int stepIndexAfter = tutorial.GetActiveRequiredStepIndex(playerLeader);
            bool transitionedToNextStep = stepIndexAfter != stepIndexBefore;
            if (!transitionedToNextStep)
            {
                deckManager.TryReturnActionCardToHand(playerLeader, cardData.GetActionRef(), cardData.actionId);
            }
        }

        isConsuming = false;
        RefreshInteractionState(force: true);
        return true;
    }

    private bool TryResolvePlayableAction(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out CharacterAction action)
    {
        game = FindFirstObjectByType<Game>();
        playerLeader = game != null ? game.player : null;
        action = null;
        selectedCharacter = null;

        if (game == null || playerLeader == null) return false;
        if (!game.IsPlayerCurrentlyPlaying()) return false;
        if (cardData == null) return false;

        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        selectedCharacter = board != null ? board.selectedCharacter : null;
        if (selectedCharacter == null) return false;
        if (selectedCharacter.GetOwner() != playerLeader) return false;
        if (selectedCharacter.killed) return false;

        string actionRef = NormalizeActionRef(cardData.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        action = ResolveActionByRef(actionRef, actionsManager);
        if (action == null) return false;

        action.Initialize(selectedCharacter);
        action.difficulty = Mathf.Max(0, cardData.difficulty);
        return true;
    }

    private void RefreshRequirementsText(CardData data)
    {
        if (requirements == null) return;
        if (data == null)
        {
            requirements.text = string.Empty;
            return;
        }

        string sprites = BuildRequirementSprites(data);
        requirements.text = string.IsNullOrWhiteSpace(sprites)
            ? string.Empty
            : $"<mark=#ffff00>{sprites}</mark>";
    }

    private static string BuildRequirementSprites(CardData data)
    {
        List<string> parts = new();
        AddRequirement(parts, "commander", data.commanderSkillRequired);
        AddRequirement(parts, "agent", data.agentSkillRequired);
        AddRequirement(parts, "emmissary", data.emissarySkillRequired);
        AddRequirement(parts, "mage", data.mageSkillRequired);
        AddRequirement(parts, "leather", data.leatherRequired);
        AddRequirement(parts, "timber", data.timberRequired);
        AddRequirement(parts, "mounts", data.mountsRequired);
        AddRequirement(parts, "iron", data.ironRequired);
        AddRequirement(parts, "steel", data.steelRequired);
        AddRequirement(parts, "mithril", data.mithrilRequired);
        AddRequirement(parts, "gold", data.goldRequired);
        if (data.jokerRequired > 0 && data.goldRequired <= 0)
        {
            AddRequirement(parts, "gold", 1);
        }
        AddRequirement(parts, "joker", data.jokerRequired);
        return string.Join(" ", parts);
    }

    private static void AddRequirement(List<string> parts, string spriteName, int amount)
    {
        if (parts == null || string.IsNullOrWhiteSpace(spriteName) || amount <= 0) return;
        parts.Add($"{amount}<sprite name=\"{spriteName}\">");
    }

    private void RefreshInteractionState(bool force = false)
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        bool isPlayableNow = IsPlayableNow();
        if (button != null)
        {
            button.interactable = !isDragging && !isConsuming && isPlayableNow;
        }

        if (canvasGroup != null && !isDragging)
        {
            if (force || !Mathf.Approximately(canvasGroup.alpha, 1f))
            {
                canvasGroup.alpha = 1f;
            }
            canvasGroup.blocksRaycasts = true;
        }

        string unavailableReason = isPlayableNow || isConsuming || isDragging ? null : BuildUnavailableReason();
        SetDisabled(!isPlayableNow && !isConsuming && !isDragging, unavailableReason);
    }

    private bool IsPlayableNow()
    {
        return cardData != null
            && TryResolvePlayableAction(out _, out _, out Character selectedCharacter, out CharacterAction action)
            && action != null
            && cardData.EvaluatePlayability(selectedCharacter, null, _ => action.FulfillsConditions());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;
        ApplyHoverVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        RestoreHoverVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerIsDown = true;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerIsDown = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        if (isConsuming || cardData == null) return;
        if (!pointerIsDown) return;
        if (Time.unscaledTime - pointerDownTime < holdToDragSeconds) return;
        if (!IsPlayableNow())
        {
            RefreshInteractionState();
            return;
        }

        RestoreHoverVisuals();
        isDragging = true;
        pointerIsDown = false;
        originalParent = rectTransform.parent;
        originalSiblingIndexBeforeDrag = rectTransform.GetSiblingIndex();
        originalWorldPositionBeforeDrag = rectTransform.position;
        originalRotationBeforeDrag = rectTransform.rotation;
        originalScaleBeforeDrag = rectTransform.localScale;

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        Camera eventCamera = ResolveEventCamera(eventData);
        if (rootCanvas != null)
        {
            rectTransform.SetParent(rootCanvas.transform, true);
        }
        rectTransform.SetAsLastSibling();

        if (TryGetPointerWorldPosition(eventData.position, eventCamera, out Vector3 pointerWorld))
        {
            dragPointerOffsetWorld = rectTransform.position - pointerWorld;
        }
        else
        {
            dragPointerOffsetWorld = Vector3.zero;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = Mathf.Clamp01(draggingAlpha);
        }

        SetSelectedCharacterDropHint(true);
        SetDisabled(false, null);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || rectTransform == null) return;
        Camera eventCamera = ResolveEventCamera(eventData);
        if (!TryGetPointerWorldPosition(eventData.position, eventCamera, out Vector3 pointerWorld)) return;

        float jitterScale = rootCanvas != null ? Mathf.Max(1f, rootCanvas.scaleFactor) : 1f;
        Vector3 jitter = new Vector3(
            Mathf.Sin(Time.unscaledTime * 47f) * dragJitterPixels / jitterScale,
            Mathf.Cos(Time.unscaledTime * 39f) * dragJitterPixels / jitterScale,
            0f
        );

        rectTransform.position = pointerWorld + dragPointerOffsetWorld + jitter;
        rectTransform.localScale = originalScaleBeforeDrag * dragScaleMultiplier;
        rectTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 20f) * dragTiltDegrees);
    }

    public async void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        bool droppedOnSelectedCharacter = IsDroppedOnSelectedCharacter(eventData);
        isDragging = false;
        SetSelectedCharacterDropHint(false);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        if (droppedOnSelectedCharacter)
        {
            bool played = await TryPlayCardAsync(promptForConfirmation: false);
            if (played) return;
        }

        RestoreCardTransformAfterDrag();
        RefreshInteractionState(force: true);
    }

    private void SetDisabled(bool disabled, string reasonText)
    {
        this.disabled = disabled;
        SetDisabledOverlayVisible();

        if (!disabledReasonHover) return;
        disabledReasonHover.Initialize(reasonText, Vector2.one * 40f, 14, TextAlignmentOptions.Center);
        if (disabled && isHovered)
        {
            ShowDisabledReasonHover();
        }
        else
        {
            HideDisabledReasonHover();
        }

    }

    private string BuildUnavailableReason()
    {
        if (cardData == null) return "Card data is missing.";

        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null) return "Game is not initialized.";
        if (!game.IsPlayerCurrentlyPlaying()) return "It is not your turn.";

        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        Character selectedCharacter = board != null ? board.selectedCharacter : null;
        if (selectedCharacter == null) return "Select one of your characters first.";
        if (selectedCharacter.GetOwner() != game.player) return "Selected character is not controlled by you.";
        if (selectedCharacter.killed) return "Selected character is dead.";

        string actionRef = NormalizeActionRef(cardData.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef)) return "This card has no linked action.";

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        CharacterAction action = ResolveActionByRef(actionRef, actionsManager);
        if (action == null) return $"Linked action '{actionRef}' was not found.";

        action.Initialize(selectedCharacter);
        action.difficulty = Mathf.Max(0, cardData.difficulty);

        bool playable = cardData.EvaluatePlayability(selectedCharacter, null, _ => action.FulfillsConditions());
        if (playable) return string.Empty;

        List<string> reasons = new();
        CardPlayabilityResult result = cardData.playability;
        if (result != null)
        {
            if (result.failsLevelRequirements)
            {
                string levelReason = BuildMissingLevelsReason(selectedCharacter);
                if (!string.IsNullOrWhiteSpace(levelReason)) reasons.Add(levelReason);
            }

            if (result.failsResourceRequirements)
            {
                string resourceReason = BuildMissingResourcesReason(selectedCharacter.GetOwner());
                if (!string.IsNullOrWhiteSpace(resourceReason)) reasons.Add(resourceReason);
            }

            if (result.failsActionConditions)
            {
                reasons.Add("No valid target or action condition is not met.");
            }
        }

        if (reasons.Count == 0) reasons.Add("Requirements are not met.");
        return string.Join("<br>", reasons);
    }

    private string BuildMissingLevelsReason(Character selectedCharacter)
    {
        if (selectedCharacter == null || cardData == null) return string.Empty;

        List<string> parts = new();
        AddMissingRequirement(parts, "Commander", cardData.commanderSkillRequired, selectedCharacter.GetCommander());
        AddMissingRequirement(parts, "Agent", cardData.agentSkillRequired, selectedCharacter.GetAgent());
        AddMissingRequirement(parts, "Emmissary", cardData.emissarySkillRequired, selectedCharacter.GetEmmissary());
        AddMissingRequirement(parts, "Mage", cardData.mageSkillRequired, selectedCharacter.GetMage());
        if (parts.Count == 0) return string.Empty;
        return $"Need levels: {string.Join(", ", parts)}";
    }

    private string BuildMissingResourcesReason(Leader owner)
    {
        if (owner == null || cardData == null) return "Not enough resources.";

        List<string> parts = new();
        AddMissingRequirement(parts, "Leather", cardData.leatherRequired, owner.leatherAmount);
        AddMissingRequirement(parts, "Timber", cardData.timberRequired, owner.timberAmount);
        AddMissingRequirement(parts, "Mounts", cardData.mountsRequired, owner.mountsAmount);
        AddMissingRequirement(parts, "Iron", cardData.ironRequired, owner.ironAmount);
        AddMissingRequirement(parts, "Steel", cardData.steelRequired, owner.steelAmount);
        AddMissingRequirement(parts, "Mithril", cardData.mithrilRequired, owner.mithrilAmount);
        AddMissingRequirement(parts, "Gold", cardData.goldRequired, owner.goldAmount);
        if (parts.Count == 0) return "Not enough resources.";
        return $"Need resources: {string.Join(", ", parts)}";
    }

    private static void AddMissingRequirement(List<string> parts, string label, int required, int current)
    {
        if (parts == null || required <= 0 || current >= required) return;
        parts.Add($"{label} {required} (have {current})");
    }

    private CharacterAction ResolveActionByRef(string actionRef, ActionsManager actionsManager = null)
    {
        string normalizedActionRef = NormalizeActionRef(actionRef);
        if (string.IsNullOrWhiteSpace(normalizedActionRef)) return null;

        if (actionsManager == null)
        {
            actionsManager = FindFirstObjectByType<ActionsManager>();
        }

        if (actionsManager != null && actionsManager.characterActions != null && actionsManager.characterActions.Length > 0)
        {
            CharacterAction fromManager = actionsManager.characterActions.FirstOrDefault(candidate =>
                candidate != null && ActionTypeMatchesRef(candidate.GetType(), normalizedActionRef));
            if (fromManager != null) return fromManager;
        }

        CharacterAction[] allActions = FindObjectsByType<CharacterAction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allActions != null && allActions.Length > 0)
        {
            CharacterAction existing = allActions.FirstOrDefault(candidate =>
                candidate != null && ActionTypeMatchesRef(candidate.GetType(), normalizedActionRef));
            if (existing != null) return existing;
        }

        // Fallback: class exists but no component instance was pre-attached.
        Type resolvedType = ResolveActionType(normalizedActionRef);
        if (resolvedType == null || !typeof(CharacterAction).IsAssignableFrom(resolvedType)) return null;

        GameObject host = actionsManager != null ? actionsManager.gameObject : null;
        if (host == null) return null;

        CharacterAction created = host.GetComponent(resolvedType) as CharacterAction;
        if (created == null)
        {
            created = host.AddComponent(resolvedType) as CharacterAction;
        }

        if (created != null && actionsManager != null)
        {
            CharacterAction[] existingArray = actionsManager.characterActions ?? Array.Empty<CharacterAction>();
            if (!existingArray.Contains(created))
            {
                actionsManager.characterActions = existingArray.Concat(new[] { created }).ToArray();
            }
        }

        return created;
    }

    private static string NormalizeActionRef(string actionRef)
    {
        if (string.IsNullOrWhiteSpace(actionRef)) return string.Empty;

        string normalized = actionRef.Trim();
        if (normalized.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 3).Trim();
        }

        int lastDotIndex = normalized.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < normalized.Length - 1)
        {
            normalized = normalized.Substring(lastDotIndex + 1).Trim();
        }

        return normalized;
    }

    private static bool ActionTypeMatchesRef(System.Type candidateType, string normalizedActionRef)
    {
        if (candidateType == null || string.IsNullOrWhiteSpace(normalizedActionRef)) return false;

        if (string.Equals(candidateType.Name, normalizedActionRef, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(candidateType.FullName)
            && string.Equals(candidateType.FullName, normalizedActionRef, System.StringComparison.OrdinalIgnoreCase);
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

            candidate = types.FirstOrDefault(t =>
                string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
            if (candidate != null) return candidate;
        }

        return null;
    }

    private void ApplyHoverVisuals()
    {
        if (isHovered || rectTransform == null) return;

        defaultScale = rectTransform.localScale;
        defaultPivot = rectTransform.pivot;

        rectTransform.pivot = HoverPivot;
        rectTransform.localScale = defaultScale * HoverScaleMultiplier;
        if (hoverCanvas == null)
        {
            hoverCanvas = gameObject.AddComponent<Canvas>();
            hoverCanvasDefaultOverrideSorting = false;
            hoverCanvasDefaultSortingOrder = 0;
        }
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
        if (hoverCanvas != null)
        {
            hoverCanvas.overrideSorting = true;
            hoverCanvas.sortingOrder = 1001;
        }

        isHovered = true;
        if (disabled) ShowDisabledReasonHover();
    }

    private void RestoreHoverVisuals()
    {
        if (!isHovered || rectTransform == null) return;

        rectTransform.localScale = defaultScale;
        rectTransform.pivot = defaultPivot;
        if (hoverCanvas != null)
        {
            hoverCanvas.overrideSorting = hoverCanvasDefaultOverrideSorting;
            hoverCanvas.sortingOrder = hoverCanvasDefaultSortingOrder;
        }

        isHovered = false;
        HideDisabledReasonHover();
    }

    private void ShowDisabledReasonHover()
    {
        if (!disabledReasonHover) return;
        disabledReasonHover.gameObject.SetActive(true);
        if (disabledReasonHover.tooltipPanel != null)
        {
            disabledReasonHover.tooltipPanel.SetActive(true);
        }
    }

    private void HideDisabledReasonHover()
    {
        if (!disabledReasonHover) return;
        if (disabledReasonHover.tooltipPanel != null)
        {
            disabledReasonHover.tooltipPanel.SetActive(false);
        }
        disabledReasonHover.gameObject.SetActive(false);
    }

    private Camera ResolveEventCamera(PointerEventData eventData)
    {
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rootCanvas.worldCamera;
        }
        return eventData != null ? eventData.pressEventCamera : null;
    }

    private bool TryGetPointerWorldPosition(Vector2 pointerPosition, Camera eventCamera, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null) return false;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, pointerPosition, eventCamera, out worldPosition);
    }

    private bool IsDroppedOnSelectedCharacter(PointerEventData eventData)
    {
        if (eventData == null) return false;
        SelectedCharacterIcon selectedIcon = GetSelectedCharacterIcon();
        if (selectedIcon == null || !selectedIcon.gameObject.activeInHierarchy) return false;

        RectTransform targetRect = selectedIcon.transform as RectTransform;
        if (targetRect == null) return false;

        Camera eventCamera = ResolveEventCamera(eventData);
        return RectTransformUtility.RectangleContainsScreenPoint(targetRect, eventData.position, eventCamera);
    }

    private SelectedCharacterIcon GetSelectedCharacterIcon()
    {
        if (selectedCharacterIcon == null)
        {
            selectedCharacterIcon = FindFirstObjectByType<SelectedCharacterIcon>();
        }
        return selectedCharacterIcon;
    }

    private void SetSelectedCharacterDropHint(bool enabled)
    {
        SelectedCharacterIcon icon = GetSelectedCharacterIcon();
        if (icon == null) return;
        icon.SetDropTargetHighlight(enabled);
    }

    private void RestoreCardTransformAfterDrag()
    {
        if (rectTransform == null || originalParent == null) return;

        rectTransform.SetParent(originalParent, true);
        int maxIndex = originalParent.childCount - 1;
        rectTransform.SetSiblingIndex(Mathf.Clamp(originalSiblingIndexBeforeDrag, 0, Mathf.Max(0, maxIndex)));
        rectTransform.position = originalWorldPositionBeforeDrag;
        rectTransform.rotation = originalRotationBeforeDrag;
        rectTransform.localScale = originalScaleBeforeDrag;
        originalParent = null;
        originalSiblingIndexBeforeDrag = -1;
    }


    private void SetDisabledOverlayVisible()
    {
        if(!disabledImageCanvasGroup) return;
        float targetAlpha = disabled ? 0.99f : 0f;
        if (!Mathf.Approximately(disabledImageCanvasGroup.alpha, targetAlpha))
        {
            disabledImageCanvasGroup.alpha = targetAlpha;
        }
    }
}
