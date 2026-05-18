using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[Serializable]
public class CardsManifest
{
    public int deckCount = 0;
    public List<DeckManifestEntry> decks = new();
}

[Serializable]
public class DeckManifestEntry
{
    public string deckId;
    public string nation;
    public string thematic;
    public int alignment;
    public string resourcePath;
    public int cardCount;
    public bool sharedToAll;
    public string parentDeckId;
    public bool isBaseDeck;
    public string deckSpriteName;
}

[Serializable]
public class DeckData
{
    public string deckId;
    public string nation;
    public int alignment;
    public List<CardData> cards = new();
}

[Serializable]
public class CardPlayabilityResult
{
    public bool isPlayable;
    public bool failsLevelRequirements;
    public bool failsResourceRequirements;
    public bool failsActionConditions;
    public bool failsCardHistoryRequirements;
    public bool failsStartingCityRequirement;
    public string cardHistoryReason;
    public string startingCityReason;

    public void Reset()
    {
        isPlayable = false;
        failsLevelRequirements = false;
        failsResourceRequirements = false;
        failsActionConditions = false;
        failsCardHistoryRequirements = false;
        failsStartingCityRequirement = false;
        cardHistoryReason = null;
        startingCityReason = null;
    }
}

[Serializable]
public class EncounterStatusEffectData
{
    public string statusId;
    public int turns = 1;
}

[Serializable]
public class EncounterOutcomeData
{
    public string outcomeId;
    public string resultText;
    public string requiredAlignment = string.Empty;
    public int minCommander;
    public int minAgent;
    public int minEmmissary;
    public int minMage;
    public int minHealth;
    public int maxHealth = -1;
    public int healthDelta;
    public int goldDelta;
    public int leatherDelta;
    public int timberDelta;
    public int mountsDelta;
    public int ironDelta;
    public int steelDelta;
    public int mithrilDelta;
    public List<EncounterStatusEffectData> statuses = new();
}

[Serializable]
public class EncounterOptionData
{
    public string optionId;
    public string label;
    public string description;
    public List<EncounterOutcomeData> outcomes = new();
}

[Serializable]
public class CardData
{
    public int cardId;
    public string name;
    public string quote;
    public string actionEffect;
    public string type;
    public List<string> tags = new();
    public string deckId;
    public int alignment;
    public string actionClassName;
    public string action;
    public string spriteName;
    public string region;
    public string description;
    public string requirementsText;
    public string historyText;
    public string statusEffect;
    public int procChance;
    public string portraitName;
    public string characterGroup;
    public string referenceDeckId;
    public int referenceCardId;
    public List<EncounterOptionData> encounterOptions = new();
    public EncounterOptionData fleeOption;
    public int commander;
    public int agent;
    public int emmissary;
    public int mage;
    public RacesEnum race;
    public List<string> artifacts = new();
    public TroopsTypeEnum troopType;
    public List<ArmySpecialAbilityEnum> specialAbilities = new();

    // Card-owned requirements (migrated from Actions.json)
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;
    public int difficulty;
    public int leatherRequired;
    public int mountsRequired;
    public int timberRequired;
    public int ironRequired;
    public int steelRequired;
    public int mithrilRequired;
    public int goldRequired;
    public int jokerRequired;

    public int leatherGranted;
    public int mountsGranted;
    public int timberGranted;
    public int ironGranted;
    public int steelGranted;
    public int mithrilGranted;
    public int goldGranted;

    public string startingPC = string.Empty;
    public InspireEffectData inspireEffectData;
    public int amount = 1;
    public string deckSpriteName;

    [NonSerialized] public bool isPlayable;
    [NonSerialized] public CardPlayabilityResult playability = new CardPlayabilityResult();

    public CardTypeEnum GetCardType()
    {
        return CardTypeParser.Parse(type);
    }

    public int GetCharacterPointTotal()
    {
        if (GetCardType() != CardTypeEnum.Character) return 0;
        return Mathf.Max(0, commander) + Mathf.Max(0, agent) + Mathf.Max(0, emmissary) + Mathf.Max(0, mage);
    }

    public int GetAdditionalGoldCost()
    {
        return GetCardType() == CardTypeEnum.Character ? GetCharacterPointTotal() * 5 : 0;
    }

    public int GetTotalGoldCost()
    {
        if (GetCardType() == CardTypeEnum.Character)
        {
            return GetAdditionalGoldCost();
        }

        return Mathf.Max(0, goldRequired) + GetAdditionalGoldCost();
    }

    public bool IsEventCard()
    {
        return GetCardType() == CardTypeEnum.Event;
    }

