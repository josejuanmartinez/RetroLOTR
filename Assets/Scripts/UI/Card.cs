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
    [SerializeField] Button discardButton;
    [SerializeField] float holdToDragSeconds = 0.08f;
    [SerializeField] float dragScaleMultiplier = 1.15f;
    [SerializeField] float dragTiltDegrees = 7f;
    [SerializeField] float dragJitterPixels = 4f;
    [SerializeField] float dropPreviewRangePixels = 240f;
    [SerializeField] float dropSnapRangePixels = 120f;
    [SerializeField] float dropSnapLerpSpeed = 16f;
    [SerializeField] float dropSnapScaleMultiplier = 1.02f;
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
    private bool dropPreviewLocked;

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
        if (discardButton != null)
        {
            discardButton.onClick.RemoveListener(OnDiscardClicked);
            discardButton.onClick.AddListener(OnDiscardClicked);
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
        if (discardButton != null) discardButton.onClick.RemoveListener(OnDiscardClicked);
    }

    private void OnDisable()
    {
        pointerIsDown = false;
        isDragging = false;
        dropPreviewLocked = false;
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

        if (data.GetCardType() == CardTypeEnum.Character)
        {
            return BuildCharacterCardDescriptionText(data, cardTypeKey, typeColorHex);
        }
        if (data.GetCardType() == CardTypeEnum.Army)
        {
            return BuildArmyCardDescriptionText(data, cardTypeKey, typeColorHex);
        }

        string actionDescription = TryGetActionDescription(data);
        string jsonDescription = data.description ?? string.Empty;
        if (data.GetCardType() == CardTypeEnum.PC
            && !string.IsNullOrWhiteSpace(actionDescription)
            && jsonDescription.Contains("<TBD>", StringComparison.Ordinal))
        {
            string resolved = jsonDescription.Replace("<TBD>", actionDescription);
            return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{resolved}";
        }

        if (!string.IsNullOrWhiteSpace(actionDescription))
        {
            return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{actionDescription}";
        }

        return $"<color=#{typeColorHex}>{cardTypeKey}.</color>{jsonDescription}";
    }

    private string TryGetActionDescription(CardData data)
    {
        if (data == null) return null;
        string actionRef = NormalizeActionRef(data.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef) && data.actionId <= 0) return null;

        // Prefer runtime descriptions when available so generated card text stays in sync with action logic.
        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        CharacterAction runtimeAction = ResolveActionByRef(actionRef, actionsManager);
        if (runtimeAction != null)
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

    private string BuildCharacterCardDescriptionText(CardData data, string cardTypeKey, string typeColorHex)
    {
        List<string> lines = new()
        {
            $"<color=#{typeColorHex}>{cardTypeKey}.</color>Race: {FormatEnumLabel(data.race.ToString())}"
        };

        List<string> skills = new();
        AddCharacterSkill(skills, "commander", data.commander);
        AddCharacterSkill(skills, "agent", data.agent);
        AddCharacterSkill(skills, "emmissary", data.emmissary);
        AddCharacterSkill(skills, "mage", data.mage);
        if (skills.Count > 0)
        {
            lines.Add(string.Join("  ", skills));
        }

        string artifactText = data.artifacts == null || data.artifacts.Count == 0
            ? "[]"
            : $"[{string.Join(", ", data.artifacts.Where(a => a != null && !string.IsNullOrWhiteSpace(a.artifactName)).Select(a => a.artifactName))}]";
        lines.Add($"<sprite name=\"artifact\"> {artifactText}");

        if (!string.IsNullOrWhiteSpace(data.description))
        {
            lines.Add(data.description.Trim());
        }

        return string.Join("\n", lines);
    }

    private string BuildArmyCardDescriptionText(CardData data, string cardTypeKey, string typeColorHex)
    {
        List<string> lines = new()
        {
            $"<color=#{typeColorHex}>{cardTypeKey}.</color>Race: {FormatEnumLabel(data.race.ToString())}"
        };

        lines.Add($"Troop: <sprite name=\"{data.troopType.ToString().ToLowerInvariant()}\"> {data.troopType.ToString().ToUpperInvariant()}");
        if (data.specialAbilities != null && data.specialAbilities.Count > 0)
        {
            lines.Add($"Abilities: {string.Join("  ", data.specialAbilities.Select(FormatArmyAbilityRichLabel))}");
        }

        if (!string.IsNullOrWhiteSpace(data.description))
        {
            lines.Add(data.description.Trim());
        }

        return string.Join("\n", lines);
    }

    private static string FormatEnumLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        return new string(chars.ToArray());
    }

    private static string FormatArmyAbilityLabel(ArmySpecialAbilityEnum ability)
    {
        return ability switch
        {
            ArmySpecialAbilityEnum.Longrange => "longrange",
            ArmySpecialAbilityEnum.ShortRange => "shortrange",
            _ => ability.ToString().ToLowerInvariant()
        };
    }

    private static void AddCharacterSkill(List<string> parts, string spriteName, int value)
    {
        if (parts == null || value <= 0) return;
        parts.Add($"<sprite name=\"{spriteName}\">{value}");
    }

    private static string FormatArmyAbilityRichLabel(ArmySpecialAbilityEnum ability)
    {
        string label = FormatArmyAbilityLabel(ability);
        string spriteName = ability switch
        {
            ArmySpecialAbilityEnum.Longrange => "ar",
            ArmySpecialAbilityEnum.ShortRange => "sword",
            ArmySpecialAbilityEnum.Poison => "poison",
            ArmySpecialAbilityEnum.Fire => "fire_sword",
            ArmySpecialAbilityEnum.Cursed => "darkness",
            ArmySpecialAbilityEnum.Raid => "boots",
            ArmySpecialAbilityEnum.Pikemen => "sword",
            ArmySpecialAbilityEnum.Shielded => "shield",
            ArmySpecialAbilityEnum.Encouraging => "banner",
            ArmySpecialAbilityEnum.Discouraging => "veil",
            ArmySpecialAbilityEnum.Berserker => "axe",
            _ => null
        };

        return string.IsNullOrWhiteSpace(spriteName)
            ? label
            : $"<sprite name=\"{spriteName}\"> {label}";
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

    private async void OnDiscardClicked()
    {
        await TryDiscardCardAsync();
    }

    public void Discard()
    {
        _ = TryDiscardCardAsync();
    }

    private async Task<bool> TryPlayCardAsync(bool promptForConfirmation)
    {
        if (isDragging || isConsuming || cardData == null) return false;

        if (cardData.GetCardType() == CardTypeEnum.Character)
        {
            return await TryPlayCharacterCardAsync(promptForConfirmation);
        }
        if (cardData.GetCardType() == CardTypeEnum.Army)
        {
            return await TryPlayArmyCardAsync(promptForConfirmation);
        }
        if (cardData.GetCardType() == CardTypeEnum.Encounter)
        {
            return await TryPlayEncounterCardAsync(promptForConfirmation);
        }

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
        if (action.LastExecutionSucceeded && selectedCharacter != null)
        {
            selectedCharacter.lastPlayedCardSpriteNameThisTurn =
                !string.IsNullOrWhiteSpace(cardData.spriteName) ? cardData.spriteName : cardData.name;
            deckManager.ApplyMapRevealForPlayedCard(playerLeader, cardData);
            playerLeader.RecordPlayedCard(cardData);
        }

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

    private async Task<bool> TryPlayEncounterCardAsync(bool promptForConfirmation)
    {
        if (!TryResolveEncounterCardContext(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out _))
        {
            RefreshInteractionState();
            return false;
        }

        if (promptForConfirmation)
        {
            bool confirm = await ConfirmationDialog.Ask($"Face {cardData.name}?", "Yes", "No");
            if (!confirm) return false;
        }

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (deckManager == null)
        {
            RefreshInteractionState();
            return false;
        }

        bool drawReplacementCard = !(TutorialManager.Instance != null && TutorialManager.Instance.IsActiveFor(playerLeader));
        if (!deckManager.TryConsumeCard(playerLeader, cardData.cardId, drawReplacementCard, out _))
        {
            RefreshInteractionState();
            return false;
        }

        isConsuming = true;
        RefreshInteractionState(force: true);

        bool resolved = await EncounterResolver.ResolveAsync(cardData, selectedCharacter);
        if (resolved)
        {
            selectedCharacter.lastPlayedCardSpriteNameThisTurn =
                !string.IsNullOrWhiteSpace(cardData.spriteName) ? cardData.spriteName : cardData.name;
            deckManager.ApplyMapRevealForPlayedCard(playerLeader, cardData);
            playerLeader.RecordPlayedCard(cardData);
        }

        isConsuming = false;
        RefreshInteractionState(force: true);
        return resolved;
    }

    private async Task<bool> TryPlayCharacterCardAsync(bool promptForConfirmation)
    {
        if (!TryResolveCharacterCardContext(out Game game, out PlayableLeader playerLeader, out Hex capitalHex, out string failureReason))
        {
            RefreshInteractionState();
            return false;
        }

        int totalGoldCost = cardData.GetTotalGoldCost();
        if (promptForConfirmation)
        {
            string prompt = totalGoldCost > 0
                ? $"Recruit {cardData.name} for {totalGoldCost} gold?"
                : $"Recruit {cardData.name}?";
            bool confirm = await ConfirmationDialog.Ask(prompt, "Yes", "No");
            if (!confirm) return false;
        }

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        CharacterInstantiator instantiator = FindFirstObjectByType<CharacterInstantiator>();
        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        if (deckManager == null || instantiator == null || capitalHex == null || board == null)
        {
            RefreshInteractionState();
            return false;
        }

        bool drawReplacementCard = !(TutorialManager.Instance != null && TutorialManager.Instance.IsActiveFor(playerLeader));
        if (!deckManager.TryConsumeCard(playerLeader, cardData.cardId, drawReplacementCard, out _))
        {
            RefreshInteractionState();
            return false;
        }

        isConsuming = true;
        RefreshInteractionState(force: true);

        BiomeConfig biomeConfig = new()
        {
            characterName = cardData.name,
            alignment = (AlignmentEnum)cardData.alignment,
            commander = cardData.commander,
            agent = cardData.agent,
            emmissary = cardData.emmissary,
            mage = cardData.mage,
            race = cardData.race,
            artifacts = cardData.artifacts != null ? new List<Artifact>(cardData.artifacts) : new List<Artifact>()
        };

        Character spawned = instantiator.InstantiateCharacter(playerLeader, capitalHex, biomeConfig);
        if (spawned != null)
        {
            spawned.startingCharacter = false;
            spawned.lastPlayedCardSpriteNameThisTurn =
                !string.IsNullOrWhiteSpace(cardData.spriteName) ? cardData.spriteName : cardData.name;
            board.SelectCharacter(spawned, true, 1.0f, 0.0f);
            deckManager.ApplyMapRevealForPlayedCard(playerLeader, cardData);
            playerLeader.RecordPlayedCard(cardData);
            MessageDisplayNoUI.ShowMessage(capitalHex, spawned, $"{spawned.characterName} joins {playerLeader.characterName} at {capitalHex.GetPC().pcName}.", Color.green);
        }

        isConsuming = false;
        RefreshInteractionState(force: true);
        return spawned != null;
    }

    private async Task<bool> TryPlayArmyCardAsync(bool promptForConfirmation)
    {
        if (!TryResolveArmyCardContext(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out string failureReason))
        {
            RefreshInteractionState();
            return false;
        }

        int totalGoldCost = cardData.GetTotalGoldCost();
        if (promptForConfirmation)
        {
            string prompt = totalGoldCost > 0
                ? $"Recruit {cardData.name} for {totalGoldCost} gold?"
                : $"Recruit {cardData.name}?";
            bool confirm = await ConfirmationDialog.Ask(prompt, "Yes", "No");
            if (!confirm) return false;
        }

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        if (deckManager == null || board == null)
        {
            RefreshInteractionState();
            return false;
        }

        bool drawReplacementCard = !(TutorialManager.Instance != null && TutorialManager.Instance.IsActiveFor(playerLeader));
        if (!deckManager.TryConsumeCard(playerLeader, cardData.cardId, drawReplacementCard, out _))
        {
            RefreshInteractionState();
            return false;
        }

        isConsuming = true;
        RefreshInteractionState(force: true);

        List<ArmySpecialAbilityEnum> specialAbilities = cardData.specialAbilities != null
            ? new List<ArmySpecialAbilityEnum>(cardData.specialAbilities)
            : new List<ArmySpecialAbilityEnum>();

        if (!selectedCharacter.IsArmyCommander())
        {
            selectedCharacter.CreateArmy(cardData.troopType, 1, false, 0, specialAbilities);
        }
        else
        {
            selectedCharacter.GetArmy()?.Recruit(cardData.troopType, 1, specialAbilities);
            selectedCharacter.hex?.RedrawCharacters();
            selectedCharacter.hex?.RedrawArmies();
            selectedCharacter.RefreshSelectedCharacterIconIfSelected();
        }

        selectedCharacter.lastPlayedCardSpriteNameThisTurn =
            !string.IsNullOrWhiteSpace(cardData.spriteName) ? cardData.spriteName : cardData.name;
        deckManager.ApplyMapRevealForPlayedCard(playerLeader, cardData);
        playerLeader.RecordPlayedCard(cardData);
        board.SelectCharacter(selectedCharacter, true, 1.0f, 0.0f);
        MessageDisplayNoUI.ShowMessage(
            selectedCharacter.hex,
            selectedCharacter,
            $"{selectedCharacter.characterName} recruits 1 <sprite name=\"{cardData.troopType.ToString().ToLowerInvariant()}\"/> from {cardData.name}.",
            Color.green);

        isConsuming = false;
        RefreshInteractionState(force: true);
        return true;
    }

    private bool TryResolveCharacterCardContext(out Game game, out PlayableLeader playerLeader, out Hex capitalHex, out string reason)
    {
        game = FindFirstObjectByType<Game>();
        playerLeader = game != null ? game.player : null;
        capitalHex = null;
        reason = null;

        if (game == null || playerLeader == null)
        {
            reason = "Game is not initialized.";
            return false;
        }

        if (!game.IsPlayerCurrentlyPlaying())
        {
            reason = "It is not your turn.";
            return false;
        }

        capitalHex = ResolveCapitalHex(playerLeader);
        if (capitalHex == null)
        {
            reason = "Your capital was not found.";
            return false;
        }

        if (!cardData.MeetsResourceRequirements(playerLeader))
        {
            reason = BuildMissingResourcesReason(playerLeader);
            return false;
        }

        bool duplicateExists = playerLeader.controlledCharacters.Any(ch =>
            ch != null
            && !ch.killed
            && string.Equals(ch.characterName, cardData.name, StringComparison.OrdinalIgnoreCase));
        if (duplicateExists)
        {
            reason = $"{cardData.name} already serves you.";
            return false;
        }

        return true;
    }

    private bool TryResolveArmyCardContext(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out string reason)
    {
        game = FindFirstObjectByType<Game>();
        playerLeader = game != null ? game.player : null;
        selectedCharacter = null;
        reason = null;

        if (game == null || playerLeader == null)
        {
            reason = "Game is not initialized.";
            return false;
        }

        if (!game.IsPlayerCurrentlyPlaying())
        {
            reason = "It is not your turn.";
            return false;
        }

        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        selectedCharacter = board != null ? board.selectedCharacter : null;
        if (selectedCharacter == null)
        {
            reason = "Select a commander first.";
            return false;
        }

        if (selectedCharacter.GetOwner() != playerLeader || selectedCharacter.killed)
        {
            reason = "Selected character is not controlled by you.";
            return false;
        }

        if (selectedCharacter.GetCommander() <= 0)
        {
            reason = $"{selectedCharacter.characterName} is not a commander.";
            return false;
        }

        if (cardData.troopType == TroopsTypeEnum.ws && (selectedCharacter.hex?.GetPC() == null || !selectedCharacter.hex.GetPC().hasPort))
        {
            reason = "Warships require a port.";
            return false;
        }

        if (!cardData.MeetsResourceRequirements(playerLeader))
        {
            reason = BuildMissingResourcesReason(playerLeader);
            return false;
        }

        return true;
    }

    private static Hex ResolveCapitalHex(Leader leader)
    {
        if (leader == null) return null;

        PC capital = leader.controlledPcs?.FirstOrDefault(pc => pc != null && pc.isCapital && pc.hex != null);
        if (capital != null) return capital.hex;

        Board board = FindFirstObjectByType<Board>();
        return board?.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == leader && x.GetPC().isCapital);
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

        string richRequirements = BuildRequirementsText(data);
        requirements.text = string.IsNullOrWhiteSpace(richRequirements)
            ? string.Empty
            : $"<mark=#ffff00>{richRequirements}</mark>";
    }

    private string BuildRequirementsText(CardData data)
    {
        if (TryBuildConditionalPcFoundingRequirements(data, out string conditionalRequirements))
        {
            return conditionalRequirements;
        }

        if (!string.IsNullOrWhiteSpace(data.requirementsText))
        {
            return data.requirementsText.Trim();
        }

        return BuildRequirementSprites(data);
    }

    private string BuildRequirementSprites(CardData data)
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
        AddRequirement(parts, "gold", data.GetTotalGoldCost());
        if (data.jokerRequired > 0 && data.GetTotalGoldCost() <= 0)
        {
            AddRequirement(parts, "gold", 1);
        }
        AddRequirement(parts, "joker", data.jokerRequired);
        return string.Join(" ", parts);
    }

    private bool TryBuildConditionalPcFoundingRequirements(CardData data, out string requirementsTextValue)
    {
        requirementsTextValue = null;
        if (data == null || data.GetCardType() != CardTypeEnum.PC) return false;
        if (!IsConditionalPcFoundingCard(data)) return false;

        string baseRequirement = "2<sprite name=\"commander\"> OR 1<sprite name=\"emmissary\">";
        requirementsTextValue = IsPcAbsentFromBoard(data.name)
            ? $"{baseRequirement} 10<sprite name=\"gold\">"
            : baseRequirement;
        return true;
    }

    private bool IsConditionalPcFoundingCard(CardData data)
    {
        if (data == null) return false;

        string actionRef = NormalizeActionRef(data.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        Type actionType = ResolveActionType(actionRef);
        return actionType != null && typeof(MaterialRetrievalOrAction).IsAssignableFrom(actionType);
    }

    private bool IsPcAbsentFromBoard(string pcName)
    {
        if (string.IsNullOrWhiteSpace(pcName)) return true;

        Board board = FindFirstObjectByType<Board>();
        List<Hex> hexes = board?.GetHexes();
        if (hexes == null || hexes.Count == 0) return true;

        for (int i = 0; i < hexes.Count; i++)
        {
            PC pc = hexes[i]?.GetPCData();
            if (pc == null || string.IsNullOrWhiteSpace(pc.pcName)) continue;
            if (string.Equals(pc.pcName.Trim(), pcName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
        if (discardButton != null)
        {
            discardButton.interactable = !isDragging && !isConsuming && cardData != null && !cardData.IsEncounterCard();
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
        if (cardData != null && cardData.GetCardType() == CardTypeEnum.Character)
        {
            return TryResolveCharacterCardContext(out _, out _, out _, out _);
        }
        if (cardData != null && cardData.GetCardType() == CardTypeEnum.Army)
        {
            return TryResolveArmyCardContext(out _, out _, out _, out _);
        }
        if (cardData != null && cardData.GetCardType() == CardTypeEnum.Encounter)
        {
            return TryResolveEncounterCardContext(out _, out _, out Character encounterCharacter, out _)
                && cardData.EvaluatePlayability(encounterCharacter);
        }

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
            canvasGroup.alpha = 1f;
        }

        dropPreviewLocked = false;
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

        dropPreviewLocked = UpdateDropPreview(eventData, eventCamera);
        if (dropPreviewLocked && TryGetSelectedCharacterTargetCenter(out Vector3 targetCenter))
        {
            float lerp = 1f - Mathf.Exp(-dropSnapLerpSpeed * Time.unscaledDeltaTime);
            rectTransform.position = Vector3.Lerp(rectTransform.position, targetCenter, lerp);
            rectTransform.localScale = originalScaleBeforeDrag * dragScaleMultiplier * dropSnapScaleMultiplier;
            rectTransform.rotation = Quaternion.Slerp(rectTransform.rotation, originalRotationBeforeDrag, lerp * 0.9f);
        }
        else
        {
            rectTransform.position = pointerWorld + dragPointerOffsetWorld + jitter;
            rectTransform.localScale = originalScaleBeforeDrag * dragScaleMultiplier;
            rectTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 20f) * dragTiltDegrees);
        }
    }

    public async void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        bool droppedOnSelectedCharacter = dropPreviewLocked || IsDroppedOnSelectedCharacter(eventData);
        isDragging = false;
        dropPreviewLocked = false;
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

        if (cardData.GetCardType() == CardTypeEnum.Character)
        {
            if (TryResolveCharacterCardContext(out _, out _, out _, out string characterReason)) return string.Empty;
            return string.IsNullOrWhiteSpace(characterReason) ? "Requirements are not met." : characterReason;
        }
        if (cardData.GetCardType() == CardTypeEnum.Army)
        {
            if (TryResolveArmyCardContext(out _, out _, out _, out string armyReason)) return string.Empty;
            return string.IsNullOrWhiteSpace(armyReason) ? "Requirements are not met." : armyReason;
        }
        if (cardData.GetCardType() == CardTypeEnum.Encounter)
        {
            if (!TryResolveEncounterCardContext(out _, out _, out Character encounterCharacter, out string encounterReason))
            {
                return string.IsNullOrWhiteSpace(encounterReason) ? "Requirements are not met." : encounterReason;
            }

            bool encounterPlayable = cardData.EvaluatePlayability(encounterCharacter);
            if (encounterPlayable) return string.Empty;

            List<string> encounterReasons = new();
            CardPlayabilityResult encounterResult = cardData.playability;
            if (encounterResult != null)
            {
                if (encounterResult.failsLevelRequirements)
                {
                    string levelReason = BuildMissingLevelsReason(encounterCharacter);
                    if (!string.IsNullOrWhiteSpace(levelReason)) encounterReasons.Add(levelReason);
                }

                if (encounterResult.failsResourceRequirements)
                {
                    string resourceReason = BuildMissingResourcesReason(encounterCharacter.GetOwner());
                    if (!string.IsNullOrWhiteSpace(resourceReason)) encounterReasons.Add(resourceReason);
                }
            }

            if (encounterReasons.Count == 0) encounterReasons.Add("Requirements are not met.");
            return string.Join("<br>", encounterReasons);
        }

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

            if (result.failsCardHistoryRequirements && !string.IsNullOrWhiteSpace(result.cardHistoryReason))
            {
                reasons.Add(result.cardHistoryReason);
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
        AddMissingRequirement(parts, "Gold", cardData.GetTotalGoldCost(), owner.goldAmount);
        if (parts.Count == 0) return "Not enough resources.";
        return $"Need resources: {string.Join(", ", parts)}";
    }

    private static void AddMissingRequirement(List<string> parts, string label, int required, int current)
    {
        if (parts == null || required <= 0 || current >= required) return;
        parts.Add($"{label} {required} (have {current})");
    }

    private bool TryResolveEncounterCardContext(out Game game, out PlayableLeader playerLeader, out Character selectedCharacter, out string reason)
    {
        game = FindFirstObjectByType<Game>();
        playerLeader = game != null ? game.player : null;
        selectedCharacter = null;
        reason = null;

        if (game == null || playerLeader == null)
        {
            reason = "Game is not initialized.";
            return false;
        }
        if (!game.IsPlayerCurrentlyPlaying())
        {
            reason = "It is not your turn.";
            return false;
        }

        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        selectedCharacter = board != null ? board.selectedCharacter : null;
        if (selectedCharacter == null)
        {
            reason = "Select one of your characters first.";
            return false;
        }
        if (selectedCharacter.GetOwner() != playerLeader)
        {
            reason = "Selected character is not controlled by you.";
            return false;
        }
        if (selectedCharacter.killed)
        {
            reason = "Selected character is dead.";
            return false;
        }
        if ((cardData.encounterOptions == null || cardData.encounterOptions.Count == 0) && cardData.fleeOption == null)
        {
            reason = "This encounter has no configured choices.";
            return false;
        }

        return true;
    }

    private async Task TryDiscardCardAsync()
    {
        if (isDragging || isConsuming || cardData == null || cardData.IsEncounterCard()) return;

        Game game = FindFirstObjectByType<Game>();
        PlayableLeader playerLeader = game != null ? game.player : null;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (game == null || playerLeader == null || deckManager == null) return;
        if (!game.IsPlayerCurrentlyPlaying()) return;

        bool confirm = await ConfirmationDialog.Ask($"Discard {cardData.name} and draw another card?", "Yes", "No");
        if (!confirm) return;

        if (!deckManager.TryDiscardCard(playerLeader, cardData.cardId, out _))
        {
            RefreshInteractionState(force: true);
            return;
        }

        deckManager.TryDrawCard(playerLeader, out _);
        deckManager.RefreshHumanPlayerHandUI();
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

    private bool UpdateDropPreview(PointerEventData eventData, Camera eventCamera)
    {
        if (eventData == null) return false;
        SelectedCharacterIcon icon = GetSelectedCharacterIcon();
        if (icon == null || !icon.gameObject.activeInHierarchy) return false;

        RectTransform targetRect = icon.transform as RectTransform;
        if (targetRect == null) return false;

        Vector2 targetCenterScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, targetRect.TransformPoint(targetRect.rect.center));
        float distanceToCenter = Vector2.Distance(eventData.position, targetCenterScreen);
        bool isInside = RectTransformUtility.RectangleContainsScreenPoint(targetRect, eventData.position, eventCamera);
        bool isNear = distanceToCenter <= dropPreviewRangePixels;
        bool lockDrop = isInside || distanceToCenter <= dropSnapRangePixels;

        float proximity = 0f;
        if (isNear)
        {
            proximity = 1f - Mathf.Clamp01(distanceToCenter / Mathf.Max(1f, dropPreviewRangePixels));
        }
        icon.SetDropTargetProximity(proximity, lockDrop);
        return lockDrop;
    }

    private bool TryGetSelectedCharacterTargetCenter(out Vector3 centerWorld)
    {
        centerWorld = Vector3.zero;
        SelectedCharacterIcon selectedIcon = GetSelectedCharacterIcon();
        if (selectedIcon == null || !selectedIcon.gameObject.activeInHierarchy) return false;

        RectTransform targetRect = selectedIcon.transform as RectTransform;
        if (targetRect == null) return false;

        centerWorld = targetRect.TransformPoint(targetRect.rect.center);
        return true;
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
        if (!enabled)
        {
            icon.SetDropTargetProximity(0f, false);
        }
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
