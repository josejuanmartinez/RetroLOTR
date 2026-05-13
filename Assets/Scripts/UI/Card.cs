using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    [SerializeField] private Hover hover;
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
    [SerializeField] private GameObject discardButton;
    [SerializeField] private TextMeshProUGUI requirementsMessage;

    [Header("Prefabs")]
    [SerializeField] private GameObject dragProxyPrefab;

    [Header("Tuning")]
    [FormerlySerializedAs("HoverScaleMultiplier")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 10f;
    [SerializeField] private float hoverLiftMultiplier = 0.5f;
    public float HoverLiftMultiplier { get => hoverLiftMultiplier; set => hoverLiftMultiplier = value; }
    public float ZoomYOffset { get; set; }
    [SerializeField] private float dragAlpha = 0.6f;
    [SerializeField] private float playDropThresholdY = 200f;
    [SerializeField] private Color requirementsMessageColor = Color.red;

    public CardData cardData { get; private set; }

    private CanvasGroup canvasGroup;
    private Canvas dragCanvas;
    private LayoutElement layoutElement;
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private Vector2 originalAnchoredPosition = Vector2.zero;
    private Vector2 originalPivot;
    private bool hoverPositionAdjusted;
    private Image hitProxyImage;
    private bool isHovered;
    private bool isDragging;
    private bool hoverSortingRaised;
    private DiscardButtonHoverTracker discardButtonHover;
    private GameObject dragProxy;
    private GameObject zoomProxy;
    private Coroutine zoomPopCoroutine;
    private int originalSiblingIndex;
    private Transform originalParent;
    private SelectedCharacterIcon selectedCharacterIcon;

    private static Illustrations illustrations;
    private static DeckManager deckManager;
    private static ActionsManager actionsManager;
    private static Colors colors;
    private static CursorManager cursorManager;

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

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

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

        CreateHitProxy();
        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();
        EnsureDiscardButtonHoverTracker();

        if (highlightImage != null) highlightImage.enabled = false;
        if (playIndicator != null) playIndicator.SetActive(false);
        if (shadowObject != null) shadowObject.SetActive(false);
        UpdateDiscardButtonState();

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
        RestrictRaycastsToRootCard();
        if (cardData != null)
        {
            UpdateInteractableState();
        }
    }

    private void OnDestroy()
    {
        activeCards.Remove(this);
        DestroyZoomProxy();
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
        if (colors == null) colors = FindFirstObjectByType<Colors>();
        if (cursorManager == null) cursorManager = FindFirstObjectByType<CursorManager>();
    }

    public void Initialize(CardData data)
    {
        cardData = data;
        EnsureManagersLoaded();
        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();

        if (titleText != null) titleText.text = FormatCardTitle(data.name);
        if (hover != null) hover.Initialize(FormatCardTypeLabel(data.GetCardType()));

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
        // if (typeText == null) typeText = FindTextByName("Type (1)") ?? FindTextByName("Type");
        if (requirementsText == null) requirementsText = FindTextByName("Requirements");

        if (cardArtImage == null) cardArtImage = FindImageByName("Image");
        if (cardBackgroundImage == null) cardBackgroundImage = FindImageByName("Border");
        if (highlightImage == null) highlightImage = FindImageByName("TitleBackground");

        if (discardButton == null) discardButton = FindChildByName("Discard");
        if (playIndicator == null) playIndicator = FindChildByName("PlayIndicator");
        if (shadowObject == null) shadowObject = FindChildByName("Hover");
    }

    private void RestrictRaycastsToRootCard()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            if (graphic.gameObject == gameObject) continue;
            if (hover != null && graphic.gameObject == hover.gameObject) continue;
            if (hitProxyImage != null && graphic == hitProxyImage) continue;
            if (graphic.GetComponent<Selectable>() != null) continue;
            graphic.raycastTarget = false;
        }

        if (cardBackgroundImage != null)
        {
            cardBackgroundImage.raycastTarget = true;
        }
        if (hitProxyImage != null)
        {
            hitProxyImage.raycastTarget = isHovered;
        }
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
        string typePrefix = FormatCardTypeLabel(cardType);
        string body = data.GetRenderedDescription(CanShowFoundingText(data));
        if (!string.IsNullOrWhiteSpace(body))
        {
            return PrefixWithCardType(typePrefix, body);
        }

        string actionRef = data.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return string.Empty;

        CharacterAction action = actionsManager != null ? actionsManager.ResolveActionByRef(actionRef, data) : null;
        return action != null ? PrefixWithCardType(typePrefix, action.GetDescriptionForCard()) : string.Empty;
    }

    private string PrefixWithCardType(string typePrefix, string text)
    {
        if (string.IsNullOrWhiteSpace(typePrefix)) return text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return typePrefix;
        return $"{typePrefix}. {text}";
    }

    private string FormatCardTitle(string value)
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

    private static bool CanShowFoundingText(CardData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.name)) return false;

        Board board = FindFirstObjectByType<Board>();
        List<Hex> hexes = board != null ? board.GetHexes() : null;
        if (hexes == null) return false;

        string target = NormalizeLookupKey(data.name);
        if (string.IsNullOrWhiteSpace(target)) return false;

        for (int i = 0; i < hexes.Count; i++)
        {
            PC candidate = hexes[i] != null ? hexes[i].GetPCData() : null;
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.pcName)) continue;
            if (string.Equals(NormalizeLookupKey(candidate.pcName), target, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
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

    private string FormatCardTypeLabel(CardTypeEnum cardType)
    {
        if (colors == null) colors = FindFirstObjectByType<Colors>();

        string label = cardType switch
        {
            CardTypeEnum.PC => "PC",
            CardTypeEnum.Land => "Land",
            CardTypeEnum.Character => "Character",
            CardTypeEnum.Army => "Army",
            CardTypeEnum.Event => "Event",
            CardTypeEnum.Action => "Action",
            CardTypeEnum.Spell => "Spell",
            CardTypeEnum.Encounter => "Encounter",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(label)) return string.Empty;

        string colorName = cardType switch
        {
            CardTypeEnum.PC => "pc",
            CardTypeEnum.Land => "land",
            CardTypeEnum.Character => "character",
            CardTypeEnum.Army => "army",
            CardTypeEnum.Event => "event",
            CardTypeEnum.Action => "action",
            CardTypeEnum.Spell => "spell",
            CardTypeEnum.Encounter => "encounter",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(colorName))
        {
            return label;
        }

        if (colors == null)
        {
            return label;
        }

        return $"<color={colors.GetHexColorByName(colorName)}>{label}</color>";
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

        if (reqs.Count == 0) return string.Empty;
        return $"{string.Join(" ", reqs)}";
    }

    private void AppendRequirement(List<string> requirements, string spriteName, int count)
    {
        if (requirements == null || string.IsNullOrWhiteSpace(spriteName) || count <= 0) return;
        requirements.Add($"{count}<sprite name=\"{spriteName}\">");
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
        Leader resourceOwner = GetHumanPlayerLeader();
        bool actionConditionsMet = true;
        string actionRef = cardData.GetActionRef();

        if (!string.IsNullOrWhiteSpace(actionRef) && actionsManager != null && selected != null)
        {
            CharacterAction action = actionsManager.ResolveActionByRef(actionRef, cardData);
            if (action != null)
            {
                action.Initialize(selected, cardData);
                actionConditionsMet = action.FulfillsConditions();
            }
        }

        bool isPlayable = cardData.EvaluatePlayability(
            selected,
            _ => resourceOwner == null || cardData.MeetsResourceRequirements(resourceOwner),
            _ => actionConditionsMet);

        if (cardData != null && (string.Equals(cardData.name, "AFriendOrThree", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cardData.name, "TheShire", StringComparison.OrdinalIgnoreCase)))
        {
            string selectedName = selected != null ? selected.characterName : "none";
            string selectedHex = selected != null && selected.hex != null ? selected.hex.name : "none";
            Leader owner = selected != null ? selected.GetOwner() : null;
            string ownerName = owner != null ? owner.characterName : "none";
            int ownerGold = owner != null ? owner.goldAmount : -1;
            string resourceOwnerName = resourceOwner != null ? resourceOwner.characterName : "none";
            int resourceOwnerGold = resourceOwner != null ? resourceOwner.goldAmount : -1;
            Debug.Log(
                $"[TutorialDebug] Playability '{cardData.name}' selected='{selectedName}' hex='{selectedHex}' " +
                $"owner='{ownerName}' gold={ownerGold} cardGold={cardData.GetTotalGoldCost()} " +
                $"resourceOwner='{resourceOwnerName}' resourceGold={resourceOwnerGold} " +
                $"playable={isPlayable} level={cardData.playability.failsLevelRequirements == false} " +
                $"resources={cardData.playability.failsResourceRequirements == false} " +
                $"action={cardData.playability.failsActionConditions == false} " +
                $"history={cardData.playability.failsCardHistoryRequirements == false}");
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isPlayable ? 1f : 0.5f;
            canvasGroup.interactable = isPlayable;
            canvasGroup.blocksRaycasts = true;
        }

        if (requirementsMessage != null)
        {
            requirementsMessage.text = isPlayable ? string.Empty : BuildRequirementsMessageText(selected, resourceOwner);
            requirementsMessage.color = isPlayable ? Color.white : requirementsMessageColor;
        }

        if (highlightImage != null)
        {
            highlightImage.enabled = isPlayable && isHovered;
        }

        UpdateDiscardButtonState();
    }

    private void Update()
    {
        if (rectTransform.localScale != targetScale)
        {
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * hoverSpeed);

            if ((rectTransform.localScale - targetScale).sqrMagnitude < 0.000001f)
            {
                rectTransform.localScale = targetScale;
            }
        }

        UpdateHitProxy();

        if (zoomProxy != null && discardButtonHover != null && discardButtonHover.IsHovered)
        {
            DestroyZoomProxy();
            isHovered = false;
            if (highlightImage != null) highlightImage.enabled = false;
            cursorManager?.SetDefaultCursor();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;
        if (discardButtonHover != null && discardButtonHover.IsHovered) return;
        isHovered = true;
        CreateZoomProxy();
        if (highlightImage != null && cardData != null && cardData.isPlayable) highlightImage.enabled = true;
        if (cursorManager != null)
        {
            if (cardData != null && cardData.isPlayable)
                cursorManager.SetDraggableCursor();
            else
                cursorManager.SetDisableCursor();
        }
        Sounds.Instance?.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = false;
        DestroyZoomProxy();
        if (highlightImage != null) highlightImage.enabled = false;
        cursorManager?.SetDefaultCursor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (canvasGroup != null && !canvasGroup.interactable) return;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            TryPlayCard();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardData == null) return;
        if (canvasGroup != null && !canvasGroup.interactable) return;

        DestroyZoomProxy();
        isDragging = true;
        isHovered = false;
        UpdateDiscardButtonState();
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
        UpdateDiscardButtonState();

        if (cursorManager != null)
        {
            if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventData.pressEventCamera))
            {
                if (cardData != null && cardData.isPlayable)
                    cursorManager.SetDraggableCursor();
                else
                    cursorManager.SetDisableCursor();
            }
            else
            {
                cursorManager.SetDefaultCursor();
            }
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (shadowObject != null) shadowObject.SetActive(false);
        if (playIndicator != null) playIndicator.SetActive(false);
        if (selectedCharacterIcon != null)
        {
            selectedCharacterIcon.SetDropTargetHighlight(false);
            selectedCharacterIcon = null;
        }

        bool overSelectedCharacter = IsPointerOverSelectedCharacterIcon(eventData);
        if (overSelectedCharacter)
        {
            SelectedCharacterIcon icon = selectedCharacterIcon != null ? selectedCharacterIcon : FindFirstObjectByType<SelectedCharacterIcon>();
            RectTransform iconRect = icon != null ? icon.transform as RectTransform : null;
            SnapToSelectedCharacterIcon(iconRect);
        }

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }
        if (dragCanvas != null)
        {
            dragCanvas.overrideSorting = false;
            dragCanvas.sortingOrder = 0;
        }

        if (isHovered)
        {
            SetHoverSorting(true);
        }
        else
        {
            SetHoverSorting(false);
        }

        if (overSelectedCharacter)
        {
            TryPlayCard();
        }
        else
        {
            RestoreCardToHandLayout();
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

        Vector3 iconWorldCenter = iconRect.TransformPoint(iconRect.rect.center);
        Vector2 iconCenter = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, iconWorldCenter);
        float distance = Vector2.Distance(eventData.position, iconCenter);
        float radius = Mathf.Max(iconRect.rect.width, iconRect.rect.height) * 0.7f;
        float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, radius));
        bool locked = RectTransformUtility.RectangleContainsScreenPoint(iconRect, eventData.position, eventData.pressEventCamera);
        selectedCharacterIcon.SetDropTargetProximity(proximity, locked);
        if (locked)
        {
            SnapToSelectedCharacterIcon(iconRect);
        }
    }

    private void SnapToSelectedCharacterIcon(RectTransform iconRect)
    {
        if (rectTransform == null || iconRect == null) return;

        Vector3 worldCenter = iconRect.TransformPoint(iconRect.rect.center);
        rectTransform.position = worldCenter;
        rectTransform.localScale = originalScale;
    }

    private void RestoreCardToHandLayout()
    {
        if (rectTransform == null) return;

        if (transform.parent != originalParent && originalParent != null)
        {
            transform.SetParent(originalParent, false);
        }

        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = false;
        }

        rectTransform.localScale = originalScale;
        rectTransform.anchoredPosition = originalAnchoredPosition;
        transform.SetSiblingIndex(originalSiblingIndex);
        targetScale = originalScale;
        AdjustHoverPosition(false);

        if (originalParent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        UpdateInteractableState();
    }

    private void SetHoverSorting(bool active)
    {
        if (dragCanvas == null || isDragging)
        {
            return;
        }

        hoverSortingRaised = active;
        dragCanvas.overrideSorting = active;
        dragCanvas.sortingOrder = active ? 1000 : 0;
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
        Leader resourceOwner = GetHumanPlayerLeader();
        CardData playedCard = cardData;
        Character playedSelected = selected;
        Sprite playedSprite = cardArtImage != null && cardArtImage.sprite != null ? cardArtImage.sprite : ResolveCardArtwork(playedCard);
        bool actionConditionsMet = true;
        string actionRef = playedCard.GetActionRef();
        if (!string.IsNullOrWhiteSpace(actionRef) && actionsManager != null && playedSelected != null)
        {
            CharacterAction action = actionsManager.ResolveActionByRef(actionRef, playedCard);
            if (action != null)
            {
                action.Initialize(playedSelected, playedCard);
                actionConditionsMet = action.FulfillsConditions();
                if (!actionConditionsMet)
                {
                    string hexName = playedSelected.hex != null ? playedSelected.hex.name : "none";
                    string pcName = playedSelected.hex?.GetPCData()?.pcName ?? "none";
                    Debug.LogWarning(
                        $"[TutorialDebug] Action gate failed for card '{playedCard.name}' on '{playedSelected.characterName}' " +
                        $"(hex='{hexName}', pc='{pcName}', commander={playedSelected.GetCommander()}, agent={playedSelected.GetAgent()}, " +
                        $"emmissary={playedSelected.GetEmmissary()}, mage={playedSelected.GetMage()})");
                }
            }
        }

        TutorialManager tutorialManager = TutorialManager.Instance;
        if (tutorialManager != null && playedSelected != null && playedCard != null)
        {
            string tutorialReason = tutorialManager.GetTutorialPlayRestrictionReason(playedSelected.GetOwner() as PlayableLeader, playedSelected, playedCard);
            if (!string.IsNullOrWhiteSpace(tutorialReason))
            {
                Debug.LogWarning($"[TutorialDebug] Card '{playedCard.name}' blocked for '{playedSelected.characterName}': {tutorialReason}");
            }
        }

        if (!playedCard.EvaluatePlayability(
            playedSelected,
            _ => resourceOwner == null || playedCard.MeetsResourceRequirements(resourceOwner),
            _ => actionConditionsMet))
        {
            return;
        }

        bool success = false;
        CardTypeEnum cardType = playedCard.GetCardType();

        switch (cardType)
        {
            case CardTypeEnum.Action:
            case CardTypeEnum.Event:
            case CardTypeEnum.Land:
            case CardTypeEnum.PC:
                success = await HandleActionCardPlayed(playedSelected);
                break;
            case CardTypeEnum.Encounter:
                success = await HandleEncounterCardPlayed(playedSelected);
                break;
            case CardTypeEnum.Character:
                success = await HandleCharacterCardPlayed(playedSelected);
                break;
            case CardTypeEnum.Army:
                success = await HandleArmyCardPlayed(playedSelected);
                break;
        }

        if (!this)
        {
            return;
        }

        if (success)
        {
            playedSelected?.RecordPlayedCard(playedCard, playedSprite);
            TutorialManager.Instance?.HandleCardPlayed(playedSelected, playedCard, playedSelected != null ? playedSelected.hex : null);
            // Card was successfully played, it will be removed from hand by the manager
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
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
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
            }
            transform.SetSiblingIndex(originalSiblingIndex);
            targetScale = originalScale;
            AdjustHoverPosition(false);
            UpdateInteractableState();
        }
    }

    private Leader GetHumanPlayerLeader()
    {
        Game game = FindFirstObjectByType<Game>();
        return game != null ? game.player : null;
    }

    private void UpdateDiscardButtonState()
    {
        if (discardButton == null) return;
        Button btn = discardButton.GetComponent<Button>();
        if (btn == null) return;
        btn.interactable = !isDragging && cardData != null && !cardData.IsEncounterCard();
    }

    public void Discard()
    {
        _ = TryDiscardAsync();
    }

    private async Task<bool> TryDiscardAsync()
    {
        if (cardData == null || isDragging) return false;

        EnsureManagersLoaded();
        if (deckManager == null) return false;

        Leader humanLeader = GetHumanPlayerLeader();
        if (humanLeader is not PlayableLeader playable) return false;

        Game game = FindFirstObjectByType<Game>();
        if (game == null || !game.IsPlayerCurrentlyPlaying()) return false;

        bool confirm = await ConfirmationDialog.AskImmediate($"Discard {cardData.name} for a random resource?", "Yes", "No");
        if (!confirm) return false;

        if (!deckManager.TryDiscardCard(playable, cardData.name, out CardData discarded)) return false;

        GrantRandomResource(playable);

        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
        return true;
    }

    private void GrantRandomResource(PlayableLeader leader)
    {
        if (leader == null || cardData == null) return;

        string[] resourceNames = { "gold", "timber", "leather", "mounts", "iron", "steel", "mithril" };
        string resourceName = resourceNames[UnityEngine.Random.Range(0, resourceNames.Length)];

        switch (resourceName)
        {
            case "gold": leader.AddGold(1); break;
            case "timber": leader.AddTimber(1); break;
            case "leather": leader.AddLeather(1); break;
            case "mounts": leader.AddMounts(1); break;
            case "iron": leader.AddIron(1); break;
            case "steel": leader.AddSteel(1); break;
            case "mithril": leader.AddMithril(1); break;
        }

        string message = $"{cardData.name} transformed into {resourceName}";
        MessageDisplay.ShowMessage(message, Color.yellow);
    }

    private void EnsureDiscardButtonHoverTracker()
    {
        if (discardButton == null) return;
        if (discardButtonHover != null) return;
        discardButtonHover = discardButton.GetComponent<DiscardButtonHoverTracker>();
        if (discardButtonHover == null)
        {
            discardButtonHover = discardButton.AddComponent<DiscardButtonHoverTracker>();
        }
    }

    private void CreateHitProxy()
    {
        if (hitProxyImage != null) return;

        GameObject proxy = new GameObject("HitProxy", typeof(RectTransform), typeof(Image));
        proxy.transform.SetParent(transform, false);

        hitProxyImage = proxy.GetComponent<Image>();
        hitProxyImage.color = Color.clear;
        hitProxyImage.raycastTarget = false;

        RectTransform proxyRect = proxy.GetComponent<RectTransform>();
        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
        proxyRect.pivot = new Vector2(0.5f, 0.5f);
        proxyRect.sizeDelta = rectTransform.rect.size;
    }

    private void UpdateHitProxy()
    {
        if (hitProxyImage == null || !isHovered || isDragging) return;

        Vector2 offset = rectTransform.anchoredPosition - originalAnchoredPosition;
        float sx = rectTransform.localScale.x;
        float sy = rectTransform.localScale.y;

        // When pivot changes, the geometric center of the parent rect shifts.
        // The hit-proxy's (0.5,0.5) anchor reference point follows that shift,
        // so we must subtract it to keep the proxy pinned to the original world position.
        Vector2 anchorCenterOffset = new Vector2(
            (0.5f - rectTransform.pivot.x) * rectTransform.rect.width,
            (0.5f - rectTransform.pivot.y) * rectTransform.rect.height
        );

        hitProxyImage.rectTransform.anchoredPosition = new Vector2(
            sx != 0 ? (-offset.x / sx) - anchorCenterOffset.x : -offset.x,
            sy != 0 ? (-offset.y / sy) - anchorCenterOffset.y : -offset.y
        );
    }

    private void CreateZoomProxy()
    {
        if (zoomProxy != null) return;
        if (discardButtonHover != null && discardButtonHover.IsHovered) return;

        GameObject prefab = dragProxyPrefab != null ? dragProxyPrefab : gameObject;
        zoomProxy = Instantiate(prefab, transform.parent);

        Card proxyCard = zoomProxy.GetComponent<Card>();
        if (proxyCard != null)
        {
            proxyCard.enabled = false;
        }

        CanvasGroup proxyGroup = zoomProxy.GetComponent<CanvasGroup>();
        if (proxyGroup != null)
        {
            proxyGroup.blocksRaycasts = false;
            proxyGroup.interactable = false;
        }

        LayoutElement proxyLayout = zoomProxy.GetComponent<LayoutElement>();
        if (proxyLayout != null)
        {
            proxyLayout.ignoreLayout = true;
        }

        RectTransform proxyRect = zoomProxy.GetComponent<RectTransform>();
        RectTransform originalRect = rectTransform;

        proxyRect.anchorMin = originalRect.anchorMin;
        proxyRect.anchorMax = originalRect.anchorMax;
        proxyRect.pivot = originalRect.pivot;
        proxyRect.sizeDelta = originalRect.sizeDelta;
        proxyRect.anchoredPosition = originalRect.anchoredPosition;

        float height = proxyRect.rect.height;
        float lift = height * hoverLiftMultiplier;
        proxyRect.pivot = new Vector2(0.5f, 0f);
        proxyRect.anchoredPosition = originalRect.anchoredPosition + Vector2.up * (lift - originalRect.pivot.y * height + ZoomYOffset);

        proxyRect.localScale = originalScale * 0.85f;
        if (zoomPopCoroutine != null)
        {
            StopCoroutine(zoomPopCoroutine);
        }
        zoomPopCoroutine = StartCoroutine(AnimateZoomPop(proxyRect, originalScale * hoverScale));

        Canvas proxyCanvas = zoomProxy.GetComponent<Canvas>();
        if (proxyCanvas != null)
        {
            proxyCanvas.overrideSorting = true;
            proxyCanvas.sortingOrder = 1000;
        }

        zoomProxy.transform.SetAsLastSibling();
    }

    private void DestroyZoomProxy()
    {
        if (zoomPopCoroutine != null)
        {
            StopCoroutine(zoomPopCoroutine);
            zoomPopCoroutine = null;
        }
        if (zoomProxy != null)
        {
            if (Application.isPlaying)
                Destroy(zoomProxy);
            else
                DestroyImmediate(zoomProxy);
            zoomProxy = null;
        }
    }

    private IEnumerator AnimateZoomPop(RectTransform proxyRect, Vector3 targetScale)
    {
        float duration = 0.22f;
        float elapsed = 0f;
        Vector3 startScale = originalScale * 0.85f;

        while (elapsed < duration)
        {
            if (proxyRect == null) yield break;
            float t = elapsed / duration;
            float eased = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            proxyRect.localScale = Vector3.LerpUnclamped(startScale, targetScale * 1.08f, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        duration = 0.1f;
        Vector3 overshoot = targetScale * 1.08f;
        while (elapsed < duration)
        {
            if (proxyRect == null) yield break;
            float t = elapsed / duration;
            proxyRect.localScale = Vector3.Lerp(overshoot, targetScale, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (proxyRect != null)
            proxyRect.localScale = targetScale;
    }

    private void AdjustHoverPosition(bool hovered)
    {
        if (rectTransform == null) return;

        if (hovered)
        {
            if (hoverPositionAdjusted) return;
            originalAnchoredPosition = rectTransform.anchoredPosition;
            originalPivot = rectTransform.pivot;

            if (layoutElement != null) layoutElement.ignoreLayout = true;

            float height = rectTransform.rect.height;
            float lift = height * hoverLiftMultiplier;

            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = originalAnchoredPosition + Vector2.up * (lift - originalPivot.y * height);

            if (hitProxyImage != null)
            {
                hitProxyImage.rectTransform.sizeDelta = rectTransform.rect.size;
                hitProxyImage.raycastTarget = true;
            }

            hoverPositionAdjusted = true;
        }
        else
        {
            if (!hoverPositionAdjusted) return;

            rectTransform.pivot = originalPivot;
            rectTransform.anchoredPosition = originalAnchoredPosition;

            if (hitProxyImage != null)
            {
                hitProxyImage.rectTransform.anchoredPosition = Vector2.zero;
                hitProxyImage.raycastTarget = false;
            }

            if (layoutElement != null && !isDragging) layoutElement.ignoreLayout = false;

            hoverPositionAdjusted = false;
        }
    }

    private string BuildRequirementsMessageText(Character selected, Leader resourceOwner)
    {
        if (cardData == null || cardData.playability == null) return string.Empty;

        List<string> messages = new();
        if (cardData.playability.failsLevelRequirements)
        {
            AppendMissingLevelMessages(messages, selected);
        }

        if (cardData.playability.failsResourceRequirements)
        {
            string resourceMessage = BuildMissingResourceMessage(resourceOwner);
            if (!string.IsNullOrWhiteSpace(resourceMessage))
            {
                messages.Add(resourceMessage);
            }
        }

        if (cardData.playability.failsStartingCityRequirement)
        {
            messages.Add($"<sprite name=\"error\">{cardData.playability.startingCityReason}");
        }

        if (cardData.playability.failsActionConditions)
        {
            messages.Add("<sprite name=\"error\">Action conditions not met.");
        }

        if (cardData.playability.failsCardHistoryRequirements)
        {
            string historyReason = string.IsNullOrWhiteSpace(cardData.playability.cardHistoryReason)
                ? "Card history requirements not met."
                : cardData.playability.cardHistoryReason;
            messages.Add($"<sprite name=\"error\">{historyReason}");
        }

        return string.Join("\n", messages);
    }

    private void AppendMissingLevelMessages(List<string> messages, Character selected)
    {
        if (messages == null) return;

        if (selected == null)
        {
            messages.Add("<sprite name=\"error\">Select a character first.");
            return;
        }

        AppendMissingLevelMessage(messages, "Commander", cardData.commanderSkillRequired, selected.GetCommander());
        AppendMissingLevelMessage(messages, "Agent", cardData.agentSkillRequired, selected.GetAgent());
        AppendMissingLevelMessage(messages, "Emissary", cardData.emissarySkillRequired, selected.GetEmmissary());
        AppendMissingLevelMessage(messages, "Mage", cardData.mageSkillRequired, selected.GetMage());
    }

    private void AppendMissingLevelMessage(List<string> messages, string label, int required, int current)
    {
        if (messages == null || required <= 0 || current >= required) return;
        messages.Add($"<sprite name=\"error\">Need {label} {required}.");
    }

    private string BuildMissingResourceMessage(Leader resourceOwner)
    {
        if (cardData == null || cardData.playability == null) return string.Empty;

        if (resourceOwner == null)
        {
            return "<sprite name=\"error\">No leader is available to pay the card cost.";
        }

        List<string> parts = new();
        AppendMissingResourcePart(parts, "leather", cardData.leatherRequired, resourceOwner.leatherAmount);
        AppendMissingResourcePart(parts, "timber", cardData.timberRequired, resourceOwner.timberAmount);
        AppendMissingResourcePart(parts, "mounts", cardData.mountsRequired, resourceOwner.mountsAmount);
        AppendMissingResourcePart(parts, "iron", cardData.ironRequired, resourceOwner.ironAmount);
        AppendMissingResourcePart(parts, "steel", cardData.steelRequired, resourceOwner.steelAmount);
        AppendMissingResourcePart(parts, "mithril", cardData.mithrilRequired, resourceOwner.mithrilAmount);

        int goldCost = cardData.GetTotalGoldCost();
        if (goldCost > 0 && resourceOwner.goldAmount < goldCost)
        {
            parts.Add($"{goldCost}<sprite name=\"gold\">");
        }

        if (parts.Count == 0) return string.Empty;
        return $"<sprite name=\"error\">Need {string.Join(string.Empty, parts)}";
    }

    private void AppendMissingResourcePart(List<string> parts, string resourceName, int required, int current)
    {
        if (parts == null || required <= 0 || current >= required) return;
        parts.Add($"{required}<sprite name=\"{resourceName}\">");
    }

    private async Task<bool> HandleActionCardPlayed(Character selected)
    {
        string actionRef = cardData.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        CharacterAction action = actionsManager.ResolveActionByRef(actionRef, cardData);
        if (action == null) return false;
        if (selected == null) return false;

        action.Initialize(selected, cardData);
        if (!action.FulfillsConditions())
        {
            return false;
        }

        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        if (playerLeader == null) return false;

        // Try to consume the card from hand first
        // We use the card name now as the ID
        bool drawReplacementCard = false;
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
        else
        {
            playerLeader.RecordPlayedCard(cardData);
        }

        return true;
    }

    private async Task<bool> HandleEncounterCardPlayed(Character selected)
    {
        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        if (playerLeader == null) return false;

        bool drawReplacementCard = false;
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
        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        if (playerLeader == null || selected == null)
        {
            return Task.FromResult(false);
        }

        Hex hex = selected.hex;
        PC pc = hex?.GetPCData();
        if (pc == null || !CardNameUtility.Equals(pc.pcName, cardData.startingPC))
        {
            return Task.FromResult(false);
        }

        bool drawReplacementCard = false;
        if (!deckManager.TryConsumeCard(playerLeader, cardData.name, drawReplacementCard, out _))
        {
            return Task.FromResult(false);
        }

        string characterName = cardData.name;
        Character existing = FindCharacterByName(characterName) ?? FindCharacterByGroup(cardData.characterGroup);

        if (existing == null)
        {
            if (!playerLeader.HasCharacterSlot())
            {
                MessageDisplay.ShowMessage("No character slots available.", Color.red);
                return Task.FromResult(false);
            }

            CharacterInstantiator instantiator = FindFirstObjectByType<CharacterInstantiator>();
            if (instantiator == null)
            {
                return Task.FromResult(false);
            }

            BiomeConfig config = new()
            {
                characterName = characterName,
                alignment = (AlignmentEnum)cardData.alignment,
                race = cardData.race,
                sex = SexEnum.Male,
                commander = cardData.commander,
                agent = cardData.agent,
                emmissary = cardData.emmissary,
                mage = cardData.mage,
                artifacts = cardData.artifacts != null ? new List<string>(cardData.artifacts) : new List<string>()
            };

            Character newCharacter = instantiator.InstantiateCharacter(playerLeader, hex, config);
            if (newCharacter == null)
            {
                return Task.FromResult(false);
            }

            newCharacter.startingCharacter = false;
            newCharacter.characterGroup = cardData.characterGroup;
            newCharacter.hasActionedThisTurn = true;
            newCharacter.isPlayerControlled = playerLeader == game.player;
            playerLeader.TryConsumeCharacterSlot();
            hex.RedrawCharacters();

            string joinMessage = $"{characterName} has joined {playerLeader.characterName}.";
            MessageDisplayNoUI.ShowMessage(hex, newCharacter, joinMessage, Color.green, recordRumour: false);

            Rumour rumour = new Rumour
            {
                leader = playerLeader,
                character = newCharacter,
                characterName = characterName,
                rumour = joinMessage,
                v2 = hex.v2
            };
            RumoursManager.AddRumour(rumour, isPublic: false);

            return Task.FromResult(true);
        }
        else
        {
            InspireEffect effect = InspireEffectFactory.CreateFromCardData(cardData);
            if (effect != null)
            {
                effect.Apply(playerLeader);
            }

            string pcName = pc.pcName;
            string inspireMessage = $"The presence of {characterName} inspires {pcName}.";
            MessageDisplayNoUI.ShowMessage(hex, existing, inspireMessage, Color.cyan, recordRumour: false);

            Rumour rumour = new Rumour
            {
                leader = existing.GetOwner() ?? playerLeader,
                character = existing,
                characterName = characterName,
                rumour = inspireMessage,
                v2 = hex.v2
            };
            RumoursManager.AddRumour(rumour, isPublic: false);

            return Task.FromResult(true);
        }
    }

    private static Character FindCharacterByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        return characters.FirstOrDefault(c => c != null && CardNameUtility.Equals(c.characterName, name));
    }

    private static Character FindCharacterByGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group)) return null;
        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        return characters.FirstOrDefault(c => c != null && !c.killed && string.Equals(c.characterGroup, group, System.StringComparison.OrdinalIgnoreCase));
    }

    private Task<bool> HandleArmyCardPlayed(Character selected)
    {
        // Army cards represent mustering troops
        return Task.FromResult(false);
    }

    private class DiscardButtonHoverTracker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool IsHovered { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }
    }
}