    public bool IsEncounterCard()
    {
        return GetCardType() == CardTypeEnum.Encounter;
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tags == null) return false;
        return tags.Any(t => string.Equals(t?.Trim(), tag.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyTag(params string[] queryTags)
    {
        if (queryTags == null || queryTags.Length == 0) return false;
        return queryTags.Any(HasTag);
    }

    public string GetActionRef()
    {
        return !string.IsNullOrWhiteSpace(action) ? action : actionClassName;
    }

    public string GetRenderedDescription(bool includeFoundingText = false)
    {
        string body = GetDescriptionBody(includeFoundingText);
        string quoteBlock = GetQuoteBlock();

        if (string.IsNullOrWhiteSpace(body))
        {
            return quoteBlock;
        }

        if (string.IsNullOrWhiteSpace(quoteBlock))
        {
            return body;
        }

        return $"{body}\n\n{quoteBlock}";
    }

    public string GetDescriptionBody(bool includeFoundingText = false)
    {
        CardTypeEnum cardType = GetCardType();
        string body = cardType switch
        {
            CardTypeEnum.Character => GetCharacterDescription(),
            CardTypeEnum.Army => GetArmyDescription(),
            CardTypeEnum.Land => GetLandDescription(),
            CardTypeEnum.PC => PcDescriptionBuilder.BuildBody(this, includeFoundingText),
            CardTypeEnum.Event or CardTypeEnum.Action or CardTypeEnum.Spell => GetActionEffectText(),
            CardTypeEnum.Encounter => !string.IsNullOrWhiteSpace(description) ? description.Trim() : string.Empty,
            _ => string.Empty
        };

        if (cardType == CardTypeEnum.Character && !string.IsNullOrWhiteSpace(actionEffect))
        {
            string effect = actionEffect.Trim();
            body = string.IsNullOrWhiteSpace(body) ? effect : $"{body}\n\n{effect}";
        }

        return cardType == CardTypeEnum.Action
            || cardType == CardTypeEnum.Event
            || cardType == CardTypeEnum.Spell
            || cardType == CardTypeEnum.Land
            || cardType == CardTypeEnum.PC
                ? AppendCardStatusText(body)
                : body;
    }

    public string GetQuoteBlock()
    {
        if (string.IsNullOrWhiteSpace(quote)) return string.Empty;

        string text = Regex.Replace(quote.Trim(), "<[^>]+>", string.Empty).Trim();
        if (text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal) && text.Length >= 2)
        {
            text = text.Substring(1, text.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return $"<align=\"center\"><color=#d3d3d388><i>\"{text}\"</i></color></align>";
    }

    public string GetArmyDescription()
    {
        if (GetCardType() != CardTypeEnum.Army) return string.Empty;
        return GetArmySummary();
    }

    public string GetActionEffectText()
    {
        return string.IsNullOrWhiteSpace(actionEffect) ? string.Empty : actionEffect.Trim();
    }

    private string AppendCardStatusText(string body)
    {
        // Status effects are internal gameplay state and should not be exposed in card authoring/display.
        // Army proc configuration is driven by specialAbilities + procChance instead.
        return body;
    }

    private string GetArmySummary()
    {
        if (GetCardType() != CardTypeEnum.Army) return string.Empty;

        string raceLabel = FormatRaceLabel(race);
        string troopLabel = GetDefaultTroopName(troopType);
        if (string.IsNullOrWhiteSpace(troopLabel))
        {
            troopLabel = !string.IsNullOrWhiteSpace(name) ? name : string.Empty;
        }

        string spriteTag = $"<sprite name=\"{troopType.ToString().ToLowerInvariant()}\">";
        List<string> abilities = GetArmyAbilityLabels();

        if (string.IsNullOrWhiteSpace(troopLabel))
        {
            if (abilities.Count > 0)
            {
                return !string.IsNullOrWhiteSpace(raceLabel)
                    ? $"{raceLabel}. {string.Join(", ", abilities)}."
                    : string.Join(", ", abilities);
            }

            return raceLabel;
        }

        string baseText = string.IsNullOrWhiteSpace(raceLabel)
            ? $"{troopLabel} {spriteTag}."
            : $"{raceLabel}. {troopLabel} {spriteTag}.";
        return abilities.Count > 0
            ? $"{baseText} {string.Join(", ", abilities)}."
            : baseText;
    }

    private string GetLandDescription()
    {
        if (GetCardType() != CardTypeEnum.Land) return string.Empty;

        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(region))
        {
            parts.Add($"{PcDescriptionBuilder.FormatDisplayRegionName(region)}.");
        }

        List<string> grants = new();
        if (leatherGranted > 0) grants.Add($"{leatherGranted}<sprite name=\"leather\">");
        if (timberGranted > 0) grants.Add($"{timberGranted}<sprite name=\"timber\">");
        if (mountsGranted > 0) grants.Add($"{mountsGranted}<sprite name=\"mounts\">");
        if (ironGranted > 0) grants.Add($"{ironGranted}<sprite name=\"iron\">");
        if (steelGranted > 0) grants.Add($"{steelGranted}<sprite name=\"steel\">");
        if (mithrilGranted > 0) grants.Add($"{mithrilGranted}<sprite name=\"mithril\">");
        if (goldGranted > 0) grants.Add($"{goldGranted}<sprite name=\"gold\">");
        if (grants.Count > 0)
        {
            parts.Add(string.Join(string.Empty, grants));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add($"Reveals hexes and allows founding PCs originally from {PcDescriptionBuilder.FormatDisplayRegionName(name)}.");
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public string GetCharacterDescription()
    {
        if (GetCardType() != CardTypeEnum.Character) return string.Empty;

        List<string> lines = new();

        if (!string.IsNullOrWhiteSpace(startingPC))
        {
            lines.Add($"Starts at {startingPC}.");
        }

        List<string> classParts = new();
        AppendCharacterLevel(classParts, "commander", commander);
        AppendCharacterLevel(classParts, "agent", agent);
        AppendCharacterLevel(classParts, "emmissary", emmissary);
        AppendCharacterLevel(classParts, "mage", mage);
        if (classParts.Count > 0)
        {
            lines.Add(string.Join(" ", classParts));
        }

        return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
    }

    public bool EvaluatePlayability(Character selectedCharacter, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        playability ??= new CardPlayabilityResult();
        playability.Reset();

        if (GetCardType() == CardTypeEnum.Character || GetCardType() == CardTypeEnum.Army)
        {
            bool cardResourcesOk = resourceCheck != null
                ? resourceCheck(selectedCharacter)
                : selectedCharacter != null && MeetsResourceRequirements(selectedCharacter.GetOwner());
            bool cardConditionsOk = conditionCheck == null || conditionCheck(selectedCharacter);

            playability.failsResourceRequirements = !cardResourcesOk;
            playability.failsActionConditions = !cardConditionsOk;

            bool startingCityOk = true;
            if (GetCardType() == CardTypeEnum.Character && !string.IsNullOrWhiteSpace(startingPC))
            {
                Hex hex = selectedCharacter?.hex;
                PC pc = hex?.GetPCData();
                startingCityOk = pc != null && CardNameUtility.Equals(pc.pcName, startingPC);
                if (!startingCityOk)
                {
                    playability.failsStartingCityRequirement = true;
                    playability.startingCityReason = $"Must be played at {startingPC}.";
                }
            }

            isPlayable = cardResourcesOk && cardConditionsOk && startingCityOk;
            playability.isPlayable = isPlayable;
            return isPlayable;
        }

        if (selectedCharacter == null)
        {
            playability.failsActionConditions = true;
            isPlayable = false;
            return false;
        }

        bool levelsOk = selectedCharacter.GetCommander() >= commanderSkillRequired
            && selectedCharacter.GetAgent() >= agentSkillRequired
            && selectedCharacter.GetEmmissary() >= emissarySkillRequired
            && selectedCharacter.GetMage() >= mageSkillRequired;

        bool resourcesOk = resourceCheck != null
            ? resourceCheck(selectedCharacter)
            : MeetsResourceRequirements(selectedCharacter.GetOwner());
        bool conditionsOk = conditionCheck == null || conditionCheck(selectedCharacter);
        bool cardHistoryOk = MeetsCardHistoryRequirements(selectedCharacter.GetOwner(), out string cardHistoryReason);

        playability.failsLevelRequirements = !levelsOk;
        playability.failsResourceRequirements = !resourcesOk;
        playability.failsActionConditions = !conditionsOk;
        playability.failsCardHistoryRequirements = !cardHistoryOk;
        playability.cardHistoryReason = cardHistoryReason;

        isPlayable = levelsOk && resourcesOk && conditionsOk && cardHistoryOk;
        playability.isPlayable = isPlayable;
        return isPlayable;
    }

    public bool MeetsResourceRequirements(Leader owner)
    {
        if (owner == null) return false;
        if (leatherRequired > 0 && owner.leatherAmount < leatherRequired) return false;
        if (timberRequired > 0 && owner.timberAmount < timberRequired) return false;
        if (mountsRequired > 0 && owner.mountsAmount < mountsRequired) return false;
        if (ironRequired > 0 && owner.ironAmount < ironRequired) return false;
        if (steelRequired > 0 && owner.steelAmount < steelRequired) return false;
        if (mithrilRequired > 0 && owner.mithrilAmount < mithrilRequired) return false;
        if (GetTotalGoldCost() > 0 && owner.goldAmount < GetTotalGoldCost()) return false;
        return true;
    }

    public bool MeetsCardHistoryRequirements(Leader owner, out string reason)
    {
        reason = null;
        if (owner is not PlayableLeader playableLeader) return true;

        if (GetCardType() == CardTypeEnum.Land && playableLeader.HasPlayedLandThisTurn())
        {
            reason = "Only one land card can be played each turn.";
            return false;
        }

        if (GetCardType() != CardTypeEnum.PC) return true;
        if (string.IsNullOrWhiteSpace(region)) return true;
        if (IsRegionDiscovered(region, owner)) return true;

        reason = $"{region} not discovered yet.";
        return false;
    }

    private bool IsRegionDiscovered(string region, Leader owner)
    {
        if (string.IsNullOrWhiteSpace(region) || owner == null) return true;

        Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
        Board board = game?.board != null ? game.board : UnityEngine.Object.FindFirstObjectByType<Board>();
        if (board == null) return false;

        DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        string normalizedTarget = NormalizeRegion(region);

        foreach (Hex hex in board.hexes.Values)
        {
            if (hex == null) continue;

            string hexRegion = hex.GetLandRegion();
            if (string.IsNullOrWhiteSpace(hexRegion))
            {
                PC pc = hex.GetPCData();
                if (pc != null && deckManager != null)
                {
                    hexRegion = deckManager.ResolveRegionForPc(pc);
                }
            }

            if (string.IsNullOrWhiteSpace(hexRegion)) continue;
            if (!string.Equals(NormalizeRegion(hexRegion), normalizedTarget, StringComparison.Ordinal)) continue;

            if (hex.IsHexRevealed() || hex.IsScoutedBy(owner)) return true;
        }

        return false;
    }

    private static string NormalizeRegion(string s)
        => string.IsNullOrWhiteSpace(s) ? string.Empty : new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string GetDefaultTroopName(TroopsTypeEnum troopType)
    {
        return troopType switch
        {
            TroopsTypeEnum.ma => "Men-at-arms",
            TroopsTypeEnum.ar => "Archers",
            TroopsTypeEnum.li => "Light Infantry",
            TroopsTypeEnum.hi => "Heavy Infantry",
            TroopsTypeEnum.lc => "Light Cavalry",
            TroopsTypeEnum.hc => "Heavy Cavalry",
            TroopsTypeEnum.ca => "Catapults",
            TroopsTypeEnum.ws => "Warships",
            _ => string.Empty
        };
    }

    private static string FormatRaceLabel(RacesEnum value)
    {
        string raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string formatted = raw.Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    private List<string> GetArmyAbilityLabels()
    {
        if (specialAbilities == null || specialAbilities.Count == 0) return new List<string>();

        ArmySpecialAbilityEnum ability = specialAbilities.First();
        string label = FormatArmyAbilityLabel(ability);
        if (string.IsNullOrWhiteSpace(label)) return new List<string>();
        int chance = Mathf.Clamp(procChance <= 0 ? 100 : procChance, 1, 100);
        return new List<string> { $"{label} {chance}%" };
    }

    private static string FormatArmyAbilityLabel(ArmySpecialAbilityEnum ability)
    {
        string abilityName = ability switch
        {
            ArmySpecialAbilityEnum.Longrange => "Long range",
            ArmySpecialAbilityEnum.ShortRange => "Short range",
            ArmySpecialAbilityEnum.RefusingDuels => "Refusing duels",
            ArmySpecialAbilityEnum.ArcaneInsight => "Arcane insight",
            ArmySpecialAbilityEnum.DuelSupremacy => "Duel supremacy",
            ArmySpecialAbilityEnum.MorgulTouch => "Morgul touch",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                Regex.Replace(ability.ToString(), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant())
        };

        string spriteName = ability switch
        {
            ArmySpecialAbilityEnum.Longrange => "longrange",
            ArmySpecialAbilityEnum.ShortRange => "shortrange",
            ArmySpecialAbilityEnum.ArcaneInsight => "arcaneinsight",
            ArmySpecialAbilityEnum.RefusingDuels => "refusingduels",
            ArmySpecialAbilityEnum.DuelSupremacy => "duelsupremacy",
            ArmySpecialAbilityEnum.MorgulTouch => "morgultouch",
            _ => ability.ToString().ToLowerInvariant()
        };

        return $"{abilityName} <sprite name=\"{spriteName}\">";
    }

    private static void AppendCharacterLevel(List<string> parts, string spriteName, int required)
    {
        if (parts == null || string.IsNullOrWhiteSpace(spriteName) || required <= 0) return;
        parts.Add($"{required}<sprite name=\"{spriteName}\">");
    }
}

public class DeckManager : MonoBehaviour
{
    private enum BalancedDeckBucket
    {
        Army,
        Event,
        PC,
        Land,
        Encounter,
        Character,
        ActionSpell,
        Misc
    }

    private static readonly BalancedDeckBucket[] BalancedDrawPattern =
    {
        BalancedDeckBucket.Army,
        BalancedDeckBucket.Army,
        BalancedDeckBucket.Event,
        BalancedDeckBucket.Event,
        BalancedDeckBucket.Event,
        BalancedDeckBucket.PC,
        BalancedDeckBucket.Land,
        BalancedDeckBucket.Land,
        BalancedDeckBucket.Land,
        BalancedDeckBucket.Encounter,
        BalancedDeckBucket.Character,
        BalancedDeckBucket.ActionSpell,
        BalancedDeckBucket.ActionSpell,
        BalancedDeckBucket.ActionSpell,
        BalancedDeckBucket.ActionSpell
    };

    private class PlayerDeckState
    {
        public string deckId;
        public readonly List<CardData> drawPile = new();
        public readonly List<CardData> hand = new();
        public readonly List<CardData> discardPile = new();
        public int turnsWithoutRegionCard;
    }

    public static DeckManager Instance { get; private set; }

    [Header("References")]
    [FormerlySerializedAs("cardCameObject")]
    [SerializeField] GameObject cardCameObject;
    [SerializeField] GridLayoutGroup gridLayout;
    [SerializeField] CanvasGroup handCanvasGroup;

    [Header("Config")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private string cardsManifestResourcePath = "Cards";
    [SerializeField] private int handSize = 6;
    [Tooltip("Extra vertical offset (pixels) applied to the zoomed card above the default lift. Increase to push the zoom card higher up.")]
    [SerializeField] private float zoomCardYOffset = 30f;

    [Header("Debug")]
    [SerializeField] private bool logInitialization = true;

    public List<CardData> cards = new();

    [Header("Status Effects")]
    [SerializeField] private List<string> availableStatusEffectIds = Enum.GetNames(typeof(StatusEffectEnum)).ToList();

    [Header("Inspector (Runtime)")]
    [SerializeField] private List<DeckData> inspectorDecks = new();

    private readonly Dictionary<string, DeckManifestEntry> deckManifestById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeckData> loadedDecksById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PlayableLeader, PlayerDeckState> playerDecks = new();
    private readonly List<GameObject> handCardInstances = new();

    private bool loaded;
    private bool isRefreshingHumanHandUI;

    public IReadOnlyList<string> AvailableStatusEffectIds => availableStatusEffectIds;

    private void OnValidate()
    {
        availableStatusEffectIds = Enum.GetNames(typeof(StatusEffectEnum)).ToList();
        ApplyZoomCardYOffsetToHand();
    }

    private void ApplyZoomCardYOffsetToHand()
    {
        foreach (GameObject go in handCardInstances)
        {
            if (go == null) continue;
            if (go.TryGetComponent<Card>(out Card card))
            {
                card.ZoomYOffset = zoomCardYOffset;
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveHandCanvasGroup();
    }

    private void Start()
    {
        if (!initializeOnStart) return;

        InitializeFromResources();

        Game game = FindFirstObjectByType<Game>();
        if (game != null && game.started)
        {
            InitializeHandsForCurrentGame();
        }
    }

    public bool InitializeFromResources()
    {
        loaded = false;
        cards.Clear();
        inspectorDecks.Clear();
        deckManifestById.Clear();
        loadedDecksById.Clear();
        playerDecks.Clear();

        TextAsset manifestAsset = Resources.Load<TextAsset>(cardsManifestResourcePath);
        if (manifestAsset == null)
        {
            Debug.LogWarning($"DeckManager: Could not load cards manifest from Resources/{cardsManifestResourcePath}.json");
            return false;
        }

        CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (manifest == null || manifest.decks == null || manifest.decks.Count == 0)
        {
            Debug.LogWarning("DeckManager: Cards manifest is empty or malformed.");
            return false;
        }

        foreach (DeckManifestEntry entry in manifest.decks)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.deckId)) continue;
            deckManifestById[entry.deckId] = entry;
        }

        foreach (DeckManifestEntry entry in deckManifestById.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.resourcePath)) continue;

            TextAsset deckAsset = Resources.Load<TextAsset>(entry.resourcePath);
            if (deckAsset == null)
            {
                Debug.LogWarning($"DeckManager: Could not load deck file Resources/{entry.resourcePath}.json");
                continue;
            }

            DeckData deckData = JsonUtility.FromJson<DeckData>(deckAsset.text);
            if (deckData == null || string.IsNullOrWhiteSpace(deckData.deckId))
            {
                Debug.LogWarning($"DeckManager: Deck file at {entry.resourcePath} is empty or malformed.");
                continue;
            }

            if (deckData.cards == null) deckData.cards = new();
            foreach (CardData card in deckData.cards)
            {
                if (card == null) continue;
                card.deckId = deckData.deckId;
                card.alignment = deckData.alignment;
                card.deckSpriteName = entry.deckSpriteName;
            }

            loadedDecksById[deckData.deckId] = deckData;
            cards.AddRange(deckData.cards);
        }

        InjectMissingStartingPcAndLandReferences();
        ResolveCardReferences();
        cards.Clear();
        foreach (DeckManifestEntry entry in deckManifestById.Values)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.deckId)) continue;
            if (!loadedDecksById.TryGetValue(entry.deckId, out DeckData deckData) || deckData?.cards == null) continue;
            cards.AddRange(deckData.cards);
        }

        loaded = loadedDecksById.Count > 0;
        RefreshInspectorDecks();

        if (logInitialization)
        {
            Debug.Log($"DeckManager: Loaded {loadedDecksById.Count} decks, {cards.Count} cards, handSize={handSize}.");
        }

        return loaded;
    }

