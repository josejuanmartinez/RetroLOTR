using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Car : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image image;
    [SerializeField] Image borderImage;
    [SerializeField] Image alignmentImage;
    [SerializeField] Image cardTypeImage;
    [SerializeField] TextMeshProUGUI description;
    [SerializeField] TextMeshProUGUI title;
    [SerializeField] TextMeshProUGUI requirements;
    [SerializeField] Button button;
    private Illustrations illustrations;
    private Colors colors;

    private CardData cardData;
    private bool isConsuming;
    private RectTransform rectTransform;
    private Vector3 defaultScale = Vector3.one;
    private Vector2 defaultPivot = new Vector2(0.5f, 0.5f);
    private int defaultSiblingIndex = -1;
    private bool isHovered;

    private const float HoverScaleMultiplier = 1.5f;
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

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(OnCardClicked);
            button.onClick.AddListener(OnCardClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnCardClicked);
    }

    private void OnDisable()
    {
        RestoreHoverVisuals();
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
        RefreshButtonInteractable();
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
            return $"<color=#{typeColorHex}>Skill.</color>{actionDescription}";
        }

        string jsonDescription = data.description ?? string.Empty;
        return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{jsonDescription}";
    }

    private string TryGetActionDescription(CardData data)
    {
        if (data == null) return null;
        string actionRef = data.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef) && data.actionId <= 0) return null;

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
        if (isConsuming || cardData == null) return;
        if (!TryResolvePlayableAction(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out CharacterAction action))
        {
            return;
        }

        bool canUse = cardData.EvaluatePlayability(selectedCharacter, null, _ => action.FulfillsConditions());
        if (!canUse)
        {
            RefreshButtonInteractable();
            return;
        }

        string cardLabel = string.IsNullOrWhiteSpace(cardData.name) ? action.actionName : cardData.name;
        bool confirm = await ConfirmationDialog.Ask($"Use {cardLabel} ?", "Yes", "No");
        if (!confirm) return;

        TutorialManager tutorial = TutorialManager.Instance;
        bool tutorialActive = tutorial != null && tutorial.IsActiveFor(playerLeader);
        int stepIndexBefore = tutorialActive ? tutorial.GetActiveRequiredStepIndex(playerLeader) : -1;
        bool drawReplacementCard = !tutorialActive;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (deckManager == null) return;

        if (!deckManager.TryConsumeActionCard(playerLeader, cardData.GetActionRef(), cardData.actionId, drawReplacementCard, out _, cardData.cardId))
        {
            RefreshButtonInteractable();
            return;
        }

        isConsuming = true;
        if (button != null) button.interactable = false;

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
        RefreshButtonInteractable();
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

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager == null) return false;
        if (actionsManager.characterActions == null || actionsManager.characterActions.Length == 0) return false;

        string actionRef = cardData.GetActionRef();
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        action = actionsManager.characterActions.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.GetType().Name, actionRef, System.StringComparison.OrdinalIgnoreCase));
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
            : $"<mark=#ffff00>{sprites}.</mark>";
    }

    private static string BuildRequirementSprites(CardData data)
    {
        List<string> parts = new();
        AddSprites(parts, "commander", data.commanderSkillRequired);
        AddSprites(parts, "agent", data.agentSkillRequired);
        AddSprites(parts, "emmissary", data.emissarySkillRequired);
        AddSprites(parts, "mage", data.mageSkillRequired);
        AddSprites(parts, "leather", data.leatherRequired);
        AddSprites(parts, "timber", data.timberRequired);
        AddSprites(parts, "mounts", data.mountsRequired);
        AddSprites(parts, "iron", data.ironRequired);
        AddSprites(parts, "steel", data.steelRequired);
        AddSprites(parts, "mithril", data.mithrilRequired);
        AddSprites(parts, "gold", data.goldRequired);
        if (data.jokerRequired > 0 && data.goldRequired <= 0)
        {
            AddSprites(parts, "gold", 1);
        }
        AddSprites(parts, "joker", data.jokerRequired);
        return string.Concat(parts);
    }

    private static void AddSprites(List<string> parts, string spriteName, int amount)
    {
        if (parts == null || string.IsNullOrWhiteSpace(spriteName) || amount <= 0) return;
        for (int i = 0; i < amount; i++)
        {
            parts.Add($"<sprite name=\"{spriteName}\">");
        }
    }

    private void RefreshButtonInteractable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            return;
        }

        button.interactable = !isConsuming
            && cardData != null
            && TryResolvePlayableAction(out _, out _, out Character selectedCharacter, out CharacterAction action)
            && action != null
            && cardData.EvaluatePlayability(selectedCharacter, null, _ => action.FulfillsConditions());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHoverVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestoreHoverVisuals();
    }

    private void ApplyHoverVisuals()
    {
        if (isHovered || rectTransform == null) return;

        defaultScale = rectTransform.localScale;
        defaultPivot = rectTransform.pivot;
        defaultSiblingIndex = rectTransform.GetSiblingIndex();

        rectTransform.pivot = HoverPivot;
        rectTransform.localScale = defaultScale * HoverScaleMultiplier;
        rectTransform.SetAsLastSibling();
        isHovered = true;
    }

    private void RestoreHoverVisuals()
    {
        if (!isHovered || rectTransform == null) return;

        rectTransform.localScale = defaultScale;
        rectTransform.pivot = defaultPivot;

        if (rectTransform.parent != null)
        {
            int childCount = rectTransform.parent.childCount;
            int clampedIndex = Mathf.Clamp(defaultSiblingIndex, 0, Mathf.Max(0, childCount - 1));
            rectTransform.SetSiblingIndex(clampedIndex);
        }

        isHovered = false;
    }
}
