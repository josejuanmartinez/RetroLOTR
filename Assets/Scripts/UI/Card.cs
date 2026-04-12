using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI requirementsText;
    [SerializeField] private Image cardArtImage;
    [SerializeField] private Image cardBackgroundImage;
    [SerializeField] private Image highlightImage;
    [SerializeField] private GameObject playIndicator;
    [SerializeField] private GameObject shadowObject;

    [Header("Prefabs")]
    [SerializeField] private GameObject dragProxyPrefab;

    [Header("Tuning")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverSpeed = 10f;
    [SerializeField] private float dragAlpha = 0.6f;
    [SerializeField] private float playDropThresholdY = 200f;

    public CardData cardData { get; private set; }

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;
    private Vector3 targetScale = Vector3.one;
    private bool isHovered;
    private bool isDragging;
    private GameObject dragProxy;
    private int originalSiblingIndex;
    private Transform originalParent;

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
        canvasGroup.alpha = 1f;

        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        targetScale = originalScale;

        if (highlightImage != null) highlightImage.enabled = false;
        if (playIndicator != null) playIndicator.SetActive(false);

        activeCards.Add(this);
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

        if (cardArtImage != null && !string.IsNullOrWhiteSpace(data.spriteName))
        {
            Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(data.spriteName) : null;
            cardArtImage.sprite = sprite;
            cardArtImage.enabled = sprite != null;
        }

        UpdateInteractableState();
    }

    private string GetActionDescription(CardData data)
    {
        if (data == null) return string.Empty;

        CardTypeEnum cardType = data.GetCardType();
        if (cardType == CardTypeEnum.Land || cardType == CardTypeEnum.PC)
        {
            StringBuilder sb = new();
            List<string> grants = new();
            if (data.leatherGranted > 0) grants.Add(RepeatSprite("leather", data.leatherGranted));
            if (data.timberGranted > 0) grants.Add(RepeatSprite("timber", data.timberGranted));
            if (data.mountsGranted > 0) grants.Add(RepeatSprite("mounts", data.mountsGranted));
            if (data.ironGranted > 0) grants.Add(RepeatSprite("iron", data.ironGranted));
            if (data.steelGranted > 0) grants.Add(RepeatSprite("steel", data.steelGranted));
            if (data.mithrilGranted > 0) grants.Add(RepeatSprite("mithril", data.mithrilGranted));
            if (data.goldGranted > 0) grants.Add(RepeatSprite("gold", data.goldGranted));

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

    private string RepeatSprite(string spriteName, int count)
    {
        StringBuilder sb = new();
        for (int i = 0; i < count; i++)
        {
            sb.Append($"<sprite name=\"{spriteName}\">");
        }
        return sb.ToString();
    }

    private string BuildRequirementsText(CardData data)
    {
        if (data == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(data.requirementsText)) return data.requirementsText;

        List<string> reqs = new();

        if (data.commanderSkillRequired > 0) reqs.Add($"<sprite name=\"commander\"> {data.commanderSkillRequired}");
        if (data.agentSkillRequired > 0) reqs.Add($"<sprite name=\"agent\"> {data.agentSkillRequired}");
        if (data.emissarySkillRequired > 0) reqs.Add($"<sprite name=\"emmissary\"> {data.emissarySkillRequired}");
        if (data.mageSkillRequired > 0) reqs.Add($"<sprite name=\"mage\"> {data.mageSkillRequired}");

        int totalGold = data.GetTotalGoldCost();
        if (totalGold > 0) reqs.Add($"<sprite name=\"gold\"> {totalGold}");

        if (data.leatherRequired > 0) reqs.Add($"<sprite name=\"leather\"> {data.leatherRequired}");
        if (data.timberRequired > 0) reqs.Add($"<sprite name=\"timber\"> {data.timberRequired}");
        if (data.mountsRequired > 0) reqs.Add($"<sprite name=\"mounts\"> {data.mountsRequired}");
        if (data.ironRequired > 0) reqs.Add($"<sprite name=\"iron\"> {data.ironRequired}");
        if (data.steelRequired > 0) reqs.Add($"<sprite name=\"steel\"> {data.steelRequired}");
        if (data.mithrilRequired > 0) reqs.Add($"<sprite name=\"mithril\"> {data.mithrilRequired}");

        return string.Join("  ", reqs);
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
        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        if (highlightImage != null && cardData != null && cardData.isPlayable) highlightImage.enabled = true;
        Sounds.Instance?.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        isHovered = false;
        targetScale = originalScale;
        transform.SetSiblingIndex(originalSiblingIndex);
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

        isDragging = true;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;

        if (shadowObject != null) shadowObject.SetActive(true);
        if (playIndicator != null) playIndicator.SetActive(true);

        transform.SetParent(transform.root, true);
        Sounds.Instance?.PlayUiHover();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        transform.position = eventData.position;

        // Visual feedback if dragged high enough to "play"
        bool overPlayArea = eventData.position.y > (Screen.height * 0.3f);
        if (playIndicator != null) playIndicator.SetActive(overPlayArea);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (shadowObject != null) shadowObject.SetActive(false);
        if (playIndicator != null) playIndicator.SetActive(false);

        bool overPlayArea = eventData.position.y > (Screen.height * 0.3f);

        if (overPlayArea)
        {
            TryPlayCard();
        }
        else
        {
            // Return to hand
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            targetScale = originalScale;
        }
    }

    private async void TryPlayCard()
    {
        if (cardData == null) return;

        Board board = FindFirstObjectByType<Board>();
        Character selected = board != null ? board.selectedCharacter : null;

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
            case CardTypeEnum.Encounter:
            case CardTypeEnum.Land:
            case CardTypeEnum.PC:
                success = await HandleActionCardPlayed(selected);
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
            // Card was successfully played, it will be removed from hand by the manager
            Destroy(gameObject);
        }
        else
        {
            // Failed to play (e.g. cancelled target selection), return to hand
            if (transform.parent != originalParent)
            {
                transform.SetParent(originalParent, true);
                transform.SetSiblingIndex(originalSiblingIndex);
            }
            targetScale = originalScale;
            UpdateInteractableState();
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

    private async Task<bool> HandleCharacterCardPlayed(Character selected)
    {
        // Character cards usually represent recruiting a specific character
        // This might involve showing a recruitment UI or spawning them at a capital
        return false;
    }

    private async Task<bool> HandleArmyCardPlayed(Character selected)
    {
        // Army cards represent mustering troops
        return false;
    }
}