    public bool InitializeHandsForCurrentGame()
    {
        if (!loaded && !InitializeFromResources()) return false;

        Game game = FindFirstObjectByType<Game>();
        if (game == null)
        {
            Debug.LogWarning("DeckManager: Game not found; cannot initialize player hands.");
            return false;
        }

        List<PlayableLeader> leaders = new();
        if (game.player != null) leaders.Add(game.player);
        if (game.competitors != null) leaders.AddRange(game.competitors.Where(x => x != null));

        InitializeHands(leaders);
        if (game.player != null)
        {
            EnsureTutorialHandForPlayer(game.player);
            if (!ShouldSkipTurnRefillForTutorialHuman(game.player)
                && playerDecks.TryGetValue(game.player, out PlayerDeckState humanState))
            {
                EnsureRegionCardInHand(humanState);
                humanState.turnsWithoutRegionCard = HandHasRegionCard(humanState) ? 0 : 1;
                EnsureSharedBaseCardInHand(humanState);
            }
        }
        RefreshHumanPlayerHandUI();
        return true;
    }

    public void InitializeHands(IEnumerable<PlayableLeader> leaders)
    {
        playerDecks.Clear();
        if (leaders == null) return;

        foreach (PlayableLeader leader in leaders.Distinct())
        {
            if (leader == null) continue;
            PlayerDeckState state = BuildDeckStateForLeader(leader);
            if (state != null)
            {
                playerDecks[leader] = state;
            }
        }
    }

    public IReadOnlyList<CardData> GetHand(PlayableLeader leader)
    {
        if (leader == null) return Array.Empty<CardData>();
        return playerDecks.TryGetValue(leader, out PlayerDeckState state) ? state.hand : Array.Empty<CardData>();
    }

    public IReadOnlyList<CardData> GetDrawPile(PlayableLeader leader)
    {
        if (leader == null) return Array.Empty<CardData>();
        return playerDecks.TryGetValue(leader, out PlayerDeckState state) ? state.drawPile : Array.Empty<CardData>();
    }

    public int GetHandSize()
    {
        return handSize;
    }

