using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[Serializable]
public class CardsManifest
{
    public int handSize = 5;
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
}

[Serializable]
public class DeckData
{
    public int handSize = 5;
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
    public string cardHistoryReason;

    public void Reset()
    {
        isPlayable = false;
        failsLevelRequirements = false;
        failsResourceRequirements = false;
        failsActionConditions = false;
        failsCardHistoryRequirements = false;
        cardHistoryReason = null;
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
    public string description;
    public string type;
    public List<string> tags = new();
    public string deckId;
    public int alignment;
    public string actionClassName;
    public string action;
    public int actionId;
    public string spriteName;
    public string region;
    public string requirementsText;
    public string pcEffectId;
    public string historyText;
    public string portraitName;
    public List<EncounterOptionData> encounterOptions = new();
    public EncounterOptionData fleeOption;
    public int commander;
    public int agent;
    public int emmissary;
    public int mage;
    public RacesEnum race;
    public List<Artifact> artifacts = new();
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
            isPlayable = cardResourcesOk && cardConditionsOk;
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

        if (GetCardType() != CardTypeEnum.PC) return true;
        if (string.IsNullOrWhiteSpace(region)) return true;
        if (playableLeader.HasPlayedLandCardForRegion(region)) return true;

        reason = $"Play the Land card for {region} first.";
        return false;
    }
}

public class DeckManager : MonoBehaviour
{
    private class PlayerDeckState
    {
        public string deckId;
        public readonly List<CardData> drawPile = new();
        public readonly List<CardData> hand = new();
        public readonly List<CardData> discardPile = new();
    }

    public static DeckManager Instance { get; private set; }

    [Header("References")]
    [FormerlySerializedAs("cardCameObject")]
    [SerializeField] GameObject cardCameObject;
    [SerializeField] GridLayoutGroup gridLayout;

    [Header("Config")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private string cardsManifestResourcePath = "Cards";
    [SerializeField] private int fallbackHandSize = 5;

    [Header("Debug")]
    [SerializeField] private bool logInitialization = true;

    public List<CardData> cards = new();

    [Header("Inspector (Runtime)")]
    [SerializeField] private List<DeckData> inspectorDecks = new();

    private readonly Dictionary<string, DeckManifestEntry> deckManifestById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeckData> loadedDecksById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionDefinition> actionDefinitionsByClass = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ActionDefinition> actionDefinitionsById = new();
    private readonly Dictionary<PlayableLeader, PlayerDeckState> playerDecks = new();
    private readonly List<GameObject> handCardInstances = new();

    private int handSize = 5;
    private bool loaded;
    private bool isRefreshingHumanHandUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!initializeOnStart) return;

