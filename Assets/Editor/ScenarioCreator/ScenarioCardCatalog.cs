using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RetroLOTR.Scenarios.EditorTools
{
    /// <summary>
    /// Editor-side lookups for the scenario creator: pulls leader names, card names (PC /
    /// Character / Army / Land regions) and representative terrain sprites from the same
    /// Resources content the game ships with, so the authoring dropdowns always match runtime.
    /// </summary>
    public static class ScenarioCardCatalog
    {
        private static List<string> _playableLeaders;
        private static List<string> _nonPlayableLeaders;
        private static Dictionary<string, List<LeaderVariantConfig>> _playableVariantsByName;
        private static List<string> _pcCards;
        private static List<string> _characterCards;
        private static List<string> _armyCards;
        private static List<string> _regions;
        private static Dictionary<TerrainEnum, Sprite> _terrainSprites;
        private static List<CardData> _allCards;
        private static Dictionary<string, CardData> _cardsByName;
        private static readonly Dictionary<string, Sprite> _artCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> _pcHexCache = new(StringComparer.Ordinal);
        private static bool _pcHexLoaded;
        private static HexTextureMapping _mapping;

        public static IReadOnlyList<string> PlayableLeaders => _playableLeaders ??= LoadLeaderNames("PlayableLeaderBiomes");
        public static IReadOnlyList<string> NonPlayableLeaders => _nonPlayableLeaders ??= LoadLeaderNames("NonPlayableLeaderBiomes");
        public static IReadOnlyList<string> PcCards => _pcCards ??= NamesOfType(CardTypeEnum.PC);
        public static IReadOnlyList<string> CharacterCards => _characterCards ??= NamesOfType(CardTypeEnum.Character);
        public static IReadOnlyList<string> ArmyCards => _armyCards ??= NamesOfType(CardTypeEnum.Army);
        public static IReadOnlyList<string> Regions => _regions ??= NamesOfType(CardTypeEnum.Land);

        /// <summary>The card behind a name (first match across all decks), for previews. May be null.</summary>
        public static CardData GetCard(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            EnsureCardsLoaded();
            return _cardsByName.TryGetValue(name.Trim(), out CardData card) ? card : null;
        }

        public static List<Sprite> GetTerrainVariations(TerrainEnum terrain)
        {
            EnsureMapping();
            return _mapping != null ? _mapping.GetTerrainVariations(terrain) : null;
        }

        public static Sprite GetTerrainSpriteByName(string spriteName)
        {
            EnsureMapping();
            return _mapping != null ? _mapping.GetTerrainSpriteByName(spriteName) : null;
        }

        // Card artwork, resolved the same way DeckExplorer does (search the card/UI art folders).
        public static Sprite GetCardArtwork(CardData card)
        {
            if (card == null) return null;
            foreach (string candidate in new[] { card.spriteName, card.portraitName, card.name })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (_artCache.TryGetValue(candidate, out Sprite cached)) { if (cached != null) return cached; continue; }
                Sprite found = FindSprite(candidate);
                _artCache[candidate] = found;
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// The hex-map artwork for a placed PC (city), loaded from Assets/Art/Hexes/PCs.
        /// The card name (e.g. "HelmDeep", "FangornSCamp") and the art filename
        /// (e.g. "Helm Deep.png", "Fangorns Camp.png") often differ only in spacing/casing,
        /// so we match on the same normalized key the runtime uses (HexTextureMapping.NormalizeKey),
        /// not an exact string compare. May be null when no art exists.
        /// </summary>
        public static Sprite GetPcHexSprite(string pcName)
        {
            if (string.IsNullOrWhiteSpace(pcName)) return null;
            string key = HexTextureMapping.NormalizeKey(pcName);
            if (string.IsNullOrEmpty(key)) return null;

            EnsurePcHexLoaded();
            return _pcHexCache.TryGetValue(key, out Sprite sprite) ? sprite : null;
        }

        // Indexes every sprite under Assets/Art/Hexes/PCs by its normalized name, mirroring the
        // runtime lookup so the same card name resolves to the same art in editor and game.
        private static void EnsurePcHexLoaded()
        {
            if (_pcHexLoaded) return;
            _pcHexLoaded = true;
            _pcHexCache.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Hexes/PCs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path)) continue;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not Sprite sprite) continue;
                    string spriteKey = HexTextureMapping.NormalizeKey(sprite.name);
                    if (!string.IsNullOrEmpty(spriteKey) && !_pcHexCache.ContainsKey(spriteKey))
                        _pcHexCache[spriteKey] = sprite;
                }
            }
        }

        /// <summary>
        /// The variants of a playable leader (from PlayableLeaderBiomes.json), used by the
        /// scenario creator to let an author pin a leader start to one variant. Empty when the
        /// name is not a known playable leader or it has no variants.
        /// </summary>
        public static IReadOnlyList<LeaderVariantConfig> GetPlayableLeaderVariants(string leaderName)
        {
            if (string.IsNullOrWhiteSpace(leaderName)) return Array.Empty<LeaderVariantConfig>();
            EnsurePlayableVariantsLoaded();
            return _playableVariantsByName.TryGetValue(leaderName.Trim(), out List<LeaderVariantConfig> variants)
                ? variants
                : Array.Empty<LeaderVariantConfig>();
        }

        private static void EnsurePlayableVariantsLoaded()
        {
            if (_playableVariantsByName != null) return;
            _playableVariantsByName = new Dictionary<string, List<LeaderVariantConfig>>(StringComparer.OrdinalIgnoreCase);

            TextAsset asset = Resources.Load<TextAsset>("PlayableLeaderBiomes");
            if (asset == null) return;
            LeaderBiomeConfigCollection collection = JsonUtility.FromJson<LeaderBiomeConfigCollection>(asset.text);
            if (collection?.biomes == null) return;

            foreach (LeaderBiomeConfig biome in collection.biomes)
            {
                if (biome == null || string.IsNullOrWhiteSpace(biome.characterName)) continue;
                _playableVariantsByName[biome.characterName.Trim()] = biome.variants ?? new List<LeaderVariantConfig>();
            }
        }

        /// <summary>All leader names (playable first), used for owner dropdowns.</summary>
        public static List<string> AllLeaders()
        {
            var list = new List<string>();
            list.AddRange(PlayableLeaders);
            list.AddRange(NonPlayableLeaders);
            return list.Distinct().ToList();
        }

        public static void Invalidate()
        {
            _playableLeaders = _nonPlayableLeaders = _pcCards = _characterCards = _armyCards = _regions = null;
            _playableVariantsByName = null;
            _terrainSprites = null;
            _allCards = null;
            _cardsByName = null;
            _artCache.Clear();
            _pcHexCache.Clear();
            _pcHexLoaded = false;
            _mapping = null;
        }

        private static List<string> NamesOfType(CardTypeEnum type)
        {
            EnsureCardsLoaded();
            return _allCards
                .Where(c => c != null && c.GetCardType() == type && !string.IsNullOrWhiteSpace(c.name))
                .Select(c => c.name.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        private static void EnsureCardsLoaded()
        {
            if (_allCards != null) return;
            _allCards = new List<CardData>();
            _cardsByName = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);

            TextAsset manifestAsset = Resources.Load<TextAsset>("Cards");
            if (manifestAsset == null) return;
            CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
            if (manifest?.decks == null) return;

            foreach (DeckManifestEntry entry in manifest.decks)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.resourcePath)) continue;
                TextAsset deckAsset = Resources.Load<TextAsset>(entry.resourcePath);
                if (deckAsset == null) continue;
                DeckData deck = JsonUtility.FromJson<DeckData>(deckAsset.text);
                if (deck?.cards == null) continue;

                foreach (CardData card in deck.cards)
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.name)) continue;
                    _allCards.Add(card);
                    string key = card.name.Trim();
                    if (!_cardsByName.ContainsKey(key)) _cardsByName[key] = card;
                }
            }
        }

        private static void EnsureMapping()
        {
            if (_mapping != null) return;
            _mapping = FindHexTextureMapping();
        }

        // Card art lookup mirroring DeckExplorerWindow.FindSprite.
        private static Sprite FindSprite(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string[] roots = { "Assets/Art/Cards", "Assets/Art/UI" };
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path)) continue;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sprite && string.Equals(sprite.name, name, StringComparison.OrdinalIgnoreCase))
                        return sprite;
                }
            }
            return null;
        }

        public static Sprite GetTerrainSprite(TerrainEnum terrain)
        {
            _terrainSprites ??= LoadTerrainSprites();
            return _terrainSprites.TryGetValue(terrain, out Sprite s) ? s : null;
        }

        private static List<string> LoadLeaderNames(string resource)
        {
            TextAsset asset = Resources.Load<TextAsset>(resource);
            if (asset == null) return new List<string>();
            // Both files share the LeaderBiomeConfigCollection shape (a 'biomes' array).
            LeaderBiomeConfigCollection collection = JsonUtility.FromJson<LeaderBiomeConfigCollection>(asset.text);
            if (collection?.biomes == null) return new List<string>();
            return collection.biomes
                .Where(b => b != null && !string.IsNullOrWhiteSpace(b.characterName))
                .Select(b => b.characterName.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        // Reads the first sprite variation per terrain off whichever prefab carries the
        // HexTextureMapping component (the Hex prefab), so the editor grid shows real tiles.
        private static Dictionary<TerrainEnum, Sprite> LoadTerrainSprites()
        {
            var result = new Dictionary<TerrainEnum, Sprite>();
            HexTextureMapping mapping = FindHexTextureMapping();
            if (mapping == null) return result;

            void Add(TerrainEnum t, List<Sprite> variations)
            {
                if (variations != null && variations.Count > 0 && variations[0] != null)
                    result[t] = variations[0];
            }

            Add(TerrainEnum.deepWater, mapping.deepWaterVariations);
            Add(TerrainEnum.shallowWater, mapping.shallowWaterVariations);
            Add(TerrainEnum.desert, mapping.desertVariations);
            Add(TerrainEnum.forest, mapping.forestVariations);
            Add(TerrainEnum.grasslands, mapping.grassVariations);
            Add(TerrainEnum.hills, mapping.hillsVariations);
            Add(TerrainEnum.plains, mapping.plainsVariations);
            Add(TerrainEnum.shore, mapping.shoreVariations);
            Add(TerrainEnum.swamp, mapping.swampVariations);
            Add(TerrainEnum.wastelands, mapping.wastelandsVariations);
            Add(TerrainEnum.mountains, mapping.mountainsVariations);
            Add(TerrainEnum.snow, mapping.snowVariations);
            return result;
        }

        private static HexTextureMapping FindHexTextureMapping()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                HexTextureMapping mapping = prefab.GetComponent<HexTextureMapping>();
                if (mapping != null) return mapping;
            }
            return null;
        }
    }
}