    public bool TryDrawCard(PlayableLeader leader, out CardData card)
    {
        card = null;
        if (leader == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        if (state.drawPile.Count == 0 && state.discardPile.Count > 0)
        {
            state.drawPile.AddRange(state.discardPile);
            state.discardPile.Clear();
            ApplyBalancedDrawOrdering(state.drawPile);
        }

        if (state.drawPile.Count == 0) return false;

        EnsureSubdeckCardIfNeeded(state);
        card = state.drawPile[0];
        state.drawPile.RemoveAt(0);
        state.hand.Add(card);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool TryPlayCard(PlayableLeader leader, string cardName, out CardData card)
    {
        card = null;
        if (leader == null || string.IsNullOrWhiteSpace(cardName)) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && CardNameUtility.Equals(x.name, cardName));
        if (index < 0) return false;

        card = state.hand[index];
        state.hand.RemoveAt(index);
        state.discardPile.Add(card);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool TryConsumeCard(PlayableLeader leader, string cardName, bool drawReplacement, out CardData consumedCard)
    {
        consumedCard = null;
        if (leader == null || string.IsNullOrWhiteSpace(cardName)) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && CardNameUtility.Equals(x.name, cardName));
        if (index < 0) return false;

        consumedCard = state.hand[index];
        if (consumedCard == null) return false;
        if (!consumedCard.MeetsResourceRequirements(leader))
        {
            consumedCard = null;
            return false;
        }

        state.hand.RemoveAt(index);
        state.discardPile.Add(consumedCard);
        ApplyCardCosts(leader, consumedCard);

        if (drawReplacement)
        {
            TryDrawCard(leader, out _);
        }
        else
        {
            RefreshHumanPlayerHandUIIfHuman(leader);
        }

        return true;
    }

    public bool TryDiscardCard(PlayableLeader leader, string cardName, out CardData discardedCard)
    {
        discardedCard = null;
        if (leader == null || string.IsNullOrWhiteSpace(cardName)) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && CardNameUtility.Equals(x.name, cardName));
        if (index < 0) return false;

        discardedCard = state.hand[index];
        if (discardedCard == null || discardedCard.IsEncounterCard()) return false;

        state.hand.RemoveAt(index);
        state.discardPile.Add(discardedCard);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool TryAddCardToHand(PlayableLeader leader, CardData card)
    {
        if (leader == null || card == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        state.hand.Add(CloneCard(card));
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool HasDeckFor(PlayableLeader leader)
    {
        return leader != null && playerDecks.ContainsKey(leader);
    }

    public bool HasActionCardInDeck(Leader leader, string actionClassName)
    {
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        bool Matches(CardData card)
        {
            if (card == null) return false;
            if (!IsConsumableEffectCard(card)) return false;
            string cardRef = card.GetActionRef();
            return !string.IsNullOrWhiteSpace(actionClassName) &&
                string.Equals(cardRef, actionClassName, StringComparison.OrdinalIgnoreCase);
        }

        return state.hand.Any(Matches)
            || state.drawPile.Any(Matches)
            || state.discardPile.Any(Matches);
    }

    public bool HasActionCardInHand(Leader leader, string actionClassName, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;
        return FindMatchingActionCardIndex(state.hand, actionClassName, selectedCharacter, resourceCheck, conditionCheck) >= 0;
    }

    public bool TryGetActionCardInHand(Leader leader, string actionClassName, out CardData card, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        card = null;
        if (leader is not PlayableLeader playableLeader) return false;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        int handIndex = FindMatchingActionCardIndex(state.hand, actionClassName, selectedCharacter, resourceCheck, conditionCheck);
        if (handIndex < 0) return false;
        card = state.hand[handIndex];
        return card != null;
    }

    public int GetActionCardDifficulty(Leader leader, string actionClassName, Character selectedCharacter = null)
    {
        if (TryGetActionCardInHand(leader, actionClassName, out CardData card, selectedCharacter))
        {
            return card != null ? Mathf.Max(0, card.difficulty) : 0;
        }
        return 0;
    }

    public bool TryConsumeActionCard(Leader leader, string actionClassName, bool drawReplacement, out CardData consumedCard, string preferredCardName = null)
    {
        consumedCard = null;
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        int handIndex = -1;
        if (!string.IsNullOrWhiteSpace(preferredCardName))
        {
            handIndex = state.hand.FindIndex(card =>
                card != null
                && string.Equals(card.name, preferredCardName, StringComparison.OrdinalIgnoreCase)
                && MatchesActionCard(card, actionClassName));
        }
        if (handIndex < 0)
        {
            handIndex = FindMatchingActionCardIndex(state.hand, actionClassName);
        }
        if (handIndex < 0) return false;

        consumedCard = state.hand[handIndex];
        if (consumedCard == null) return false;
        if (consumedCard.GetCardType() == CardTypeEnum.Land && playableLeader.HasPlayedLandThisTurn())
        {
            consumedCard = null;
            return false;
        }
        if (!consumedCard.MeetsResourceRequirements(playableLeader))
        {
            consumedCard = null;
            return false;
        }

        state.hand.RemoveAt(handIndex);
        state.discardPile.Add(consumedCard);
        ApplyCardCosts(playableLeader, consumedCard);

        if (drawReplacement)
        {
            TryDrawCard(playableLeader, out _);
        }
        else
        {
            RefreshHumanPlayerHandUIIfHuman(playableLeader);
        }

        return true;
    }

    public void ApplyMapRevealForPlayedCard(PlayableLeader leader, CardData card)
    {
        if (leader == null || card == null) return;

        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player != leader || !game.IsPlayerCurrentlyPlaying()) return;

        Board board = game.board != null ? game.board : FindFirstObjectByType<Board>();
        if (board == null || board.hexes == null || board.hexes.Count == 0) return;
        board.nationSpawner?.EnsureLandRegionsAssigned();

        List<Hex> revealedPcHexes = null;
        string revealMessage = null;
        string discoveryRegion = null;
        switch (card.GetCardType())
        {
            case CardTypeEnum.Land:
                revealedPcHexes = RevealRegion(board, card.name, leader, maxHexes: 1);
                revealMessage = $"The lands of {FormatDisplayName(card.name)} were revealed";
                discoveryRegion = card.name;
                break;
            case CardTypeEnum.PC:
                string pcRegion = !string.IsNullOrWhiteSpace(card.region) ? card.region : card.name;
                revealedPcHexes = RevealRegion(board, pcRegion, leader);
                revealMessage = $"The lands of {FormatDisplayName(pcRegion)} were revealed";
                discoveryRegion = pcRegion;
                break;
        }

        MinimapManager.RefreshMinimap();
        if (!string.IsNullOrWhiteSpace(discoveryRegion) && (revealedPcHexes == null || revealedPcHexes.Count == 0))
        {
            Debug.LogWarning($"DeckManager: No hexes matched reveal region '{discoveryRegion}'.");
        }

        if (leader is PlayableLeader pl && revealedPcHexes != null && revealedPcHexes.Count > 0
            && !string.IsNullOrWhiteSpace(discoveryRegion) && pl.TryDiscoverRegion(discoveryRegion))
        {
            NotifyRegionDiscovered(discoveryRegion, ChooseFocusHex(revealedPcHexes));
        }

        QueueRevealMessages(revealedPcHexes, revealMessage);
    }

    public bool TryReturnActionCardToHand(Leader leader, string actionClassName)
    {
        if (leader is not PlayableLeader playableLeader) return false;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;
        if (state.discardPile == null || state.discardPile.Count == 0) return false;

        int discardIndex = -1;
        for (int i = state.discardPile.Count - 1; i >= 0; i--)
        {
            CardData card = state.discardPile[i];
            if (!MatchesActionCard(card, actionClassName)) continue;
            discardIndex = i;
            break;
        }

        if (discardIndex < 0) return false;

        CardData returnedCard = state.discardPile[discardIndex];
        state.discardPile.RemoveAt(discardIndex);
        state.hand.Add(returnedCard);
        RefreshHumanPlayerHandUIIfHuman(playableLeader);
        return true;
    }

    public bool TryReturnCardToHand(Leader leader, string cardName)
    {
        if (leader is not PlayableLeader playableLeader) return false;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;
        if (state.discardPile == null || state.discardPile.Count == 0) return false;

        int discardIndex = state.discardPile.FindLastIndex(card => card != null && string.Equals(card.name, cardName, StringComparison.OrdinalIgnoreCase));
        if (discardIndex < 0) return false;

        CardData returnedCard = state.discardPile[discardIndex];
        state.discardPile.RemoveAt(discardIndex);
        state.hand.Add(returnedCard);
        RefreshHumanPlayerHandUIIfHuman(playableLeader);
        return true;
    }

    public bool TryReshuffleHandIntoDeckAndRedraw(PlayableLeader leader, int targetHandSize)
    {
        if (leader == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        Game game = FindFirstObjectByType<Game>();
        bool isHuman = game != null && game.player == leader && handCardInstances.Count > 0;

        if (isHuman)
        {
            List<GameObject> oldCards = new List<GameObject>(handCardInstances);
            handCardInstances.Clear();

            state.drawPile.AddRange(state.hand);
            state.hand.Clear();
            state.drawPile.AddRange(state.discardPile);
            state.discardPile.Clear();
            ApplyBalancedDrawOrdering(state.drawPile);
            RefillHandToCount(state, targetHandSize);
            EnsureSharedBaseCardInHand(state);

            StartCoroutine(PlayReshuffleAnimation(oldCards));
            return true;
        }

        state.drawPile.AddRange(state.hand);
        state.hand.Clear();
        state.drawPile.AddRange(state.discardPile);
        state.discardPile.Clear();

        ApplyBalancedDrawOrdering(state.drawPile);
        RefillHandToCount(state, targetHandSize);
        EnsureSharedBaseCardInHand(state);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool SetTutorialActionCards(PlayableLeader leader, IEnumerable<string> actionClassNames)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;

        ApplyTutorialActionCardsToState(leader, actionClassNames);

        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool SetTutorialCardsByName(PlayableLeader leader, IEnumerable<string> cardNames)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;

        ApplyTutorialCardsToStateByName(leader, cardNames);

        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool RestoreStandardHandAfterTutorial(PlayableLeader leader, int cardsToDraw = -1)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;

        PlayerDeckState rebuilt = BuildDeckStateForLeader(leader);
        if (rebuilt == null) return false;

        playerDecks[leader] = rebuilt;
        RefillHandToCount(rebuilt, Mathf.Max(0, cardsToDraw < 0 ? handSize : cardsToDraw));
        EnsureRegionCardInHand(rebuilt);
        rebuilt.turnsWithoutRegionCard = HandHasRegionCard(rebuilt) ? 0 : 1;
        EnsureSharedBaseCardInHand(rebuilt);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool ReplenishHandForTurn(PlayableLeader leader)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;
        if (ShouldSkipTurnRefillForTutorialHuman(leader))
        {
            Debug.Log($"[TutorialDebug] Turn refill skipped for tutorial leader '{leader.characterName}'");
            EnsureTutorialHandForPlayer(leader);
            RefreshHumanPlayerHandUIIfHuman(leader);
            return true;
        }

        RefillHandToCount(state, handSize);
        ApplyRegionCardGuarantee(state);
        EnsureSharedBaseCardInHand(state);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public void RefreshHumanPlayerHandUI()
    {
        if (isRefreshingHumanHandUI) return;
        isRefreshingHumanHandUI = true;
        try
        {
            ClearHandCardInstances();

            GameObject cardPrefab = ResolveCardPrefab();
            if (cardPrefab == null || gridLayout == null) return;

            Game game = FindFirstObjectByType<Game>();
            if (game == null || game.player == null) return;
            if (!playerDecks.TryGetValue(game.player, out PlayerDeckState state) || state.hand == null) return;

            foreach (CardData card in state.hand)
            {
                if (card == null) continue;

                GameObject cardGo = Instantiate(cardPrefab, gridLayout.transform);
                cardGo.SetActive(true);
                handCardInstances.Add(cardGo);

                Card cardComponent = cardGo.GetComponent<Card>();
                if (cardComponent == null)
                {
                    Debug.LogWarning("DeckManager: Card prefab is missing the Card component.");
                    continue;
                }

                cardComponent.ZoomYOffset = zoomCardYOffset;
                cardComponent.Initialize(card);
            }
        }
        finally
        {
            isRefreshingHumanHandUI = false;
        }
    }

    public void ClearHumanPlayerHandUI()
    {
        ClearHandCardInstances();
    }

    public void SetHumanHandVisible(bool visible)
    {
        CanvasGroup target = ResolveHandCanvasGroup();
        if (target == null) return;

        target.alpha = visible ? 1f : 0f;
        target.interactable = visible;
        target.blocksRaycasts = visible;
    }

    private void RefillHandToCount(PlayerDeckState state, int targetCount)
    {
        if (state == null) return;

        while (state.hand.Count > targetCount)
        {
            CardData last = state.hand[state.hand.Count - 1];
            state.hand.RemoveAt(state.hand.Count - 1);
            state.drawPile.Add(last);
        }

        while (state.hand.Count < targetCount)
        {
        if (state.drawPile.Count == 0 && state.discardPile.Count > 0)
        {
            state.drawPile.AddRange(state.discardPile);
            state.discardPile.Clear();
            ApplyBalancedDrawOrdering(state.drawPile);
        }

            if (state.drawPile.Count == 0) break;
            EnsureSubdeckCardIfNeeded(state);
            CardData card = state.drawPile[0];
            state.drawPile.RemoveAt(0);
            state.hand.Add(card);
        }
    }

    private static bool HandHasRegionCard(PlayerDeckState state)
    {
        for (int i = 0; i < state.hand.Count; i++)
        {
            if (state.hand[i] != null && state.hand[i].GetCardType() == CardTypeEnum.Land) return true;
        }
        return false;
    }

    private void EnsureRegionCardInHand(PlayerDeckState state)
    {
        if (state == null || HandHasRegionCard(state)) return;

        int landIndex = state.drawPile.FindIndex(c => c != null && c.GetCardType() == CardTypeEnum.Land);
        if (landIndex < 0)
        {
            int discardLandIndex = state.discardPile.FindIndex(c => c != null && c.GetCardType() == CardTypeEnum.Land);
            if (discardLandIndex < 0) return;
            CardData fromDiscard = state.discardPile[discardLandIndex];
            state.discardPile.RemoveAt(discardLandIndex);
            state.drawPile.Insert(0, fromDiscard);
            landIndex = 0;
        }

        CardData land = state.drawPile[landIndex];
        state.drawPile.RemoveAt(landIndex);

        if (state.hand.Count >= handSize)
        {
            int returnIndex = state.hand.FindLastIndex(c => c != null && c.GetCardType() != CardTypeEnum.Land);
            if (returnIndex < 0) returnIndex = state.hand.Count - 1;
            CardData returnCard = state.hand[returnIndex];
            state.hand.RemoveAt(returnIndex);
            state.drawPile.Insert(0, returnCard);
        }

        state.hand.Add(land);
    }

    private void ApplyRegionCardGuarantee(PlayerDeckState state)
    {
        if (state == null) return;
        if (HandHasRegionCard(state))
        {
            state.turnsWithoutRegionCard = 0;
        }
        else
        {
            state.turnsWithoutRegionCard++;
            if (state.turnsWithoutRegionCard >= 2)
            {
                EnsureRegionCardInHand(state);
                state.turnsWithoutRegionCard = 0;
            }
        }
    }

    private static bool HandHasSharedBaseCard(PlayerDeckState state)
    {
        if (state == null) return false;
        return state.hand.Any(c => c != null
            && string.Equals(c.deckId, "shared_base", StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureSharedBaseCardInHand(PlayerDeckState state)
    {
        if (state == null || HandHasSharedBaseCard(state)) return;

        int drawIndex = state.drawPile.FindIndex(c => c != null
            && string.Equals(c.deckId, "shared_base", StringComparison.OrdinalIgnoreCase));

        if (drawIndex < 0)
        {
            int discardIndex = state.discardPile.FindIndex(c => c != null
                && string.Equals(c.deckId, "shared_base", StringComparison.OrdinalIgnoreCase));
            if (discardIndex < 0) return;
            CardData fromDiscard = state.discardPile[discardIndex];
            state.discardPile.RemoveAt(discardIndex);
            state.drawPile.Insert(0, fromDiscard);
            drawIndex = 0;
        }

        CardData sharedCard = state.drawPile[drawIndex];
        state.drawPile.RemoveAt(drawIndex);

        if (state.hand.Count >= handSize)
        {
            // Make room by returning a non-shared-base card to the top of the draw pile.
            int returnIndex = state.hand.FindLastIndex(c => c != null
                && !string.Equals(c.deckId, "shared_base", StringComparison.OrdinalIgnoreCase));
            if (returnIndex < 0) returnIndex = state.hand.Count - 1;
            CardData returnCard = state.hand[returnIndex];
            state.hand.RemoveAt(returnIndex);
            state.drawPile.Insert(0, returnCard);
        }

        state.hand.Add(sharedCard);
    }

    private CardData FindActionCardForLeader(PlayableLeader leader, string actionClassName)
    {
        if (leader == null || string.IsNullOrWhiteSpace(actionClassName)) return null;

        string deckId = ResolveDeckIdForLeader(leader);
        foreach (DeckData deckData in GetDeckChain(deckId))
        {
            if (deckData?.cards == null) continue;
            CardData inLeaderDeck = deckData.cards.FirstOrDefault(card =>
                card != null
                && IsConsumableEffectCard(card)
                && string.Equals(card.GetActionRef(), actionClassName, StringComparison.OrdinalIgnoreCase));
            if (inLeaderDeck != null) return inLeaderDeck;
        }

        return cards.FirstOrDefault(card =>
            card != null
            && IsConsumableEffectCard(card)
            && string.Equals(card.GetActionRef(), actionClassName, StringComparison.OrdinalIgnoreCase));
    }

    public CardData FindCardByNameForLeader(PlayableLeader leader, string cardName)
    {
        if (leader == null || string.IsNullOrWhiteSpace(cardName)) return null;
        if (!loaded && !InitializeFromResources()) return null;

        string deckId = ResolveDeckIdForLeader(leader);
        foreach (DeckData deckData in GetDeckChain(deckId))
        {
            if (deckData?.cards == null) continue;
            CardData inLeaderDeck = deckData.cards.FirstOrDefault(card =>
                card != null
                && string.Equals(card.name, cardName, StringComparison.OrdinalIgnoreCase));
            if (inLeaderDeck != null) return inLeaderDeck;
        }

        return cards.FirstOrDefault(card =>
            card != null
            && string.Equals(card.name, cardName, StringComparison.OrdinalIgnoreCase));
    }

    public CardData FindArmyCardByName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return null;
        if (!loaded && !InitializeFromResources()) return null;
        return cards.FirstOrDefault(card =>
            card != null
            && card.GetCardType() == CardTypeEnum.Army
            && string.Equals(card.name, cardName, StringComparison.OrdinalIgnoreCase));
    }

    private static CardData CloneCard(CardData card)
    {
        if (card == null) return null;
        return new CardData
        {
            cardId = card.cardId,
            name = card.name,
            quote = card.quote,
            actionEffect = card.actionEffect,
            description = card.description,
            type = card.type,
            tags = card.tags != null ? new List<string>(card.tags) : new List<string>(),
            deckId = card.deckId,
            alignment = card.alignment,
            actionClassName = card.actionClassName,
            action = card.action,
            commanderSkillRequired = card.commanderSkillRequired,
            agentSkillRequired = card.agentSkillRequired,
            emissarySkillRequired = card.emissarySkillRequired,
            mageSkillRequired = card.mageSkillRequired,
            commander = card.commander,
            agent = card.agent,
            emmissary = card.emmissary,
            mage = card.mage,
            race = card.race,
            artifacts = card.artifacts != null ? new List<string>(card.artifacts) : new List<string>(),
            troopType = card.troopType,
            specialAbilities = card.specialAbilities != null ? new List<ArmySpecialAbilityEnum>(card.specialAbilities) : new List<ArmySpecialAbilityEnum>(),
            spriteName = card.spriteName,
            region = card.region,
            requirementsText = card.requirementsText,
            historyText = card.historyText,
            statusEffect = card.statusEffect,
            procChance = card.procChance,
            portraitName = card.portraitName,
            characterGroup = card.characterGroup,
            referenceDeckId = card.referenceDeckId,
            referenceCardId = card.referenceCardId,
            encounterOptions = card.encounterOptions != null ? CloneEncounterOptions(card.encounterOptions) : new List<EncounterOptionData>(),
            fleeOption = CloneEncounterOption(card.fleeOption),
            difficulty = card.difficulty,
            leatherRequired = card.leatherRequired,
            mountsRequired = card.mountsRequired,
            timberRequired = card.timberRequired,
            ironRequired = card.ironRequired,
            steelRequired = card.steelRequired,
            mithrilRequired = card.mithrilRequired,
            goldRequired = card.goldRequired,
            jokerRequired = card.jokerRequired,
            leatherGranted = card.leatherGranted,
            mountsGranted = card.mountsGranted,
            timberGranted = card.timberGranted,
            ironGranted = card.ironGranted,
            steelGranted = card.steelGranted,
            mithrilGranted = card.mithrilGranted,
            goldGranted = card.goldGranted,
            startingPC = card.startingPC,
            inspireEffectData = card.inspireEffectData,
            amount = card.amount,
            deckSpriteName = card.deckSpriteName
        };
    }

    private void InjectMissingStartingPcAndLandReferences()
    {
        // Build lookups from all non-reference cards
        Dictionary<string, CardData> pcByName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CardData> landByRegion = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CardData> allCardsByKey = new(StringComparer.OrdinalIgnoreCase);

        foreach (DeckData deck in loadedDecksById.Values)
        {
            if (deck?.cards == null) continue;
            foreach (CardData card in deck.cards)
            {
                if (card == null) continue;
                allCardsByKey[BuildCardReferenceKey(deck.deckId, card.cardId)] = card;
                if (IsReferenceCard(card)) continue;
                if (card.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(card.name))
                {
                    string key = NormalizeCardName(card.name);
                    if (!pcByName.ContainsKey(key)) pcByName[key] = card;
                }
                else if (card.GetCardType() == CardTypeEnum.Land && !string.IsNullOrWhiteSpace(card.region))
                {
                    string key = NormalizeCardName(card.region);
                    if (!landByRegion.ContainsKey(key)) landByRegion[key] = card;
                }
            }
        }

        foreach (DeckManifestEntry entry in deckManifestById.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.parentDeckId)) continue;
            if (!loadedDecksById.TryGetValue(entry.deckId, out DeckData subdeck) || subdeck?.cards == null) continue;

            HashSet<string> existingPcNames = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingLandRegions = new(StringComparer.OrdinalIgnoreCase);

            foreach (CardData card in subdeck.cards)
            {
                if (card == null) continue;
                CardData resolved = IsReferenceCard(card)
                    ? FindCardByKey(allCardsByKey, card.referenceDeckId, card.referenceCardId)
                    : card;
                if (resolved == null) continue;
                if (resolved.GetCardType() == CardTypeEnum.PC && !string.IsNullOrWhiteSpace(resolved.name))
                    existingPcNames.Add(NormalizeCardName(resolved.name));
                else if (resolved.GetCardType() == CardTypeEnum.Land && !string.IsNullOrWhiteSpace(resolved.region))
                    existingLandRegions.Add(NormalizeCardName(resolved.region));
            }

            int n = subdeck.cards.Count;
            for (int i = 0; i < n; i++)
            {
                CardData card = subdeck.cards[i];
                if (card == null) continue;

                CardData effective = IsReferenceCard(card)
                    ? FindCardByKey(allCardsByKey, card.referenceDeckId, card.referenceCardId)
                    : card;
                if (effective == null) continue;
                if (effective.GetCardType() != CardTypeEnum.Character) continue;
                if (string.IsNullOrWhiteSpace(effective.startingPC)) continue;

                string pcKey = NormalizeCardName(effective.startingPC);
                if (existingPcNames.Contains(pcKey)) continue;

                if (!pcByName.TryGetValue(pcKey, out CardData originalPc))
                {
                    Debug.LogWarning($"DeckManager: Character '{card.name}' in '{subdeck.deckId}' has startingPC '{card.startingPC}' but no matching PC card found.");
                    continue;
                }

                InjectReferenceCard(subdeck, originalPc);
                existingPcNames.Add(pcKey);

                if (string.IsNullOrWhiteSpace(originalPc.region)) continue;
                string regionKey = NormalizeCardName(originalPc.region);
                if (existingLandRegions.Contains(regionKey)) continue;
                if (!landByRegion.TryGetValue(regionKey, out CardData originalLand)) continue;

                InjectReferenceCard(subdeck, originalLand);
                existingLandRegions.Add(regionKey);
            }
        }
    }

    private static CardData FindCardByKey(Dictionary<string, CardData> index, string deckId, int cardId)
    {
        if (string.IsNullOrWhiteSpace(deckId) || cardId <= 0) return null;
        return index.TryGetValue(BuildCardReferenceKey(deckId, cardId), out CardData card) ? card : null;
    }

    private static void InjectReferenceCard(DeckData subdeck, CardData original)
    {
        if (subdeck?.cards == null || original == null) return;
        int maxId = subdeck.cards.Where(c => c != null).Select(c => c.cardId).DefaultIfEmpty(0).Max();
        subdeck.cards.Add(new CardData
        {
            cardId = maxId + 1,
            referenceDeckId = original.deckId,
            referenceCardId = original.cardId
        });
        Debug.Log($"DeckManager: Injected reference to '{original.name}' ({original.type}) from '{original.deckId}' into '{subdeck.deckId}'.");
    }

    private void ResolveCardReferences()
    {
        Dictionary<string, CardData> cardIndex = BuildCardIndex();
        Dictionary<string, CardData> resolvedTemplates = new(StringComparer.OrdinalIgnoreCase);

        foreach (DeckData deckData in loadedDecksById.Values)
        {
            if (deckData?.cards == null) continue;

            for (int i = 0; i < deckData.cards.Count; i++)
            {
                CardData card = deckData.cards[i];
                if (!IsReferenceCard(card)) continue;

                CardData template = ResolveReferencedTemplate(card.referenceDeckId, card.referenceCardId, cardIndex, resolvedTemplates, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (template == null)
                {
                    Debug.LogWarning($"DeckManager: Could not resolve reference for card '{card?.name}' in deck '{deckData.deckId}' -> {card.referenceDeckId}:{card.referenceCardId}.");
                    continue;
                }

                CardData resolvedCard = CloneCard(template);
                resolvedCard.cardId = card.cardId;
                resolvedCard.deckId = deckData.deckId;
                resolvedCard.alignment = deckData.alignment;
                resolvedCard.referenceDeckId = card.referenceDeckId;
                resolvedCard.referenceCardId = card.referenceCardId;
                deckData.cards[i] = resolvedCard;
            }
        }
    }

    private Dictionary<string, CardData> BuildCardIndex()
    {
        Dictionary<string, CardData> cardIndex = new(StringComparer.OrdinalIgnoreCase);

        foreach (DeckData deckData in loadedDecksById.Values)
        {
            if (deckData?.cards == null || string.IsNullOrWhiteSpace(deckData.deckId)) continue;

            foreach (CardData card in deckData.cards)
            {
                if (card == null) continue;
                cardIndex[BuildCardReferenceKey(deckData.deckId, card.cardId)] = card;
            }
        }

        return cardIndex;
    }

    private static CardData ResolveReferencedTemplate(
        string referenceDeckId,
        int referenceCardId,
        Dictionary<string, CardData> cardIndex,
        Dictionary<string, CardData> resolvedTemplates,
        HashSet<string> resolving)
    {
        if (string.IsNullOrWhiteSpace(referenceDeckId) || referenceCardId <= 0) return null;

        string referenceKey = BuildCardReferenceKey(referenceDeckId, referenceCardId);
        if (resolvedTemplates.TryGetValue(referenceKey, out CardData cachedTemplate))
        {
            return cachedTemplate;
        }

        if (!cardIndex.TryGetValue(referenceKey, out CardData sourceCard) || sourceCard == null)
        {
            return null;
        }

        if (!IsReferenceCard(sourceCard))
        {
            CardData directTemplate = CloneCard(sourceCard);
            resolvedTemplates[referenceKey] = directTemplate;
            return directTemplate;
        }

        if (!resolving.Add(referenceKey))
        {
            return null;
        }

        CardData nestedTemplate = ResolveReferencedTemplate(sourceCard.referenceDeckId, sourceCard.referenceCardId, cardIndex, resolvedTemplates, resolving);
        resolving.Remove(referenceKey);

        if (nestedTemplate == null) return null;

        CardData resolvedTemplate = CloneCard(nestedTemplate);
        resolvedTemplates[referenceKey] = resolvedTemplate;
        return resolvedTemplate;
    }

    private static bool IsReferenceCard(CardData card)
    {
        return card != null && !string.IsNullOrWhiteSpace(card.referenceDeckId) && card.referenceCardId > 0;
    }

    private static string BuildCardReferenceKey(string deckId, int cardId)
    {
        return $"{deckId?.Trim().ToLowerInvariant()}::{cardId}";
    }

    private static int FindMatchingActionCardIndex(List<CardData> cardsList, string actionClassName, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        if (cardsList == null || cardsList.Count == 0) return -1;
        for (int i = 0; i < cardsList.Count; i++)
        {
            CardData card = cardsList[i];
            if (card == null) continue;
            if (!IsConsumableEffectCard(card)) continue;

            bool matches = false;
            if (!string.IsNullOrWhiteSpace(actionClassName) &&
                string.Equals(card.GetActionRef(), actionClassName, StringComparison.OrdinalIgnoreCase))
            {
                matches = true;
            }

            if (!matches) continue;

            if (selectedCharacter == null || card.EvaluatePlayability(selectedCharacter, resourceCheck, conditionCheck))
            {
                return i;
            }
        }
        return -1;
    }

    private static List<EncounterOptionData> CloneEncounterOptions(List<EncounterOptionData> options)
    {
        if (options == null) return new List<EncounterOptionData>();
        return options.Select(CloneEncounterOption).Where(option => option != null).ToList();
    }

    private static EncounterOptionData CloneEncounterOption(EncounterOptionData option)
    {
        if (option == null) return null;
        return new EncounterOptionData
        {
            optionId = option.optionId,
            label = option.label,
            description = option.description,
            outcomes = option.outcomes != null
                ? option.outcomes.Select(CloneEncounterOutcome).Where(outcome => outcome != null).ToList()
                : new List<EncounterOutcomeData>()
        };
    }

    private static EncounterOutcomeData CloneEncounterOutcome(EncounterOutcomeData outcome)
    {
        if (outcome == null) return null;
        return new EncounterOutcomeData
        {
            outcomeId = outcome.outcomeId,
            resultText = outcome.resultText,
            requiredAlignment = outcome.requiredAlignment,
            minCommander = outcome.minCommander,
            minAgent = outcome.minAgent,
            minEmmissary = outcome.minEmmissary,
            minMage = outcome.minMage,
            minHealth = outcome.minHealth,
            maxHealth = outcome.maxHealth,
            healthDelta = outcome.healthDelta,
            goldDelta = outcome.goldDelta,
            leatherDelta = outcome.leatherDelta,
            timberDelta = outcome.timberDelta,
            mountsDelta = outcome.mountsDelta,
            ironDelta = outcome.ironDelta,
            steelDelta = outcome.steelDelta,
            mithrilDelta = outcome.mithrilDelta,
            statuses = outcome.statuses != null
                ? outcome.statuses.Select(status => new EncounterStatusEffectData
                {
                    statusId = status.statusId,
                    turns = status.turns
                }).ToList()
                : new List<EncounterStatusEffectData>()
        };
    }

    private static bool MatchesActionCard(CardData card, string actionClassName)
    {
        if (!IsConsumableEffectCard(card)) return false;

        return !string.IsNullOrWhiteSpace(actionClassName)
            && string.Equals(card.GetActionRef(), actionClassName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConsumableEffectCard(CardData card)
    {
        if (card == null) return false;

        CardTypeEnum cardType = card.GetCardType();
        bool supportedType = cardType == CardTypeEnum.Action
            || cardType == CardTypeEnum.Event
            || cardType == CardTypeEnum.Encounter
            || cardType == CardTypeEnum.Land
            || cardType == CardTypeEnum.PC;
        if (!supportedType) return false;

        return !string.IsNullOrWhiteSpace(card.GetActionRef());
    }

    private List<Hex> RevealRegion(Board board, string region, Leader owner, int maxHexes = -1)
    {
        List<Hex> candidates = new();
        if (board == null || string.IsNullOrWhiteSpace(region)) return candidates;

        string normalizedRegion = NormalizeCardName(region);
        foreach (Hex hex in board.hexes.Values)
        {
            if (hex == null) continue;

            string hexRegion = hex.GetLandRegion();
            if (string.IsNullOrWhiteSpace(hexRegion))
            {
                PC pc = hex.GetPCData();
                if (pc != null) hexRegion = ResolveRegionForPc(pc);
            }

            if (string.IsNullOrWhiteSpace(hexRegion)) continue;
            if (!string.Equals(NormalizeCardName(hexRegion), normalizedRegion, StringComparison.Ordinal)) continue;

            candidates.Add(hex);
        }

        if (maxHexes > 0 && candidates.Count > maxHexes)
        {
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            candidates = candidates.Take(maxHexes).ToList();
        }

        foreach (Hex hex in candidates) hex.RevealMapOnlyArea(1, false, false);
        return candidates;
    }

    private List<Hex> RevealPcOnMapOnly(Board board, string pcName)
    {
        List<Hex> revealedHexes = new();
        if (board == null || string.IsNullOrWhiteSpace(pcName)) return revealedHexes;

        string normalizedPcName = NormalizeCardName(pcName);
        foreach (Hex hex in board.hexes.Values)
        {
            PC pc = hex?.GetPCData();
            if (pc == null) continue;
            if (!string.Equals(NormalizeCardName(pc.pcName), normalizedPcName, StringComparison.Ordinal)) continue;

            hex.RevealMapOnlyArea(1, false, false);
            revealedHexes.Add(hex);
            return revealedHexes;
        }

        return revealedHexes;
    }

    public string ResolveRegionForPc(PC pc)
    {
        if (pc == null) return null;

        LeaderBiomeConfig ownerBiome = pc.owner != null ? pc.owner.GetBiome() : null;
        if (ownerBiome != null
            && !string.IsNullOrWhiteSpace(ownerBiome.startingCityName)
            && string.Equals(NormalizeCardName(ownerBiome.startingCityName), NormalizeCardName(pc.pcName), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(ownerBiome.startingCityRegion))
        {
            return ownerBiome.startingCityRegion;
        }

        string normalizedPcName = NormalizeCardName(pc.pcName);
        CardData pcCard = cards.FirstOrDefault(card =>
            card != null
            && card.GetCardType() == CardTypeEnum.PC
            && !string.IsNullOrWhiteSpace(card.region)
            && string.Equals(NormalizeCardName(card.name), normalizedPcName, StringComparison.Ordinal));

        return pcCard?.region;
    }

    private static string NormalizeCardName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return string.Empty;
        return new string(cardName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static void NotifyRegionDiscovered(string regionName, Hex anchorHex)
    {
        if (string.IsNullOrWhiteSpace(regionName)) return;

        FindFirstObjectByType<RegionLabelManager>()?.ShowLabel(regionName.Trim());

        EventIconsManager iconsManager = EventIconsManager.FindManager();
        BoardNavigator navigator = BoardNavigator.Instance != null ? BoardNavigator.Instance : FindFirstObjectByType<BoardNavigator>();
        string displayName = FormatDisplayName(regionName);
        string text = $"{displayName} discovered!";
        Action showMessage = () => MessageDisplay.ShowMessage(text, Color.cyan, true);

        if (iconsManager != null)
        {
            iconsManager.AddEventIcon(EventIconType.Discovery, true, () =>
            {
                if (navigator != null && anchorHex != null)
                    navigator.EnqueueFocus(anchorHex, 0.5f, 0.18f, true, showMessage);
                else
                    showMessage();
            });
        }
        else
        {
            showMessage();
        }
    }

    private static void QueueRevealMessages(List<Hex> revealedPcHexes, string message)
    {
        if (revealedPcHexes == null || revealedPcHexes.Count == 0) return;

        EventIconsManager iconsManager = EventIconsManager.FindManager();
        BoardNavigator navigator = BoardNavigator.Instance != null ? BoardNavigator.Instance : FindFirstObjectByType<BoardNavigator>();
        Hex anchorHex = ChooseFocusHex(revealedPcHexes);
        if (anchorHex == null) return;

        string revealText = string.IsNullOrWhiteSpace(message) ? "The lands were revealed" : message;

        Action showRevealMessage = () => MessageDisplay.ShowMessage(revealText, Color.yellow, true);
        if (iconsManager != null)
        {
            iconsManager.AddEventIcon(
                EventIconType.HexMessage,
                true,
                () =>
                {
                    if (navigator != null)
                    {
                        navigator.EnqueueFocus(anchorHex, 0.5f, 0.18f, true, showRevealMessage);
                    }
                    else
                    {
                        showRevealMessage();
                    }
                });
        }
        else if (navigator != null)
        {
            navigator.EnqueueFocus(anchorHex, 0.5f, 0.18f, true, showRevealMessage);
        }
        else
        {
            showRevealMessage();
        }
    }

    private static string FormatDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string spaced = Regex.Replace(value.Trim(), @"(?<!^)([A-Z])", " $1");
        return Regex.Replace(spaced, @"\s+", " ").Trim();
    }

    private static Hex ChooseFocusHex(List<Hex> hexes)
    {
        if (hexes == null || hexes.Count == 0) return null;

        List<Hex> validHexes = hexes.Where(hex => hex != null).ToList();
        if (validHexes.Count == 0) return null;
        if (validHexes.Count == 1) return validHexes[0];

        float averageX = (float)validHexes.Average(hex => hex.v2.x);
        float averageY = (float)validHexes.Average(hex => hex.v2.y);

        Hex bestHex = validHexes[0];
        float bestDistance = float.MaxValue;
        for (int i = 0; i < validHexes.Count; i++)
        {
            Hex hex = validHexes[i];
            float dx = hex.v2.x - averageX;
            float dy = hex.v2.y - averageY;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHex = hex;
            }
        }

        return bestHex;
    }

    private static void ApplyCardCosts(Leader owner, CardData card)
    {
        if (owner == null || card == null) return;

        if (card.leatherRequired > 0) owner.RemoveLeather(card.leatherRequired, false);
        if (card.timberRequired > 0) owner.RemoveTimber(card.timberRequired, false);
        if (card.mountsRequired > 0) owner.RemoveMounts(card.mountsRequired, false);
        if (card.ironRequired > 0) owner.RemoveIron(card.ironRequired, false);
        if (card.steelRequired > 0) owner.RemoveSteel(card.steelRequired, false);
        if (card.mithrilRequired > 0) owner.RemoveMithril(card.mithrilRequired, false);
        int totalGoldCost = card.GetTotalGoldCost();
        if (totalGoldCost > 0) owner.RemoveGold(totalGoldCost, false);
    }

    private PlayerDeckState BuildDeckStateForLeader(PlayableLeader leader)
    {
        string deckId = ResolveDeckIdForLeader(leader);
        if (string.IsNullOrWhiteSpace(deckId)) return null;

        bool isVariantSelection = leader != null && !string.IsNullOrWhiteSpace(leader.GetSelectedVariantName());
        PlayerDeckState state = new PlayerDeckState
        {
            deckId = deckId
        };

        List<CardData> basePool = new();
        List<CardData> subdeckPool = new();

        if (isVariantSelection)
        {
            List<DeckData> ownedChain = GetDeckChain(deckId).ToList();
            if (ownedChain.Count == 0) return null;

            DeckData leafDeck = ownedChain[ownedChain.Count - 1];
            List<DeckData> ancestorDecks = ownedChain.Take(Mathf.Max(0, ownedChain.Count - 1)).ToList();

            foreach (DeckData ownedDeck in ancestorDecks)
            {
                if (ownedDeck?.cards == null) continue;
                basePool.AddRange(
                    ownedDeck.cards
                        .Where(card => ShouldIncludeCardInDeck(state.deckId, ownedDeck.deckId, card))
                        .SelectMany(card => Enumerable.Repeat(card, Math.Max(1, card.amount)).Select(CloneCard))
                        .Where(card => card != null));
            }

            if (leafDeck?.cards != null)
            {
                subdeckPool.AddRange(
                    leafDeck.cards
                        .Where(card => ShouldIncludeCardInDeck(state.deckId, leafDeck.deckId, card))
                        .SelectMany(card => Enumerable.Repeat(card, Math.Max(1, card.amount)).Select(CloneCard))
                        .Where(card => card != null));
            }

            AddMergedPool(state.drawPile, basePool, subdeckPool);
        }
        else
        {
            List<DeckData> expandedDecks = GetDeckTree(deckId).ToList();
            if (expandedDecks.Count == 0) return null;

            DeckData baseDeck = expandedDecks[0];
            List<DeckData> descendantDecks = expandedDecks.Skip(1).ToList();

            if (baseDeck?.cards != null)
            {
                basePool.AddRange(
                    baseDeck.cards
                        .Where(card => ShouldIncludeCardInDeck(state.deckId, baseDeck.deckId, card))
                        .SelectMany(card => Enumerable.Repeat(card, Math.Max(1, card.amount)).Select(CloneCard))
                        .Where(card => card != null));
            }

            foreach (DeckData descendantDeck in descendantDecks)
            {
                if (descendantDeck?.cards == null) continue;
                subdeckPool.AddRange(
                    descendantDeck.cards
                        .Where(card => ShouldIncludeCardInDeck(state.deckId, descendantDeck.deckId, card))
                        .SelectMany(card => Enumerable.Repeat(card, Math.Max(1, card.amount)).Select(CloneCard))
                        .Where(card => card != null));
            }

            AddMergedPool(state.drawPile, basePool, subdeckPool);
        }

        List<CardData> sharedPool = new();
        foreach (DeckData sharedDeck in GetSharedDecks())
        {
            if (sharedDeck?.cards == null) continue;
            sharedPool.AddRange(
                sharedDeck.cards
                    .Where(card => ShouldIncludeCardInDeck(state.deckId, sharedDeck.deckId, card))
                    .SelectMany(card => Enumerable.Repeat(card, Math.Max(1, card.amount)).Select(CloneCard))
                    .Where(card => card != null));
        }
        Shuffle(sharedPool);
        foreach (CardData sharedCard in sharedPool)
        {
            int insertIndex = UnityEngine.Random.Range(0, state.drawPile.Count + 1);
            state.drawPile.Insert(insertIndex, sharedCard);
        }

        ApplyBalancedDrawOrdering(state.drawPile);
        DrawUpToHandSize(state);
        return state;
    }

    private static void AddMergedPool(List<CardData> destination, List<CardData> basePool, List<CardData> leafPool)
    {
        if (destination == null) return;

        List<CardData> merged = new List<CardData>();
        if (basePool != null) merged.AddRange(basePool);
        if (leafPool != null) merged.AddRange(leafPool);
        Shuffle(merged);
        destination.AddRange(merged);
    }

    private void EnsureSubdeckCardIfNeeded(PlayerDeckState state)
    {
        if (state == null || state.drawPile.Count == 0) return;
        if (string.IsNullOrWhiteSpace(state.deckId)) return;
        if (!deckManifestById.TryGetValue(state.deckId, out DeckManifestEntry entry)) return;
        if (string.IsNullOrWhiteSpace(entry.parentDeckId)) return;

        bool hasSubdeckCard = state.hand.Any(c => c != null
            && string.Equals(c.deckId, state.deckId, StringComparison.OrdinalIgnoreCase));
        if (hasSubdeckCard) return;

        for (int i = 1; i < state.drawPile.Count; i++)
        {
            CardData candidate = state.drawPile[i];
            if (candidate == null) continue;
            if (!string.Equals(candidate.deckId, state.deckId, StringComparison.OrdinalIgnoreCase)) continue;
            state.drawPile.RemoveAt(i);
            state.drawPile.Insert(0, candidate);
            return;
        }
    }

    private static CardData TakeRandomCard(List<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return null;
        int index = UnityEngine.Random.Range(0, cards.Count);
        CardData card = cards[index];
        cards.RemoveAt(index);
        return card;
    }

    private static void ApplyBalancedDrawOrdering(List<CardData> cards)
    {
        if (cards == null || cards.Count < 2) return;

        List<CardData> shuffled = new(cards);
        Shuffle(shuffled);

        Dictionary<BalancedDeckBucket, List<CardData>> buckets = new();
        foreach (BalancedDeckBucket bucket in Enum.GetValues(typeof(BalancedDeckBucket)))
        {
            buckets[bucket] = new List<CardData>();
        }

        foreach (CardData card in shuffled)
        {
            buckets[GetBalancedDeckBucket(card)].Add(card);
        }

        foreach (List<CardData> bucketCards in buckets.Values)
        {
            Shuffle(bucketCards);
        }

        List<CardData> ordered = new(cards.Count);
        while (HasBalancedTargetCards(buckets))
        {
            List<BalancedDeckBucket> cycleSlots = new(BalancedDrawPattern);
            Shuffle(cycleSlots);

            foreach (BalancedDeckBucket preferredBucket in cycleSlots)
            {
                CardData next = TakeBalancedCard(buckets, preferredBucket);
                if (next == null) break;
                ordered.Add(next);
            }
        }

        List<CardData> miscCards = buckets[BalancedDeckBucket.Misc];
        for (int i = 0; i < miscCards.Count; i++)
        {
            int insertIndex = UnityEngine.Random.Range(0, ordered.Count + 1);
            ordered.Insert(insertIndex, miscCards[i]);
        }

        cards.Clear();
        cards.AddRange(ordered);
    }

    private static bool HasBalancedTargetCards(Dictionary<BalancedDeckBucket, List<CardData>> buckets)
    {
        foreach (BalancedDeckBucket bucket in BalancedDrawPattern)
        {
            if (buckets.TryGetValue(bucket, out List<CardData> cards) && cards.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static BalancedDeckBucket GetBalancedDeckBucket(CardData card)
    {
        if (card == null) return BalancedDeckBucket.Misc;

        return card.GetCardType() switch
        {
            CardTypeEnum.Army => BalancedDeckBucket.Army,
            CardTypeEnum.Event => BalancedDeckBucket.Event,
            CardTypeEnum.PC => BalancedDeckBucket.PC,
            CardTypeEnum.Land => BalancedDeckBucket.Land,
            CardTypeEnum.Encounter => BalancedDeckBucket.Encounter,
            CardTypeEnum.Character => BalancedDeckBucket.Character,
            CardTypeEnum.Action => BalancedDeckBucket.ActionSpell,
            CardTypeEnum.Spell => BalancedDeckBucket.ActionSpell,
            _ => BalancedDeckBucket.Misc
        };
    }

    private static CardData TakeBalancedCard(Dictionary<BalancedDeckBucket, List<CardData>> buckets, BalancedDeckBucket preferredBucket)
    {
        if (buckets.TryGetValue(preferredBucket, out List<CardData> preferredCards) && preferredCards.Count > 0)
        {
            return TakeRandomCard(preferredCards);
        }

        BalancedDeckBucket? fallbackBucket = null;
        int fallbackCount = 0;
        foreach (BalancedDeckBucket bucket in BalancedDrawPattern.Distinct())
        {
            if (bucket == preferredBucket) continue;
            if (!buckets.TryGetValue(bucket, out List<CardData> cards) || cards.Count <= 0) continue;
            if (cards.Count > fallbackCount)
            {
                fallbackBucket = bucket;
                fallbackCount = cards.Count;
            }
        }

        if (fallbackBucket.HasValue && buckets.TryGetValue(fallbackBucket.Value, out List<CardData> fallbackCards) && fallbackCards.Count > 0)
        {
            return TakeRandomCard(fallbackCards);
        }

        if (buckets.TryGetValue(BalancedDeckBucket.Misc, out List<CardData> miscCards) && miscCards.Count > 0)
        {
            return TakeRandomCard(miscCards);
        }

        return null;
    }

    private IEnumerable<DeckData> GetDeckTree(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId)) yield break;

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> queue = new();
        queue.Enqueue(deckId);

        while (queue.Count > 0)
        {
            string currentDeckId = queue.Dequeue();
            if (string.IsNullOrWhiteSpace(currentDeckId) || !visited.Add(currentDeckId)) continue;
            if (!loadedDecksById.TryGetValue(currentDeckId, out DeckData deckData) || deckData == null) continue;
            yield return deckData;

            foreach (DeckManifestEntry entry in deckManifestById.Values)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.parentDeckId)) continue;
                if (!string.Equals(entry.parentDeckId, currentDeckId, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(entry.deckId)) continue;
                if (entry.sharedToAll) continue;
                queue.Enqueue(entry.deckId);
            }
        }
    }

    private static bool ShouldIncludeCardInDeck(string ownerDeckId, string sourceDeckId, CardData card)
    {
        if (card == null) return false;

        // Encounters must come only from EncounterDeck.json.
        // Shared/base modular decks may contain legacy duplicate encounter definitions,
        // but they are not the source of truth for live encounter gameplay.
        if (card.IsEncounterCard())
        {
            return string.Equals(sourceDeckId, "encounter_shared", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private IEnumerable<DeckData> GetSharedDecks()
    {
        foreach (DeckManifestEntry entry in deckManifestById.Values)
        {
            if (entry == null || !entry.sharedToAll) continue;
            if (string.IsNullOrWhiteSpace(entry.deckId)) continue;
            if (loadedDecksById.TryGetValue(entry.deckId, out DeckData deckData) && deckData != null)
            {
                yield return deckData;
            }
        }
    }

    private IEnumerable<DeckData> GetDeckChain(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId)) yield break;

        Stack<DeckData> chain = new();
        string currentDeckId = deckId;
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(currentDeckId)
            && visited.Add(currentDeckId)
            && deckManifestById.TryGetValue(currentDeckId, out DeckManifestEntry entry))
        {
            if (loadedDecksById.TryGetValue(currentDeckId, out DeckData deckData) && deckData != null)
            {
                chain.Push(deckData);
            }

            currentDeckId = entry.parentDeckId;
        }

        while (chain.Count > 0)
        {
            yield return chain.Pop();
        }
    }

    private string ResolveDeckIdForLeader(PlayableLeader leader)
    {
        if (leader == null) return null;

        string selectedSubdeckId = leader.GetSelectedSubdeckId();
        if (!string.IsNullOrWhiteSpace(selectedSubdeckId)
            && deckManifestById.ContainsKey(selectedSubdeckId))
        {
            return selectedSubdeckId;
        }

        DeckManifestEntry byNation = deckManifestById.Values.FirstOrDefault(x =>
            !x.sharedToAll &&
            !x.isBaseDeck &&
            !string.IsNullOrWhiteSpace(x?.nation) &&
            string.Equals(x.nation, leader.characterName, StringComparison.OrdinalIgnoreCase));
        if (byNation != null) return byNation.deckId;

        int alignment = (int)leader.alignment;
        DeckManifestEntry byAlignment = deckManifestById.Values.FirstOrDefault(x =>
            x != null
            && !x.sharedToAll
            && x.isBaseDeck
            && x.alignment == alignment);
        return byAlignment?.deckId;
    }

    private void DrawUpToHandSize(PlayerDeckState state)
    {
        if (state == null) return;
        while (state.hand.Count < handSize && state.drawPile.Count > 0)
        {
            EnsureSubdeckCardIfNeeded(state);
            CardData card = state.drawPile[0];
            state.drawPile.RemoveAt(0);
            state.hand.Add(card);
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        if (list == null) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void RefreshInspectorDecks()
    {
        inspectorDecks = loadedDecksById.Values
            .OrderBy(x => x.nation)
            .ThenBy(x => x.deckId)
            .ToList();
    }

    private void RefreshHumanPlayerHandUIIfHuman(PlayableLeader leader)
    {
        if (leader == null) return;
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null || leader != game.player) return;
        RefreshHumanPlayerHandUI();
    }

    private void EnsureTutorialHandForPlayer(PlayableLeader leader)
    {
        if (leader == null) return;

        TutorialManager tutorial = TutorialManager.Instance;
        if (tutorial == null || !tutorial.IsActiveFor(leader)) return;

        List<string> requiredCardNames = tutorial.GetCurrentExpectedCardNames(leader);

        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state))
        {
            Debug.Log($"[TutorialDebug] Rebuild tutorial hand for '{leader.characterName}': no existing state, expected=[{string.Join(", ", requiredCardNames)}]");
            SetTutorialCardsByName(leader, requiredCardNames);
            return;
        }

        bool sameCount = state.hand.Count == requiredCardNames.Count;
        bool allPresent = requiredCardNames.All(required =>
        {
            CardData expectedCard = FindCardByNameForLeader(leader, required);
            if (expectedCard == null) return false;

            return state.hand.Any(card =>
                card != null
                && string.Equals(card.name, required, StringComparison.OrdinalIgnoreCase));
        });

        if (!sameCount || !allPresent)
        {
            string currentHand = string.Join(", ", state.hand.Where(card => card != null).Select(card => card.name));
            Debug.Log($"[TutorialDebug] Rebuild tutorial hand for '{leader.characterName}': hand=[{currentHand}] expected=[{string.Join(", ", requiredCardNames)}]");
            ApplyTutorialCardsToStateByName(leader, requiredCardNames);
        }
    }

    private bool ShouldSkipTurnRefillForTutorialHuman(PlayableLeader leader)
    {
        if (leader == null) return false;
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player == null || leader != game.player) return false;
        TutorialManager tutorial = TutorialManager.Instance;
        return tutorial != null && tutorial.IsActiveFor(leader);
    }

    private bool ApplyTutorialActionCardsToState(PlayableLeader leader, IEnumerable<string> actionClassNames)
    {
        if (leader == null) return false;

        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state))
        {
            state = BuildDeckStateForLeader(leader);
            if (state == null) return false;
            playerDecks[leader] = state;
        }

        List<string> required = actionClassNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        state.hand.Clear();
        state.drawPile.Clear();
        state.discardPile.Clear();

        foreach (string actionClass in required)
        {
            CardData card = FindActionCardForLeader(leader, actionClass);
            if (card == null)
            {
                Debug.LogWarning($"DeckManager: Could not find tutorial card for action '{actionClass}' and leader '{leader.characterName}'.");
                continue;
            }
            state.hand.Add(CloneCard(card));
        }

        return true;
    }

    private bool ApplyTutorialCardsToStateByName(PlayableLeader leader, IEnumerable<string> cardNames)
    {
        if (leader == null) return false;

        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state))
        {
            state = BuildDeckStateForLeader(leader);
            if (state == null) return false;
            playerDecks[leader] = state;
        }

        List<string> required = cardNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        state.hand.Clear();
        state.drawPile.Clear();
        state.discardPile.Clear();

        foreach (string cardName in required)
        {
            CardData card = FindCardByNameForLeader(leader, cardName);
            if (card == null)
            {
                Debug.LogWarning($"DeckManager: Could not find tutorial card '{cardName}' for leader '{leader.characterName}'.");
                continue;
            }
            state.hand.Add(CloneCard(card));
        }

        return true;
    }