        InitializeFromResources();
        InitializeHandsForCurrentGame();
    }

    public bool InitializeFromResources()
    {
        loaded = false;
        cards.Clear();
        inspectorDecks.Clear();
        deckManifestById.Clear();
        loadedDecksById.Clear();
        actionDefinitionsByClass.Clear();
        actionDefinitionsById.Clear();
        playerDecks.Clear();

        TextAsset manifestAsset = Resources.Load<TextAsset>(cardsManifestResourcePath);
        if (manifestAsset == null)
        {
            Debug.LogWarning($"DeckManager: Could not load cards manifest from Resources/{cardsManifestResourcePath}.json");
            handSize = fallbackHandSize;
            return false;
        }

        CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (manifest == null || manifest.decks == null || manifest.decks.Count == 0)
        {
            Debug.LogWarning("DeckManager: Cards manifest is empty or malformed.");
            handSize = fallbackHandSize;
            return false;
        }

        handSize = manifest.handSize > 0 ? manifest.handSize : fallbackHandSize;
        foreach (DeckManifestEntry entry in manifest.decks)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.deckId)) continue;
            deckManifestById[entry.deckId] = entry;
        }

        LoadActionDefinitions();

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
                ApplyActionRequirementsToCard(card);
            }

            loadedDecksById[deckData.deckId] = deckData;
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
            Shuffle(state.drawPile);
        }

        if (state.drawPile.Count == 0) return false;

        card = state.drawPile[0];
        state.drawPile.RemoveAt(0);
        state.hand.Add(card);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool TryPlayCard(PlayableLeader leader, int cardId, out CardData card)
    {
        card = null;
        if (leader == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && x.cardId == cardId);
        if (index < 0) return false;

        card = state.hand[index];
        state.hand.RemoveAt(index);
        state.discardPile.Add(card);
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool TryConsumeCard(PlayableLeader leader, int cardId, bool drawReplacement, out CardData consumedCard)
    {
        consumedCard = null;
        if (leader == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && x.cardId == cardId);
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

    public bool TryDiscardCard(PlayableLeader leader, int cardId, out CardData discardedCard)
    {
        discardedCard = null;
        if (leader == null) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;

        int index = state.hand.FindIndex(x => x != null && x.cardId == cardId);
        if (index < 0) return false;

        discardedCard = state.hand[index];
        if (discardedCard == null || discardedCard.IsEncounterCard()) return false;

        state.hand.RemoveAt(index);
        state.discardPile.Add(discardedCard);
        return true;
    }

    public bool HasDeckFor(PlayableLeader leader)
    {
        return leader != null && playerDecks.ContainsKey(leader);
    }

    public bool HasActionCardInDeck(Leader leader, string actionClassName, int actionId)
    {
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        bool Matches(CardData card)
        {
            if (card == null) return false;
            if (!IsConsumableEffectCard(card)) return false;
            string cardRef = card.GetActionRef();
            if (!string.IsNullOrWhiteSpace(actionClassName) &&
                string.Equals(cardRef, actionClassName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return actionId > 0 && card.actionId == actionId;
        }

        return state.hand.Any(Matches)
            || state.drawPile.Any(Matches)
            || state.discardPile.Any(Matches);
    }

    public bool HasActionCardInHand(Leader leader, string actionClassName, int actionId, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;
        return FindMatchingActionCardIndex(state.hand, actionClassName, actionId, selectedCharacter, resourceCheck, conditionCheck) >= 0;
    }

    public bool TryGetActionCardInHand(Leader leader, string actionClassName, int actionId, out CardData card, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
    {
        card = null;
        if (leader is not PlayableLeader playableLeader) return false;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        int handIndex = FindMatchingActionCardIndex(state.hand, actionClassName, actionId, selectedCharacter, resourceCheck, conditionCheck);
        if (handIndex < 0) return false;
        card = state.hand[handIndex];
        return card != null;
    }

    public int GetActionCardDifficulty(Leader leader, string actionClassName, int actionId, Character selectedCharacter = null)
    {
        if (TryGetActionCardInHand(leader, actionClassName, actionId, out CardData card, selectedCharacter))
        {
            return card != null ? Mathf.Max(0, card.difficulty) : 0;
        }
        return 0;
    }

    public bool TryConsumeActionCard(Leader leader, string actionClassName, int actionId, bool drawReplacement, out CardData consumedCard, int preferredCardId = 0)
    {
        consumedCard = null;
        if (leader is not PlayableLeader playableLeader) return true;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;

        int handIndex = -1;
        if (preferredCardId > 0)
        {
            handIndex = state.hand.FindIndex(card =>
                card != null
                && card.cardId == preferredCardId
                && MatchesActionCard(card, actionClassName, actionId));
        }
        if (handIndex < 0)
        {
            handIndex = FindMatchingActionCardIndex(state.hand, actionClassName, actionId);
        }
        if (handIndex < 0) return false;

        consumedCard = state.hand[handIndex];
        if (consumedCard == null) return false;
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

        List<Hex> revealedPcHexes = null;
        string revealMessage = null;
        switch (card.GetCardType())
        {
            case CardTypeEnum.Land:
                revealedPcHexes = RevealRegionOnMapOnly(board, card.name);
                revealMessage = $"Lands of {card.name}";
                break;
            case CardTypeEnum.PC:
                revealedPcHexes = RevealPcOnMapOnly(board, card.name);
                revealMessage = $"Lands of {card.region}";
                break;
        }

        leader.RefreshVisibleHexesImmediate();
        MinimapManager.RefreshMinimap();
        QueueRevealMessages(revealedPcHexes, revealMessage);
    }

    public bool TryReturnActionCardToHand(Leader leader, string actionClassName, int actionId)
    {
        if (leader is not PlayableLeader playableLeader) return false;
        if (!playerDecks.TryGetValue(playableLeader, out PlayerDeckState state)) return false;
        if (state.discardPile == null || state.discardPile.Count == 0) return false;

        int discardIndex = -1;
        for (int i = state.discardPile.Count - 1; i >= 0; i--)
        {
            CardData card = state.discardPile[i];
            if (!MatchesActionCard(card, actionClassName, actionId)) continue;
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

    public bool SetTutorialActionCards(PlayableLeader leader, IEnumerable<string> actionClassNames)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;

        ApplyTutorialActionCardsToState(leader, actionClassNames);

        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool RestoreStandardHandAfterTutorial(PlayableLeader leader, int cardsToDraw = 5)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;

        PlayerDeckState rebuilt = BuildDeckStateForLeader(leader);
        if (rebuilt == null) return false;

        playerDecks[leader] = rebuilt;
        RefillHandToCount(rebuilt, Mathf.Max(0, cardsToDraw));
        RefreshHumanPlayerHandUIIfHuman(leader);
        return true;
    }

    public bool ReplenishHandForTurn(PlayableLeader leader)
    {
        if (leader == null) return false;
        if (!loaded && !InitializeFromResources()) return false;
        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state)) return false;
        if (ShouldSkipTurnRefillForTutorialHuman(leader)) return false;

        RefillHandToCount(state, handSize);
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
            EnsureTutorialHandForPlayer(game.player);
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
                Shuffle(state.drawPile);
            }

            if (state.drawPile.Count == 0) break;
            CardData card = state.drawPile[0];
            state.drawPile.RemoveAt(0);
            state.hand.Add(card);
        }
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

    private static CardData CloneCard(CardData card)
    {
        if (card == null) return null;
        return new CardData
        {
            cardId = card.cardId,
            name = card.name,
            description = card.description,
            type = card.type,
            tags = card.tags != null ? new List<string>(card.tags) : new List<string>(),
            deckId = card.deckId,
            alignment = card.alignment,
            actionClassName = card.actionClassName,
            action = card.action,
            actionId = card.actionId,
            commanderSkillRequired = card.commanderSkillRequired,
            agentSkillRequired = card.agentSkillRequired,
            emissarySkillRequired = card.emissarySkillRequired,
            mageSkillRequired = card.mageSkillRequired,
            commander = card.commander,
            agent = card.agent,
            emmissary = card.emmissary,
            mage = card.mage,
            race = card.race,
            artifacts = card.artifacts != null ? new List<Artifact>(card.artifacts) : new List<Artifact>(),
            troopType = card.troopType,
            specialAbilities = card.specialAbilities != null ? new List<ArmySpecialAbilityEnum>(card.specialAbilities) : new List<ArmySpecialAbilityEnum>(),
            spriteName = card.spriteName,
            region = card.region,
            requirementsText = card.requirementsText,
            pcEffectId = card.pcEffectId,
            historyText = card.historyText,
            portraitName = card.portraitName,
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
            jokerRequired = card.jokerRequired
        };
    }

    private static int FindMatchingActionCardIndex(List<CardData> cardsList, string actionClassName, int actionId, Character selectedCharacter = null, Func<Character, bool> resourceCheck = null, Func<Character, bool> conditionCheck = null)
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
            else if (actionId > 0 && card.actionId == actionId)
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

    private static bool MatchesActionCard(CardData card, string actionClassName, int actionId)
    {
        if (!IsConsumableEffectCard(card)) return false;

        if (!string.IsNullOrWhiteSpace(actionClassName)
            && string.Equals(card.GetActionRef(), actionClassName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return actionId > 0 && card.actionId == actionId;
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

        return !string.IsNullOrWhiteSpace(card.GetActionRef()) || card.actionId > 0;
    }

    private List<Hex> RevealRegionOnMapOnly(Board board, string region)
    {
        List<Hex> revealedHexes = new();
        if (board == null || string.IsNullOrWhiteSpace(region)) return revealedHexes;

        string normalizedRegion = NormalizeCardName(region);
        foreach (Hex hex in board.hexes.Values)
        {
            PC pc = hex?.GetPCData();
            if (pc == null) continue;

            string pcRegion = ResolveRegionForPc(pc);
            if (string.IsNullOrWhiteSpace(pcRegion)) continue;
            if (!string.Equals(NormalizeCardName(pcRegion), normalizedRegion, StringComparison.Ordinal)) continue;

            hex.RevealMapOnlyArea(1, false, false);
            revealedHexes.Add(hex);
        }

        return revealedHexes;
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

    private string ResolveRegionForPc(PC pc)
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

    private static void QueueRevealMessages(List<Hex> revealedPcHexes, string message)
    {
        if (revealedPcHexes == null || revealedPcHexes.Count == 0) return;

        BoardNavigator navigator = BoardNavigator.Instance != null ? BoardNavigator.Instance : FindFirstObjectByType<BoardNavigator>();
        foreach (Hex hex in revealedPcHexes.Distinct())
        {
            if (hex == null) continue;
            string revealText = string.IsNullOrWhiteSpace(message) ? "Lands revealed" : message;

            if (navigator != null)
            {
                navigator.EnqueueFocus(hex, 0.35f, 0.18f, true, () =>
                {
                    MessageDisplayNoUI.ShowAnchoredMessage(hex, revealText, Color.yellow);
                });
            }
            else
            {
                MessageDisplayNoUI.ShowAnchoredMessage(hex, revealText, Color.yellow);
            }
        }
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

        PlayerDeckState state = new PlayerDeckState
        {
            deckId = deckId
        };

        foreach (DeckData ownedDeck in GetDeckChain(deckId))
        {
            if (ownedDeck?.cards == null) continue;
            state.drawPile.AddRange(ownedDeck.cards.Select(CloneCard).Where(card => card != null));
        }

        foreach (DeckData sharedDeck in GetSharedDecks())
        {
            if (sharedDeck?.cards == null) continue;
            state.drawPile.AddRange(sharedDeck.cards.Select(CloneCard).Where(card => card != null));
        }
        Shuffle(state.drawPile);
        DrawUpToHandSize(state);
        return state;
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

        string requiredActionClass = tutorial.GetCurrentRequiredActionClass(leader);
        List<string> requiredActions = string.IsNullOrWhiteSpace(requiredActionClass)
            ? new List<string>()
            : new List<string> { requiredActionClass };

        if (!playerDecks.TryGetValue(leader, out PlayerDeckState state))
        {
            SetTutorialActionCards(leader, requiredActions);
            return;
        }

        bool sameCount = state.hand.Count == requiredActions.Count;
        bool allPresent = requiredActions.All(required =>
            state.hand.Any(card =>
                card != null && string.Equals(card.GetActionRef(), required, StringComparison.OrdinalIgnoreCase)));

        if (!sameCount || !allPresent)
        {
            ApplyTutorialActionCardsToState(leader, requiredActions);
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

    private void LoadActionDefinitions()
    {
        TextAsset actionsAsset = Resources.Load<TextAsset>("Actions");
        if (actionsAsset == null) return;

        ActionDefinitionCollection collection = JsonUtility.FromJson<ActionDefinitionCollection>(actionsAsset.text);
        if (collection?.actions == null) return;

        foreach (ActionDefinition definition in collection.actions)
        {
            if (definition == null) continue;
            if (!string.IsNullOrWhiteSpace(definition.className))
            {
                actionDefinitionsByClass[definition.className] = definition;
            }
            if (definition.actionId > 0)
            {
                actionDefinitionsById[definition.actionId] = definition;
            }
        }
    }

    private void ApplyActionRequirementsToCard(CardData card)
    {
        if (card == null) return;

        ActionDefinition definition = ResolveActionDefinitionForCard(card);
        if (definition == null) return;

        if (!string.IsNullOrWhiteSpace(card.description)
            && !string.IsNullOrWhiteSpace(definition.description)
            && string.Equals(card.description.Trim(), definition.description.Trim(), StringComparison.Ordinal))
        {
            card.description = string.Empty;
        }
    }

    private ActionDefinition ResolveActionDefinitionForCard(CardData card)
    {
        if (card == null) return null;

        string actionRef = card.GetActionRef();
        if (!string.IsNullOrWhiteSpace(actionRef)
            && actionDefinitionsByClass.TryGetValue(actionRef, out ActionDefinition byClass))
        {
            return byClass;
        }

        if (card.actionId > 0 && actionDefinitionsById.TryGetValue(card.actionId, out ActionDefinition byId))
        {
            return byId;
        }

        return null;
    }
}
