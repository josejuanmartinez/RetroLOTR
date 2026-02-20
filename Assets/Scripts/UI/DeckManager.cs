using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
    public int alignment;
    public string resourcePath;
    public int cardCount;
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
public class CardData
{
    public int cardId;
    public string name;
    public string description;
    public string type;
    public string spriteName;
    public string deckId;
    public int alignment;
    public string actionClassName;
    public int actionId;
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
    private readonly Dictionary<PlayableLeader, PlayerDeckState> playerDecks = new();

    private int handSize = 5;
    private bool loaded;

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
        return true;
    }

    public bool HasDeckFor(PlayableLeader leader)
    {
        return leader != null && playerDecks.ContainsKey(leader);
    }

    private PlayerDeckState BuildDeckStateForLeader(PlayableLeader leader)
    {
        string deckId = ResolveDeckIdForLeader(leader);
        if (string.IsNullOrWhiteSpace(deckId)) return null;
        if (!loadedDecksById.TryGetValue(deckId, out DeckData deckData)) return null;

        PlayerDeckState state = new PlayerDeckState
        {
            deckId = deckId
        };

        state.drawPile.AddRange(deckData.cards);
        Shuffle(state.drawPile);
        DrawUpToHandSize(state);
        return state;
    }

    private string ResolveDeckIdForLeader(PlayableLeader leader)
    {
        if (leader == null) return null;

        DeckManifestEntry byNation = deckManifestById.Values.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x?.nation) &&
            string.Equals(x.nation, leader.characterName, StringComparison.OrdinalIgnoreCase));
        if (byNation != null) return byNation.deckId;

        int alignment = (int)leader.alignment;
        DeckManifestEntry byAlignment = deckManifestById.Values.FirstOrDefault(x => x != null && x.alignment == alignment);
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
}