    private IEnumerator PlayReshuffleAnimation(List<GameObject> oldCards)
    {
        if (gridLayout == null) yield break;

        // Force layout pass so rt.position values are current before we capture them.
        Canvas.ForceUpdateCanvases();

        Transform floatRoot = gridLayout.transform.parent;
        int total = oldCards.Count;

        // Capture all world positions in one pass before touching any parent.
        Vector3[] worldPositions = new Vector3[total];
        for (int i = 0; i < total; i++)
        {
            if (oldCards[i] == null) continue;
            if (oldCards[i].TryGetComponent(out RectTransform rt))
                worldPositions[i] = rt.position;
        }

        // Disable GridLayoutGroup so reparenting one card at a time doesn't
        // cause the remaining siblings to jump around between iterations.
        gridLayout.enabled = false;

        for (int i = 0; i < total; i++)
        {
            GameObject card = oldCards[i];
            if (card == null) continue;

            RectTransform rt = card.GetComponent<RectTransform>();
            if (rt == null) continue;

            card.transform.SetParent(floatRoot, false);
            rt.position = worldPositions[i];

            LayoutElement le = card.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;

            StartCoroutine(ScatterCard(card, i, total));
        }

        gridLayout.enabled = true;

        yield return new WaitForSecondsRealtime(0.5f);

        foreach (var go in oldCards)
            if (go != null) Destroy(go);

        RefreshHumanPlayerHandUI();

        // Hide all new cards synchronously before any frame renders them,
        // so there is no flash at their grid positions.
        List<GameObject> newCards = new List<GameObject>(handCardInstances);
        foreach (GameObject card in newCards)
        {
            if (card == null) continue;
            RectTransform rt = card.GetComponent<RectTransform>();
            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (rt != null) rt.localScale = Vector3.zero;
            if (cg != null) cg.alpha = 0f;
        }

        for (int i = 0; i < newCards.Count; i++)
            StartCoroutine(DealInCard(newCards[i], i * 0.07f));
    }

