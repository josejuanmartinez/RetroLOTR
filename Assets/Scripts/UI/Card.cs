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
    [SerializeField] private TextMeshProUGUI requirementsMessage;

    [Header("Prefabs")]
    [SerializeField] private GameObject dragProxyPrefab;

    [Header("Tuning")]
    [FormerlySerializedAs("HoverScaleMultiplier")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 10f;
    [SerializeField] private float hoverLiftMultiplier = 0.5f;
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
    private bool hoverPositionAdjusted;
    private bool isHovered;
    private bool isDragging;
    private bool hoverSortingRaised;
    private GameObject dragProxy;
    private int originalSiblingIndex;
    private Transform originalParent;
    private SelectedCharacterIcon selectedCharacterIcon;

    private static Illustrations illustrations;
    private static DeckManager deckManager;
    private static ActionsManager actionsManager;
    private static Colors colors;

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

        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();

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
        RestrictRaycastsToRootCard();
        if (cardData != null)
        {
            UpdateInteractableState();
        }
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
        if (colors == null) colors = FindFirstObjectByType<Colors>();
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

        if (playIndicator == null) playIndicator = FindChildByName("Discard");
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
            if (graphic.GetComponent<Selectable>() != null) continue;
            graphic.raycastTarget = false;
        }

        if (cardBackgroundImage != null)
        {
            cardBackgroundImage.raycastTarget = true;
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
        if (cardType == CardTypeEnum.Character)
        {
            string characterSummary = data.GetCharacterDescription();
            string characterBody = !string.IsNullOrWhiteSpace(data.description) ? data.description : string.Empty;
            if (string.IsNullOrWhiteSpace(characterSummary))
            {
                return PrefixWithCardType(typePrefix, characterBody);
            }

            return string.IsNullOrWhiteSpace(characterBody)
                ? PrefixWithCardType(typePrefix, characterSummary)
                : PrefixWithCardType(typePrefix, $"{characterSummary}. {characterBody}");
        }
        if (cardType == CardTypeEnum.Army)
        {
            return PrefixWithCardType(typePrefix, data.GetArmyDescription());
        }

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
                sb.Append("\nOR\n");
                PcEffectCatalog.PcEffectDefinition pcEffect = PcEffectCatalog.GetDefinition(data.pcEffectId);
                if (pcEffect != null)
                {
                    sb.Append(pcEffect.title).Append(": ").Append(pcEffect.description);
                }
                else
                {
                    sb.Append("Local Effect");
                }
            }

            return PrefixWithCardType(typePrefix, sb.ToString());
        }

        if (!string.IsNullOrWhiteSpace(data.description))
        {
            return PrefixWithCardType(typePrefix, data.description);
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
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = true;
        SetHoverSorting(true);
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
        SetHoverSorting(false);
        if (highlightImage != null) highlightImage.enabled = false;
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
