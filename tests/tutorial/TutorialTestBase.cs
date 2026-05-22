using System.Text.Json;
using NUnit.Framework;

namespace RetroLOTR.Tutorial.Tests;

public abstract class TutorialTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Walk upward from the test binary until we find the Assets/ folder.
    protected static readonly string ResourcesPath = FindResourcesPath();

    private static string FindResourcesPath()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "Assets", "Resources");
            if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate Assets/Resources starting from '{AppContext.BaseDirectory}'");
    }

    // ── Cached loaders ───────────────────────────────────────────────────────

    private static TutorialRoot? _tutorial;
    private static CardsManifest? _manifest;
    private static BiomesRoot? _biomes;
    private static readonly Dictionary<string, List<CardInfo>> DeckCardCache = new();

    protected static TutorialRoot LoadTutorial()
    {
        if (_tutorial != null) return _tutorial;
        string json = File.ReadAllText(Path.Combine(ResourcesPath, "Tutorial.json"));
        return _tutorial = JsonSerializer.Deserialize<TutorialRoot>(json, JsonOpts)!;
    }

    protected static CardsManifest LoadManifest()
    {
        if (_manifest != null) return _manifest;
        string json = File.ReadAllText(Path.Combine(ResourcesPath, "Cards.json"));
        return _manifest = JsonSerializer.Deserialize<CardsManifest>(json, JsonOpts)!;
    }

    protected static BiomesRoot LoadBiomes()
    {
        if (_biomes != null) return _biomes;
        string json = File.ReadAllText(Path.Combine(ResourcesPath, "PlayableLeaderBiomes.json"));
        return _biomes = JsonSerializer.Deserialize<BiomesRoot>(json, JsonOpts)!;
    }

    // ── Convenience getters ──────────────────────────────────────────────────

    protected static TutorialFlow GetFlow(string leaderName)
    {
        var flow = LoadTutorial().Tutorials.FirstOrDefault(f =>
            string.Equals(f.LeaderName, leaderName, StringComparison.OrdinalIgnoreCase));
        Assert.That(flow, Is.Not.Null, $"No tutorial flow found for leader '{leaderName}'");
        return flow!;
    }

    protected static BiomeEntry GetBiome(string leaderName)
    {
        var biome = LoadBiomes().Biomes.FirstOrDefault(b =>
            string.Equals(b.CharacterName, leaderName, StringComparison.OrdinalIgnoreCase));
        Assert.That(biome, Is.Not.Null, $"No biome config found for leader '{leaderName}'");
        return biome!;
    }

    // ── Deck chain builder ───────────────────────────────────────────────────

    // Builds the full card pool available to a leader whose base deck is baseDeckId:
    // walks the parentDeckId chain, then appends all sharedToAll decks.
    protected static List<CardInfo> BuildAvailableCards(string baseDeckId)
    {
        var manifest = LoadManifest();
        var all = new List<CardInfo>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? current = baseDeckId;
        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            all.AddRange(LoadDeckCards(current));
            var entry = manifest.Decks.FirstOrDefault(d =>
                string.Equals(d.DeckId, current, StringComparison.OrdinalIgnoreCase));
            current = entry?.ParentDeckId;
        }

        foreach (var entry in manifest.Decks.Where(d => d.SharedToAll && !visited.Contains(d.DeckId)))
            all.AddRange(LoadDeckCards(entry.DeckId));

        return all;
    }

    private static List<CardInfo> LoadDeckCards(string deckId)
    {
        if (DeckCardCache.TryGetValue(deckId, out var cached)) return cached;

        var manifest = LoadManifest();
        var entry = manifest.Decks.FirstOrDefault(d =>
            string.Equals(d.DeckId, deckId, StringComparison.OrdinalIgnoreCase));
        if (entry == null) { DeckCardCache[deckId] = new(); return DeckCardCache[deckId]; }

        string filePath = Path.Combine(ResourcesPath, entry.ResourcePath + ".json");
        if (!File.Exists(filePath)) { DeckCardCache[deckId] = new(); return DeckCardCache[deckId]; }

        var cards = ParseDeckCards(filePath);
        DeckCardCache[deckId] = cards;
        return cards;
    }

    private static List<CardInfo> ParseDeckCards(string filePath)
    {
        var result = new List<CardInfo>();
        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!doc.RootElement.TryGetProperty("cards", out var cardsEl)) return result;

        foreach (var card in cardsEl.EnumerateArray())
        {
            string name = card.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string type = card.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            int troopType = 0;
            if (card.TryGetProperty("troopType", out var tt) && tt.ValueKind == JsonValueKind.Number)
                tt.TryGetInt32(out troopType);

            if (!string.IsNullOrWhiteSpace(name))
                result.Add(new CardInfo(name, type, troopType));
        }
        return result;
    }

    // ── Card query helpers ───────────────────────────────────────────────────

    protected static bool CardExists(List<CardInfo> pool, string cardName, string? requiredType = null) =>
        pool.Any(c =>
            string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase) &&
            (requiredType == null || string.Equals(c.Type, requiredType, StringComparison.OrdinalIgnoreCase)));

    protected static CardInfo? FindCard(List<CardInfo> pool, string cardName) =>
        pool.FirstOrDefault(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase));

    protected static IEnumerable<GrantedCharacter> AllGrantedCharacters(TutorialFlow flow) =>
        flow.Steps.SelectMany(s => s.Rewards?.GrantCharacters ?? Enumerable.Empty<GrantedCharacter>());
}