    private IEnumerator ScatterCard(GameObject card, int index, int total)
    {
        if (card == null) yield break;

        RectTransform rt = card.GetComponent<RectTransform>();
        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (rt == null) yield break;

        float t0 = total > 1 ? (float)index / (total - 1) : 0.5f;
        float baseAngle = Mathf.Lerp(-65f, 65f, t0);
        float spin = UnityEngine.Random.Range(-20f, 20f);
        float rad = (baseAngle + spin) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), -Mathf.Abs(Mathf.Cos(rad)) - 0.3f).normalized;

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + dir * UnityEngine.Random.Range(380f, 520f);
        float startRot = rt.localEulerAngles.z;
        float endRot = startRot + UnityEngine.Random.Range(-30f, 30f);
        Vector3 startScale = rt.localScale;

        float delay = index * 0.025f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (card == null) yield break;
            float p = elapsed / duration;
            float eased = p * p;

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startRot, endRot, p));
            rt.localScale = Vector3.Lerp(startScale, startScale * 0.3f, eased);
            if (cg != null) cg.alpha = 1f - p;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator DealInCard(GameObject card, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (card == null) yield break;

        RectTransform rt = card.GetComponent<RectTransform>();
        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (rt == null) yield break;

        Vector3 targetScale = rt.localScale;
        rt.localScale = Vector3.zero;
        if (cg != null) cg.alpha = 0f;

        float duration = 0.26f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (card == null) yield break;
            float p = elapsed / duration;
            float eased = 1f + 2.70158f * Mathf.Pow(p - 1f, 3f) + 1.70158f * Mathf.Pow(p - 1f, 2f);
            rt.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, eased);
            if (cg != null) cg.alpha = Mathf.Clamp01(p * 2.5f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (card != null)
        {
            rt.localScale = targetScale;
            if (cg != null) cg.alpha = 1f;
        }
    }

    private void ClearHandCardInstances()
    {
        foreach (GameObject card in handCardInstances)
        {
            if (card == null) continue;
            Destroy(card);
        }
        handCardInstances.Clear();

        if (gridLayout == null) return;

        for (int i = gridLayout.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = gridLayout.transform.GetChild(i);
            if (child == null) continue;
            GameObject childGo = child.gameObject;
            if (childGo == null) continue;
            if (cardCameObject != null && childGo == cardCameObject) continue;
            if (childGo.GetComponent<Card>() == null) continue;
            Destroy(childGo);
        }
    }

    private GameObject ResolveCardPrefab()
    {
        if (cardCameObject != null)
        {
            if (cardCameObject.activeSelf)
            {
                cardCameObject.SetActive(false);
            }
            return cardCameObject;
        }
        if (gridLayout == null) return null;

        Card existingCard = gridLayout.GetComponentInChildren<Card>(true);
        if (existingCard == null) return null;

        cardCameObject = existingCard.gameObject;
        if (cardCameObject != null)
        {
            cardCameObject.SetActive(false);
        }

        return cardCameObject;
    }

    private CanvasGroup ResolveHandCanvasGroup()
    {
        if (handCanvasGroup != null) return handCanvasGroup;

        if (gridLayout != null)
        {
            handCanvasGroup = gridLayout.GetComponent<CanvasGroup>();
            if (handCanvasGroup != null) return handCanvasGroup;

            handCanvasGroup = gridLayout.GetComponentInParent<CanvasGroup>();
        }

        if (handCanvasGroup == null)
        {
            handCanvasGroup = GetComponent<CanvasGroup>();
        }

        return handCanvasGroup;
    }
}
