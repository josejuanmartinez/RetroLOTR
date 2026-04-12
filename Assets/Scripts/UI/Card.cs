using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly List<Card> activeCards = new();

    public static void RequestInteractionRefreshAll()
    {
        for (int i = 0; i < activeCards.Count; i++)
        {
            if (activeCards[i] != null)
            {
                activeCards[i].UpdateInteractableState();
            }
        }
    }

    [Header("UI References")]
    [FormerlySerializedAs("title")]
    [SerializeField] private TextMeshProUGUI titleText;
    [FormerlySerializedAs("description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [FormerlySerializedAs("type")]
    [SerializeField] private TextMeshProUGUI typeText;
    [FormerlySerializedAs("requirements")]
    [SerializeField] private TextMeshProUGUI requirementsText;
    [FormerlySerializedAs("image")]
    [SerializeField] private Image cardArtImage;
    [FormerlySerializedAs("borderImage")]
    [SerializeField] private Image cardBackgroundImage;
    [FormerlySerializedAs("disabledReasonHover")]
    [SerializeField] private Image highlightImage;
    [FormerlySerializedAs("button")]
    [SerializeField] private GameObject playIndicator;
    [FormerlySerializedAs("discardButton")]
    [SerializeField] private GameObject shadowObject;

    [Header("Prefabs")]
    [SerializeField] private GameObject dragProxyPrefab;

    [Header("Tuning")]
    [FormerlySerializedAs("HoverScaleMultiplier")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 10f;
    [SerializeField] private float hoverLiftMultiplier = 0.5f;
    [SerializeField] private float dragAlpha = 0.6f;
    [SerializeField] private float playDropThresholdY = 200f;

    public CardData cardData { get; private set; }

    private CanvasGroup canvasGroup;
    private Canvas dragCanvas;
    private LayoutElement layoutElement;
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private Vector2 originalAnchoredPosition = Vector2.zero;
    private bool hoverPositionAdjusted;
    private bool isHovered;
    private bool isDragging;
    private GameObject dragProxy;
    private int originalSiblingIndex;
    private Transform originalParent;
    private SelectedCharacterIcon selectedCharacterIcon;

    private static Illustrations illustrations;
    private static DeckManager deckManager;
    private static ActionsManager actionsManager;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        canvasGroup.alpha = 1f;

        dragCanvas = GetComponent<Canvas>();
        if (dragCanvas == null)
        {
            dragCanvas = gameObject.AddComponent<Canvas>();
        }
        dragCanvas.overrideSorting = false;
        dragCanvas.sortingOrder = 0;

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = false;

        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        targetScale = originalScale;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        BindLegacyPrefabReferences();

        if (highlightImage != null) highlightImage.enabled = false;
        if (playIndicator != null) playIndicator.SetActive(false);

        activeCards.Add(this);
    }

    private void OnEnable()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }

        BindLegacyPrefabReferences();
    }

    private void OnDestroy()
    {
        activeCards.Remove(this);
    }

    private void Start()
    {
        EnsureManagersLoaded();
    }

    private static void EnsureManagersLoaded()
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (actionsManager == null) actionsManager = FindFirstObjectByType<ActionsManager>();
    }

    public void Initialize(CardData data)
    {
        cardData = data;
        EnsureManagersLoaded();
        BindLegacyPrefabReferences();

        if (titleText != null) titleText.text = data.name;
        if (typeText != null) typeText.text = data.type?.ToUpper();

        if (descriptionText != null)
        {
            descriptionText.text = GetActionDescription(data);
        }

        if (requirementsText != null)
        {
            requirementsText.text = BuildRequirementsText(data);
        }

        if (cardArtImage != null)
        {
            Sprite sprite = ResolveCardArtwork(data);
            cardArtImage.sprite = sprite;
            cardArtImage.enabled = sprite != null;
        }

        UpdateInteractableState();
    }

    private Sprite ResolveCardArtwork(CardData data)
    {
        if (data == null || illustrations == null) return null;

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
            if (illustrations.TryGetIllustrationByName(candidate, out Sprite sprite))
            {
                return sprite;
            }
        }

        return null;
    }

    private void BindLegacyPrefabReferences()
    {
        if (titleText == null) titleText = FindTextByName("Title");
        if (descriptionText == null) descriptionText = FindTextByName("Description");
        if (typeText == null) typeText = FindTextByName("Type (1)") ?? FindTextByName("Type");
        if (requirementsText == null) requirementsText = FindTextByName("Requirements");

        if (cardArtImage == null) cardArtImage = FindImageByName("Image");
        if (cardBackgroundImage == null) cardBackgroundImage = FindImageByName("Border");
        if (highlightImage == null) highlightImage = FindImageByName("TitleBackground");

        if (playIndicator == null) playIndicator = FindChildByName("Discard");
        if (shadowObject == null) shadowObject = FindChildByName("Hover");
    }

    private TextMeshProUGUI FindTextByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && string.Equals(texts[i].gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return texts[i];
            }
        }
        return null;
    }

    private Image FindImageByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && string.Equals(images[i].gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return images[i];
            }
        }
        return null;
    }

    private GameObject FindChildByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                return children[i].gameObject;
            }
        }
        return null;
    }

    private string GetActionDescription(CardData data)
    {
        if (data == null) return string.Empty;

        CardTypeEnum cardType = data.GetCardType();
        if (cardType == CardTypeEnum.Land || cardType == CardTypeEnum.PC)
        {
            StringBuilder sb = new();
            List<string> grants = new();
            if (data.leatherGranted > 0) grants.Add(FormatRequirementToken("leather", data.leatherGranted));
            if (data.timberGranted > 0) grants.Add(FormatRequirementToken("timber", data.timberGranted));
            if (data.mountsGranted > 0) grants.Add(FormatRequirementToken("mounts", data.mountsGranted));
            if (data.ironGranted > 0) grants.Add(FormatRequirementToken("iron", data.ironGranted));
            if (data.steelGranted > 0) grants.Add(FormatRequirementToken("steel", data.steelGranted));
            if (data.mithrilGranted > 0) grants.Add(FormatRequirementToken("mithril", data.mithrilGranted));
            if (data.goldGranted > 0) grants.Add(FormatRequirementToken("gold", data.goldGranted));

            if (!string.IsNullOrWhiteSpace(data.region))
            {
                sb.Append(data.region).Append(". ");
            }
            
            sb.Append(string.Join("", grants));

            if (cardType == CardTypeEnum.PC)
            {
                sb.Append("\nOR\nLocal Effect");
            }

            return sb.ToString();
        }

        if (!string.IsNullOrWhiteSpace(data.description)) return data.description;

        string actionRef = data.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return string.Empty;

        CharacterAction action = actionsManager != null ? actionsManager.ResolveActionByRef(actionRef, data) : null;
        return action != null ? action.GetDescriptionForCard() : string.Empty;
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

        if (data.GetCardType() == CardTypeEnum.PC)
        {
            AppendRequirement(reqs, "gold", 10);
            reqs.Add("OR");
            AppendRequirement(reqs, "commander", 2);
            reqs.Add("OR");
            AppendRequirement(reqs, "emmissary", 1);
        }

        if (reqs.Count == 0) return string.Empty;
        return $"<mark=#ffff00>{string.Join(" ", reqs)}</mark>";
    }

    private void AppendRequirement(List<string> requirements, string spriteName, int count)
    {
        if (requirements == null || string.IsNullOrWhiteSpace(spriteName) || count <= 0) return;
        requirements.Add(FormatRequirementToken(spriteName, count));
    }

    private string FormatRequirementToken(string spriteName, int count)
    {
        if (string.IsNullOrWhiteSpace(spriteName) || count <= 0) return string.Empty;
        return $"{count}<sprite name=\"{spriteName}\">";
    }

    public void UpdateInteractableState()
    {
        if (cardData == null) return;

        Board board = FindFirstObjectByType<Board>();
        Character selected = board != null ? board.selectedCharacter : null;

        bool isPlayable = cardData.EvaluatePlayability(selected);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isPlayable ? 1f : 0.5f;
            // For now, let's keep it interactable so they can see why it's not playable
            canvasGroup.interactable = true;
        }

        if (highlightImage != null)
        {
            highlightImage.enabled = isPlayable && isHovered;
        }
    }

    private void Update()
    {
        if (rectTransform.localScale != targetScale)
        {
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * hoverSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = true;
        targetScale = originalScale * hoverScale;
        AdjustHoverPosition(true);
        if (highlightImage != null && cardData != null && cardData.isPlayable) highlightImage.enabled = true;
        Sounds.Instance?.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = false;
        targetScale = originalScale;
        AdjustHoverPosition(false);
        if (highlightImage != null) highlightImage.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            TryPlayCard();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardData == null) return;
        if (canvasGroup != null && !canvasGroup.interactable) return;

        isDragging = true;
        isHovered = false;
        targetScale = originalScale;
        rectTransform.localScale = originalScale;
        AdjustHoverPosition(false);
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        if (dragCanvas != null)
        {
            dragCanvas.overrideSorting = true;
            dragCanvas.sortingOrder = 5000;
        }

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;

        if (shadowObject != null) shadowObject.SetActive(true);
        if (playIndicator != null) playIndicator.SetActive(true);

        rectTransform.SetAsLastSibling();
        UpdateSelectedCharacterDragHint(eventData);
        Sounds.Instance?.PlayUiHover();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        transform.position = eventData.position;

        // Visual feedback if dragged high enough to "play"
        bool overPlayArea = eventData.position.y > playDropThresholdY;
        if (playIndicator != null) playIndicator.SetActive(overPlayArea);
        UpdateSelectedCharacterDragHint(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (shadowObject != null) shadowObject.SetActive(false);
        if (playIndicator != null) playIndicator.SetActive(false);
        if (selectedCharacterIcon != null)
        {
            selectedCharacterIcon.SetDropTargetHighlight(false);
            selectedCharacterIcon = null;
        }

        bool overPlayArea = eventData.position.y > playDropThresholdY;
        bool overSelectedCharacter = IsPointerOverSelectedCharacterIcon(eventData);

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }
        if (dragCanvas != null)
        {
            dragCanvas.overrideSorting = false;
            dragCanvas.sortingOrder = 0;
        }

        if (overPlayArea || overSelectedCharacter)
        {
            TryPlayCard();
        }
        else
        {
            transform.SetSiblingIndex(originalSiblingIndex);
            targetScale = originalScale;
            AdjustHoverPosition(false);
            if (originalParent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }

    private void UpdateSelectedCharacterDragHint(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        SelectedCharacterIcon icon = FindFirstObjectByType<SelectedCharacterIcon>();
        if (icon == null)
        {
            if (selectedCharacterIcon != null)
            {
                selectedCharacterIcon.SetDropTargetHighlight(false);
                selectedCharacterIcon = null;
            }
            return;
        }

        if (selectedCharacterIcon != icon)
        {
            if (selectedCharacterIcon != null)
            {
                selectedCharacterIcon.SetDropTargetHighlight(false);
            }
            selectedCharacterIcon = icon;
            selectedCharacterIcon.SetDropTargetHighlight(true);
        }

        RectTransform iconRect = selectedCharacterIcon.transform as RectTransform;
        if (iconRect == null)
        {
            selectedCharacterIcon.SetDropTargetProximity(0f, false);
            return;
        }

        Vector2 iconCenter = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, iconRect.position);
        float distance = Vector2.Distance(eventData.position, iconCenter);
        float radius = Mathf.Max(iconRect.rect.width, iconRect.rect.height) * 0.7f;
        float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, radius));
        bool locked = RectTransformUtility.RectangleContainsScreenPoint(iconRect, eventData.position, eventData.pressEventCamera);
        selectedCharacterIcon.SetDropTargetProximity(proximity, locked);
        if (locked)
        {
            transform.position = iconRect.position;
        }
    }

    private bool IsPointerOverSelectedCharacterIcon(PointerEventData eventData)
    {
        if (EventSystem.current == null || eventData == null) return false;

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null) continue;
            if (hitObject.GetComponentInParent<SelectedCharacterIcon>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private async void TryPlayCard()
    {
        if (cardData == null) return;
        if (canvasGroup != null && !canvasGroup.interactable) return;

        Board board = FindFirstObjectByType<Board>();
        SelectedCharacterIcon icon = selectedCharacterIcon != null ? selectedCharacterIcon : FindFirstObjectByType<SelectedCharacterIcon>();
        Character selected = icon != null && icon.CurrentCharacter != null
            ? icon.CurrentCharacter
            : board != null ? board.selectedCharacter : null;

        if (!cardData.EvaluatePlayability(selected))
        {
            ShowWhyNotPlayable();
            return;
        }

        bool success = false;
        CardTypeEnum cardType = cardData.GetCardType();

        switch (cardType)
        {
            case CardTypeEnum.Action:
            case CardTypeEnum.Event:
            case CardTypeEnum.Land:
            case CardTypeEnum.PC:
                success = await HandleActionCardPlayed(selected);
                break;
            case CardTypeEnum.Encounter:
                success = await HandleEncounterCardPlayed(selected);
                break;
            case CardTypeEnum.Character:
                success = await HandleCharacterCardPlayed(selected);
                break;
            case CardTypeEnum.Army:
                success = await HandleArmyCardPlayed(selected);
                break;
        }

        if (success)
        {
            TutorialManager.Instance?.HandleCardPlayed(selected, cardData, selected != null ? selected.hex : null);
            // Card was successfully played, it will be removed from hand by the manager
            Destroy(gameObject);
        }
        else
        {
            // Failed to play (e.g. cancelled target selection), return to hand
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }
            if (dragCanvas != null)
            {
                dragCanvas.overrideSorting = false;
                dragCanvas.sortingOrder = 0;
            }
            if (transform.parent != originalParent)
            {
                transform.SetParent(originalParent, false);
            }
            transform.SetSiblingIndex(originalSiblingIndex);
            targetScale = originalScale;
            AdjustHoverPosition(false);
            UpdateInteractableState();
        }
    }

    private void AdjustHoverPosition(bool hovered)
    {
        if (rectTransform == null) return;

        if (hovered)
        {
            if (hoverPositionAdjusted) return;
            originalAnchoredPosition = rectTransform.anchoredPosition;
            float lift = rectTransform.rect.height * Mathf.Max(0f, hoverScale - 1f) * hoverLiftMultiplier;
            rectTransform.anchoredPosition = originalAnchoredPosition + Vector2.up * lift;
            hoverPositionAdjusted = true;
        }
        else
        {
            if (!hoverPositionAdjusted) return;
            rectTransform.anchoredPosition = originalAnchoredPosition;
            hoverPositionAdjusted = false;
        }
    }

    private void ShowWhyNotPlayable()
    {
        if (cardData == null || cardData.playability == null) return;

        StringBuilder sb = new();
        if (cardData.playability.failsLevelRequirements) sb.AppendLine("- Insufficient character level.");
        if (cardData.playability.failsResourceRequirements) sb.AppendLine("- Missing required resources.");
        if (cardData.playability.failsActionConditions) sb.AppendLine("- Action conditions not met.");
        if (cardData.playability.failsCardHistoryRequirements) sb.AppendLine($"- {cardData.playability.cardHistoryReason}");

        if (sb.Length == 0) sb.Append("Cannot play this card right now.");

        // We can show a temporary message or tooltip here
        Board board = FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter != null)
        {
            MessageDisplayNoUI.ShowMessage(board.selectedCharacter.hex, board.selectedCharacter, sb.ToString().Trim(), Color.red);
        }
        
        Sounds.Instance?.PlayActionFail();
    }

    private async Task<bool> HandleActionCardPlayed(Character selected)
    {
        string actionRef = cardData.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        CharacterAction action = actionsManager.ResolveActionByRef(actionRef, cardData);
        if (action == null) return false;

        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        if (playerLeader == null) return false;

        // Try to consume the card from hand first
        // We use the card name now as the ID
        bool drawReplacementCard = !TutorialManager.Instance.IsActiveFor(playerLeader);
        if (!deckManager.TryConsumeActionCard(playerLeader, actionRef, drawReplacementCard, out _, cardData.name))
        {
            return false;
        }

        // Apply any map reveals immediately if it's a Land or PC card
        deckManager.ApplyMapRevealForPlayedCard(playerLeader, cardData);

        // Execute the action
        action.Initialize(selected, cardData);
        await action.Execute();

        if (!action.LastExecutionSucceeded)
        {
            // If the action failed or was cancelled, we should probably return the card to hand,
            // but the current game design usually consumes it anyway on fail.
            // If we want to return it:
            // deckManager.TryReturnActionCardToHand(playerLeader, actionRef);
        }

        return true;
    }

    private async Task<bool> HandleEncounterCardPlayed(Character selected)
    {
        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        if (playerLeader == null) return false;

        bool drawReplacementCard = !TutorialManager.Instance.IsActiveFor(playerLeader);
        if (!deckManager.TryConsumeCard(playerLeader, cardData.name, drawReplacementCard, out _))
        {
            return false;
        }

        bool resolved = await EncounterResolver.ResolveAsync(cardData, selected);
        if (!resolved)
        {
            deckManager.TryReturnCardToHand(playerLeader, cardData.name);
        }

        return resolved;
    }

    private Task<bool> HandleCharacterCardPlayed(Character selected)
    {
        // Character cards usually represent recruiting a specific character
        // This might involve showing a recruitment UI or spawning them at a capital
        return Task.FromResult(false);
    }

    private Task<bool> HandleArmyCardPlayed(Character selected)
    {
        // Army cards represent mustering troops
        return Task.FromResult(false);
    }
}
