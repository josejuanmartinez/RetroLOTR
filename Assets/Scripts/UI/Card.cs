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
public class Card : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
    [SerializeField] private GameObject discardButton;
    [SerializeField] private Image deckTypeImage;

    [Header("Token / Card Flip")]
    [SerializeField] private Image tokenImage;
    [SerializeField] private Image tokenBorder;
    [SerializeField] private CanvasGroup tokenCanvasGroup;
    [SerializeField] private CanvasGroup realCardCanvasGroup;
    [SerializeField] private TextMeshProUGUI environmentalSprite;

    [Header("Tuning")]
    [SerializeField] private Color requirementsMessageColor = Color.red;

    public CardData cardData { get; private set; }

    private CanvasGroup canvasGroup;
    private LayoutElement layoutElement;
    private RectTransform rectTransform;
    private Graphic rootHitGraphic;
    public bool SuppressHoverEffects { get; set; }
    private bool lockedToRealCard;
    private string baseDescription = string.Empty;
    private Image encounterArtOverlay;
    private TextMeshProUGUI encounterQuestionMark;
    private Image encounterTokenOverlay;
    private TextMeshProUGUI encounterTokenQuestionMark;
    private Coroutine descriptionTypewriterCoroutine;
    private Coroutine encounterHintCoroutine;

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

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = false;

        rectTransform = GetComponent<RectTransform>();
        rootHitGraphic = GetComponent<Graphic>();

        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();
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

    public void SetEnvironmentalPulse(bool active)
    {
        GameObject target = tokenCanvasGroup != null ? tokenCanvasGroup.gameObject : gameObject;
        CardEnvironmentalPulseEffect existing = target.GetComponent<CardEnvironmentalPulseEffect>();
        if (active)
        {
            if (existing == null) target.AddComponent<CardEnvironmentalPulseEffect>();
            else existing.enabled = true;
        }
        else if (existing != null)
        {
            existing.enabled = false;
        }
    }

    public void Initialize(CardData data, bool startAsToken = true)
    {
        cardData = data;
        EnsureManagersLoaded();
        BindLegacyPrefabReferences();
        RestrictRaycastsToRootCard();

        if (titleText != null) titleText.text = FormatCardTitle(data.name);
        if (hover != null) hover.Initialize(FormatCardTypeLabel(data.GetCardType()));
        ApplyCardTypeColor(data.GetCardType());

        // Only the active environmental card (Layout's "Environmental > EnvironmentalCard")
        // shows this icon; hidden by default so it never leaks onto hand cards.
        if (environmentalSprite != null) environmentalSprite.gameObject.SetActive(false);

        if (descriptionText != null)
        {
            baseDescription = GetActionDescription(data);
            descriptionText.text = baseDescription;
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

            if (cardArtImage.GetComponent<CardShineEffect>() == null)
                cardArtImage.gameObject.AddComponent<CardShineEffect>();

            if (tokenImage != null) tokenImage.sprite = sprite;
        }

        lockedToRealCard = !startAsToken;
        if (startAsToken) ShowToken();
        else ShowRealCard();

        if (deckTypeImage != null && !string.IsNullOrWhiteSpace(data.deckSpriteName) && illustrations != null)
        {
            if (illustrations.TryGetIllustrationByName(data.deckSpriteName, out Sprite deckSprite))
            {
                deckTypeImage.sprite = deckSprite;
                deckTypeImage.enabled = true;
            }
        }

        if (data.IsEncounterCard())
        {
            AssignEncounterTargetHexIfNeeded(data);
            if (!data.encounterRevealed)
                SetupEncounterHiddenVisuals(data);
        }

        UpdateInteractableState();

        if (!data.hasShownHandAnimation && descriptionText != null && !string.IsNullOrEmpty(baseDescription))
        {
            string quoteBlock = data.GetQuoteBlock();
            if (!string.IsNullOrWhiteSpace(quoteBlock) && baseDescription.Contains(quoteBlock))
            {
                int quoteStart = baseDescription.LastIndexOf(quoteBlock, StringComparison.Ordinal);
                string immediateText = baseDescription.Substring(0, quoteStart).TrimEnd();
                descriptionText.text = immediateText;
                descriptionTypewriterCoroutine = StartCoroutine(HandDrawTypewriterCoroutine("\n\n" + quoteBlock, data, append: true));
            }
            else
            {
                descriptionText.text = string.Empty;
                descriptionTypewriterCoroutine = StartCoroutine(HandDrawTypewriterCoroutine(baseDescription, data));
            }
        }
    }

    // Called for the active environmental card shown in Layout's "Environmental > EnvironmentalCard".
    // Reveals the environmental sprite transform and renders the card's icon via the normalized name
    // (same scheme as the sprite-asset m_Name fields, e.g. "wind", "sun", "redsun").
    public void ShowEnvironmentalSprite()
    {
        if (environmentalSprite == null) return;
        environmentalSprite.gameObject.SetActive(true);
        environmentalSprite.text = cardData != null
            ? $"<sprite name=\"{CardNameUtility.Normalize(cardData.name)}\">"
            : string.Empty;
    }

    private IEnumerator HandDrawTypewriterCoroutine(string text, CardData data, bool append = false)
    {
        if (append)
            yield return StartCoroutine(AppendTypewriterEffectCoroutine(descriptionText, text));
        else
            yield return StartCoroutine(TypewriterEffectCoroutine(descriptionText, text));
        if (data != null) data.hasShownHandAnimation = true;
        descriptionTypewriterCoroutine = null;
        UpdateInteractableState();
    }

    private IEnumerator AppendTypewriterEffectCoroutine(TextMeshProUGUI textComponent, string appendText)
    {
        if (textComponent == null || string.IsNullOrEmpty(appendText)) yield break;
        string prefix = textComponent.text;
        float delay = Mathf.Min(0.05f, 2f / appendText.Length);
        for (int i = 0; i < appendText.Length; i++)
        {
            if (textComponent == null) yield break;
            textComponent.text = prefix + appendText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }
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

    private static void AssignEncounterTargetHexIfNeeded(CardData data)
    {
        if (data.encounterTargetHex != null) return;

        Game game = FindFirstObjectByType<Game>();
        Leader leader = game?.player;
        if (leader == null) return;

        var candidates = new HashSet<Hex>();
        if (leader.hex != null && !leader.killed)
        {
            foreach (Hex h in leader.hex.GetHexesInRadius(5)) candidates.Add(h);
        }
        if (leader.controlledCharacters != null)
        {
            foreach (Character c in leader.controlledCharacters)
            {
                if (c == null || c.killed || c.hex == null) continue;
                foreach (Hex h in c.hex.GetHexesInRadius(5)) candidates.Add(h);
            }
        }

        if (candidates.Count == 0) return;
        var list = new List<Hex>(candidates);
        data.encounterTargetHex = list[UnityEngine.Random.Range(0, list.Count)];
    }

    private bool IsUnplayedEncounterWithHex() =>
        cardData != null &&
        cardData.IsEncounterCard() &&
        !cardData.encounterRevealed &&
        cardData.encounterTargetHex != null;

    private void FlashEncounterHintFrame(Hex hex)
    {
        if (hex == null || hex.tipHexFrame == null) return;
        if (encounterHintCoroutine != null) StopCoroutine(encounterHintCoroutine);
        encounterHintCoroutine = StartCoroutine(EncounterHintFrameCoroutine(hex));
    }

    private IEnumerator EncounterHintFrameCoroutine(Hex hex)
    {
        hex.tipHexFrame.SetActive(true);
        yield return new WaitForSecondsRealtime(5f);
        if (hex != null && hex.tipHexFrame != null)
            hex.tipHexFrame.SetActive(false);
        encounterHintCoroutine = null;
    }

    private void SetupEncounterHiddenVisuals(CardData data)
    {
        if (titleText != null) titleText.text = "Encounter";

        if (encounterArtOverlay == null && cardArtImage != null)
        {
            var overlayGo = new GameObject("EncounterOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(cardArtImage.transform, false);
            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            encounterArtOverlay = overlayGo.GetComponent<Image>();
            encounterArtOverlay.color = Color.black;

            var qGo = new GameObject("QuestionMark", typeof(RectTransform), typeof(TextMeshProUGUI));
            qGo.transform.SetParent(overlayGo.transform, false);
            var qRect = qGo.GetComponent<RectTransform>();
            qRect.anchorMin = Vector2.zero;
            qRect.anchorMax = Vector2.one;
            qRect.offsetMin = Vector2.zero;
            qRect.offsetMax = Vector2.zero;
            encounterQuestionMark = qGo.GetComponent<TextMeshProUGUI>();
            encounterQuestionMark.text = "?";
            encounterQuestionMark.fontSize = 64f;
            encounterQuestionMark.alignment = TextAlignmentOptions.Center;
            encounterQuestionMark.color = Color.white;
            encounterQuestionMark.fontStyle = FontStyles.Bold;
        }

        if (encounterTokenOverlay == null && tokenImage != null)
        {
            var tokenOverlayGo = new GameObject("EncounterTokenOverlay", typeof(RectTransform), typeof(Image));
            tokenOverlayGo.transform.SetParent(tokenImage.transform, false);
            var tokenOverlayRect = tokenOverlayGo.GetComponent<RectTransform>();
            tokenOverlayRect.anchorMin = Vector2.zero;
            tokenOverlayRect.anchorMax = Vector2.one;
            tokenOverlayRect.offsetMin = Vector2.zero;
            tokenOverlayRect.offsetMax = Vector2.zero;
            encounterTokenOverlay = tokenOverlayGo.GetComponent<Image>();
            encounterTokenOverlay.color = Color.black;
            encounterTokenOverlay.raycastTarget = false;

            var tqGo = new GameObject("QuestionMark", typeof(RectTransform), typeof(TextMeshProUGUI));
            tqGo.transform.SetParent(tokenOverlayGo.transform, false);
            var tqRect = tqGo.GetComponent<RectTransform>();
            tqRect.anchorMin = Vector2.zero;
            tqRect.anchorMax = Vector2.one;
            tqRect.offsetMin = Vector2.zero;
            tqRect.offsetMax = Vector2.zero;
            encounterTokenQuestionMark = tqGo.GetComponent<TextMeshProUGUI>();
            encounterTokenQuestionMark.text = "?";
            encounterTokenQuestionMark.fontSize = 64f;
            encounterTokenQuestionMark.alignment = TextAlignmentOptions.Center;
            encounterTokenQuestionMark.color = Color.white;
            encounterTokenQuestionMark.fontStyle = FontStyles.Bold;
            encounterTokenQuestionMark.raycastTarget = false;
        }

        string hexCoords = data.encounterTargetHex != null
            ? $"{data.encounterTargetHex.v2.x}, {data.encounterTargetHex.v2.y}"
            : "unknown";
        baseDescription = $"An encounter can be investigated at hex {hexCoords}";
        if (descriptionText != null) descriptionText.text = baseDescription;
    }

    private void BindLegacyPrefabReferences()
    {
        if (titleText == null) titleText = FindTextByName("Title");
        if (descriptionText == null) descriptionText = FindTextByName("Description");
        // if (typeText == null) typeText = FindTextByName("Type (1)") ?? FindTextByName("Type");
        if (requirementsText == null) requirementsText = FindTextByName("Requirements");

        if (cardArtImage == null) cardArtImage = FindImageByName("Image");
        if (cardBackgroundImage == null) cardBackgroundImage = FindImageByName("Border");
        if (discardButton == null) discardButton = FindChildByName("Discard");
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

    private Color GetCardTypeColor(CardTypeEnum cardType)
    {
        if (colors == null) colors = FindFirstObjectByType<Colors>();

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
            CardTypeEnum.Environmental => "environmental",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(colorName) || colors == null) return Color.clear;

        Color c;
        try { c = colors.GetColorByName(colorName); }
        catch { c = Color.clear; }

        if (c.a < 0.01f)
            c = colorName switch { "environmental" => new Color(0.42f, 0.67f, 0.42f, 1f), _ => Color.clear };

        return c;
    }

    private void ApplyCardTypeColor(CardTypeEnum cardType)
    {
        Color c = GetCardTypeColor(cardType);
        if (c.a < 0.01f) return;

        if (cardBackgroundImage != null)
            cardBackgroundImage.color = new Color(c.r, c.g, c.b, cardBackgroundImage.color.a);
        if (tokenBorder != null)
            tokenBorder.color = new Color(c.r, c.g, c.b, tokenBorder.color.a);
    }

    private string FormatCardTypeLabel(CardTypeEnum cardType)
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
            CardTypeEnum.Encounter => "Encounter",
            CardTypeEnum.Environmental => "Environmental",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(label)) return string.Empty;

        Color c = GetCardTypeColor(cardType);
        if (c.a < 0.01f) return label;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{label}</color>";
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

        string situationLabel = FormatSituationLabel(data);
        if (!string.IsNullOrWhiteSpace(situationLabel))
        {
            string costPart = reqs.Count > 0 ? $"\n{string.Join(" ", reqs)}" : string.Empty;
            return $"{situationLabel}{costPart}";
        }

        if (reqs.Count == 0) return string.Empty;
        return $"{string.Join(" ", reqs)}";
    }

    private string FormatSituationLabel(CardData data)
    {
        if (data == null || data.GetCardType() != CardTypeEnum.Action) return string.Empty;
        CardSituationEnum situation = data.GetSituation();
        if (situation == CardSituationEnum.None) return string.Empty;

        string label = situation switch
        {
            CardSituationEnum.ArmyAtEnemyPC                    => "Army at enemy PC",
            CardSituationEnum.AgentAtEnemyPC                   => "Agent at enemy PC",
            CardSituationEnum.EmmissaryAtEnemyPC               => "Emissary at enemy PC",
            CardSituationEnum.ArmyAtFriendlyPC                 => "Army at friendly PC",
            CardSituationEnum.EmmissaryAtOwnPC                 => "Emissary at own PC",
            CardSituationEnum.AgentAtOwnPC                     => "Agent at own PC",
            CardSituationEnum.ArmyAtHexWithEnemyArmyAndNoPC   => "Army meets enemy army",
            CardSituationEnum.AgentAtHexWithEnemyCharacter     => "Agent meets enemy",
            CardSituationEnum.EmmissaryAtHexWithEnemyCharacter => "Emissary meets enemy",
            CardSituationEnum.MageAtHexWithEnemyCharacter      => "Mage meets enemy",
            CardSituationEnum.MageAtArtifactHex                => "Mage at artifact",
            CardSituationEnum.CommanderAtOwnPC                 => "Commander at own PC",
            _                                                   => string.Empty
        };

        return string.IsNullOrWhiteSpace(label) ? string.Empty : $"When: {label}";
    }

    private void AppendRequirement(List<string> requirements, string spriteName, int count)
    {
        if (requirements == null || string.IsNullOrWhiteSpace(spriteName) || count <= 0) return;
        requirements.Add($"{count}<sprite name=\"{spriteName}\">");
    }

    public void UpdateInteractableState()
    {
        if (cardData == null) return;

        bool isTypewriting = descriptionTypewriterCoroutine != null;

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

        if (!SuppressHoverEffects && canvasGroup != null)
        {
            canvasGroup.alpha = isPlayable ? 1f : 0.5f;
            canvasGroup.interactable = isPlayable;
            canvasGroup.blocksRaycasts = true;
        }

        if (!isTypewriting && descriptionText != null)
        {
            if (isPlayable)
            {
                descriptionText.text = baseDescription;
            }
            else
            {
                string errorText = BuildRequirementsMessageText(selected, resourceOwner);
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    string colorHex = ColorUtility.ToHtmlStringRGB(requirementsMessageColor);
                    string separator = string.IsNullOrWhiteSpace(baseDescription) ? string.Empty : "\n";
                    descriptionText.text = $"{baseDescription}{separator}<color=#{colorHex}>{errorText}</color>";
                }
                else
                {
                    descriptionText.text = baseDescription;
                }
            }
        }

        UpdateDiscardButtonState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!SuppressHoverEffects) ShowRealCard();
        if (Sounds.Instance != null) Sounds.Instance.PlayUiHover();
        if (SuppressHoverEffects) return;
        if (cursorManager != null)
        {
            if (cardData != null && cardData.isPlayable)
                cursorManager.SetClickableCursor();
            else if (IsUnplayedEncounterWithHex())
                cursorManager.SetClickableCursor();
            else
                cursorManager.SetDisableCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!SuppressHoverEffects) ShowToken();
        if (SuppressHoverEffects) return;
        if (cursorManager != null) cursorManager.SetDefaultCursor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (canvasGroup != null && !canvasGroup.interactable)
            {
                if (IsUnplayedEncounterWithHex())
                {
                    BoardNavigator.Instance?.LookAt(cardData.encounterTargetHex.transform.position);
                    FlashEncounterHintFrame(cardData.encounterTargetHex);
                }
                return;
            }
            TryPlayCard();
        }
    }

    private async void TryPlayCard()
    {
        if (cardData == null) return;
        if (canvasGroup != null && !canvasGroup.interactable) return;

        Board board = FindFirstObjectByType<Board>();
        SelectedCharacterIcon icon = FindFirstObjectByType<SelectedCharacterIcon>();
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
            if (IsUnplayedEncounterWithHex())
            {
                BoardNavigator.Instance?.LookAt(cardData.encounterTargetHex.transform.position);
                FlashEncounterHintFrame(cardData.encounterTargetHex);
            }
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
            case CardTypeEnum.Environmental:
                success = await HandleEnvironmentalCardPlayed(playedSelected);
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
        btn.interactable = cardData != null && !cardData.IsEncounterCard();
    }

    public void Discard()
    {
        _ = TryDiscardAsync();
    }

    private async Task<bool> TryDiscardAsync()
    {
        if (cardData == null) return false;

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
            StartCoroutine(AnimateDiscardAndDestroy());
        }
        return true;
    }

    private IEnumerator AnimateDiscardAndDestroy()
    {
        // Block further interaction immediately.
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Escape the GridLayout so siblings reflow while we fly away.
        Canvas.ForceUpdateCanvases();
        Transform gridParent = rectTransform.parent;
        Transform floatTarget = (gridParent != null && gridParent.parent != null) ? gridParent.parent : gridParent;
        if (floatTarget != null && floatTarget != rectTransform.parent)
        {
            Vector3 worldPos = rectTransform.position;
            rectTransform.SetParent(floatTarget, false);
            rectTransform.position = worldPos;
            rectTransform.SetAsLastSibling();
        }
        else if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        Vector2 startPos = rectTransform.anchoredPosition;
        float drift = UnityEngine.Random.Range(-70f, 70f);
        Vector2 endPos = startPos + new Vector2(drift, 200f);
        float startRot = rectTransform.localEulerAngles.z;
        float endRot = startRot + UnityEngine.Random.Range(-18f, 18f);

        float duration = 0.32f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (this == null) yield break;
            float p = elapsed / duration;
            float eased = 1f - (1f - p) * (1f - p);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRot, endRot, p));
            rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.55f, p);
            if (canvasGroup != null) canvasGroup.alpha = 1f - p;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Destroy(gameObject);
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

    public void ShowToken()
    {
        if (lockedToRealCard) return;
        if (tokenCanvasGroup != null)
        {
            tokenCanvasGroup.alpha = 1f;
            tokenCanvasGroup.blocksRaycasts = true;
            tokenCanvasGroup.interactable = true;
        }
        if (realCardCanvasGroup != null)
        {
            realCardCanvasGroup.alpha = 0f;
            realCardCanvasGroup.blocksRaycasts = false;
            realCardCanvasGroup.interactable = false;
        }
        if (tokenImage != null) tokenImage.raycastTarget = true;
        if (rootHitGraphic != null) rootHitGraphic.raycastTarget = false;
    }

    public void ShowRealCard()
    {
        if (tokenCanvasGroup != null)
        {
            tokenCanvasGroup.alpha = 0f;
            tokenCanvasGroup.blocksRaycasts = false;
            tokenCanvasGroup.interactable = false;
        }
        if (realCardCanvasGroup != null)
        {
            realCardCanvasGroup.alpha = 1f;
            realCardCanvasGroup.blocksRaycasts = true;
            realCardCanvasGroup.interactable = true;
        }
        if (tokenImage != null) tokenImage.raycastTarget = false;
        if (rootHitGraphic != null) rootHitGraphic.raycastTarget = true;
        if (cardBackgroundImage != null) cardBackgroundImage.raycastTarget = true;
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
            if (cardData.IsEncounterCard() && cardData.encounterTargetHex != null)
            {
                string hexCoords = $"{cardData.encounterTargetHex.v2.x}, {cardData.encounterTargetHex.v2.y}";
                messages.Add($"<sprite name=\"error\">Move your character to hex {hexCoords} to investigate.");
            }
            else if (cardData.IsEncounterCard())
            {
                messages.Add("<sprite name=\"error\">Move your character to that hex to investigate.");
            }
            else
            {
                messages.Add("<sprite name=\"error\">Action conditions not met.");
            }
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
        bool spellArcaneBypass = cardData.GetCardType() == CardTypeEnum.Spell
            && selected.HasStatusEffect(StatusEffectEnum.ArcaneInsight);
        if (!spellArcaneBypass)
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

    private async Task<bool> HandleEnvironmentalCardPlayed(Character selected)
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null) return false;
        PlayableLeader playerLeader = game.player;
        if (playerLeader == null) return false;

        if (!deckManager.TryConsumeCard(playerLeader, cardData.name, false, out _))
            return false;

        EnvironmentalCardManager.GetOrCreate().SetActiveCard(cardData);
        playerLeader.RecordPlayedCard(cardData);

        await Task.Yield();
        return true;
    }

    private async Task<bool> HandleEncounterCardPlayed(Character selected)
    {
        if (!cardData.encounterRevealed)
        {
            if (canvasGroup != null) canvasGroup.interactable = false;
            await RevealEncounterCardAsync();
            cardData.encounterRevealed = true;
            UpdateInteractableState();
        }

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

    private async Task RevealEncounterCardAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(RevealEncounterCoroutine(tcs));
        await tcs.Task;
    }

    private IEnumerator RevealEncounterCoroutine(TaskCompletionSource<bool> tcs)
    {
        const float FadeDuration = 0.8f;
        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            if (this == null) { tcs.TrySetResult(false); yield break; }
            float alpha = 1f - elapsed / FadeDuration;

            if (encounterArtOverlay != null)
            {
                Color c = encounterArtOverlay.color;
                c.a = alpha;
                encounterArtOverlay.color = c;
            }
            if (encounterQuestionMark != null)
            {
                Color c = encounterQuestionMark.color;
                c.a = alpha;
                encounterQuestionMark.color = c;
            }
            if (encounterTokenOverlay != null)
            {
                Color c = encounterTokenOverlay.color;
                c.a = alpha;
                encounterTokenOverlay.color = c;
            }
            if (encounterTokenQuestionMark != null)
            {
                Color c = encounterTokenQuestionMark.color;
                c.a = alpha;
                encounterTokenQuestionMark.color = c;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (encounterArtOverlay != null)
        {
            Destroy(encounterArtOverlay.gameObject);
            encounterArtOverlay = null;
            encounterQuestionMark = null;
        }
        if (encounterTokenOverlay != null)
        {
            Destroy(encounterTokenOverlay.gameObject);
            encounterTokenOverlay = null;
            encounterTokenQuestionMark = null;
        }

        if (titleText != null) titleText.text = FormatCardTitle(cardData.name);

        string realDescription = GetActionDescription(cardData);
        yield return StartCoroutine(TypewriterEffectCoroutine(descriptionText, realDescription));
        baseDescription = realDescription;

        tcs.SetResult(true);
    }

    private IEnumerator TypewriterEffectCoroutine(TextMeshProUGUI textComponent, string fullText)
    {
        if (textComponent == null || string.IsNullOrEmpty(fullText)) yield break;
        textComponent.text = string.Empty;
        float delay = Mathf.Min(0.05f, 2f / fullText.Length);
        foreach (char c in fullText)
        {
            if (textComponent == null) yield break;
            textComponent.text += c;
            yield return new WaitForSecondsRealtime(delay);
        }
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

}
