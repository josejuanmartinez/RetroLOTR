using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System;

[RequireComponent(typeof(Board))]
public class NationSpawner : MonoBehaviour
{
    private sealed class RegionSeed
    {
        public readonly Vector2Int position;
        public readonly string region;

        public RegionSeed(Vector2Int position, string region)
        {
            this.position = position;
            this.region = region;
        }
    }

    public Board board;

    private PlayableLeaders playableLeaders;
    private NonPlayableLeaders nonPlayableLeaders;
    private List<Vector2Int> placedPositions;
    private CharacterInstantiator characterInstantiator;
    private Dictionary<TerrainEnum, List<Vector2Int>> terrainHexCache;
    private Dictionary<FeaturesEnum, List<Vector2Int>> featuresHexCache;
    private Dictionary<Vector2Int, Vector3Int> cubeCoordinateCache;
    private int currentCharacterCount;
    private int currentPcCount;
    private bool isInitialized = false;
    private readonly Dictionary<string, Vector2Int> leaderPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Vector2Int>> startingCityPositionsByRegion = new(StringComparer.OrdinalIgnoreCase);
    // Exact hex of each pre-spawned ownerless anchor city (Hobbiton / Orthanc / Barad-dur),
    // keyed by region. Captured before pass-2 NPLs pollute startingCityPositionsByRegion, so
    // it always points at the real anchor a playable leader is meant to start beside.
    private readonly Dictionary<string, Vector2Int> ownerlessAnchorPositionByRegion = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TerrainEnum[] StartFallbackTerrains =
        { TerrainEnum.plains, TerrainEnum.grasslands, TerrainEnum.hills, TerrainEnum.shore };
    // Every land (non-water) terrain a leader may legitimately stand on. SelectClosestPosition
    // searches all of these so a leader always lands next to its own anchor, even if the only
    // hexes immediately around the anchor are an "off-list" terrain like mountains or desert.
    private static readonly TerrainEnum[] LandTerrains =
    {
        TerrainEnum.plains, TerrainEnum.grasslands, TerrainEnum.shore, TerrainEnum.hills,
        TerrainEnum.forest, TerrainEnum.swamp, TerrainEnum.desert, TerrainEnum.wastelands,
        TerrainEnum.mountains
    };
    private Dictionary<string, string> tutorialAnchorByNpl = new(StringComparer.OrdinalIgnoreCase);
    private bool landRegionsAssigned;

    public void Initialize(Board board)
    {
        if (board == null)
        {
            Debug.LogError("Board is null in NationSpawner.Initialize!");
            return;
        }

        this.board = board;
        placedPositions = new List<Vector2Int>(20); // Pre-allocate for typical number of leaders
        terrainHexCache = new Dictionary<TerrainEnum, List<Vector2Int>>();
        featuresHexCache = new Dictionary<FeaturesEnum, List<Vector2Int>>();
        cubeCoordinateCache = new Dictionary<Vector2Int, Vector3Int>();

        characterInstantiator = FindFirstObjectByType<CharacterInstantiator>();
        if (characterInstantiator == null)
        {
            Debug.LogError("CharacterInstantiator not found!");
            return;
        }

        playableLeaders = FindFirstObjectByType<PlayableLeaders>();
        if (playableLeaders == null)
        {
            Debug.LogError("PlayableLeaders not found!");
            return;
        }
        playableLeaders.Initialize();
        tutorialAnchorByNpl = BuildTutorialAnchors(playableLeaders.playableLeaders?.biomes);

        nonPlayableLeaders = FindFirstObjectByType<NonPlayableLeaders>();
        if (nonPlayableLeaders == null)
        {
            Debug.LogError("NonPlayableLeaders not found!");
            return;
        }
        nonPlayableLeaders.Initialize();

        isInitialized = true;
    }

    public void BuildTerrainHexCache(TerrainEnum[,] terrainGrid)
    {
        featuresHexCache[FeaturesEnum.river] = board.boardGenerator.riverCoastHexes.ToList();
        featuresHexCache[FeaturesEnum.lake] = board.boardGenerator.lakeCoastHexes.ToList();

        if (terrainGrid == null)
        {
            Debug.LogError("terrainGrid is null in BuildTerrainHexCache!");
            return;
        }

        terrainHexCache.Clear();
        for (int x = 0; x < board.GetHeight(); x++)
        {
            for (int y = 0; y < board.GetWidth(); y++)
            {
                var terrain = terrainGrid[x, y];
                if (!terrainHexCache.ContainsKey(terrain))
                {
                    terrainHexCache[terrain] = new List<Vector2Int>();
                }
                terrainHexCache[terrain].Add(new Vector2Int(x, y));
            }
        }
    }

    private void RecountExistingEntities()
    {
        currentCharacterCount = 0;
        currentPcCount = 0;

        if (board?.hexes == null)
            return;

        foreach (var hex in board.hexes.Values)
        {
            if (hex == null) continue;
            currentCharacterCount += hex.characters?.Count ?? 0;
            if (hex.GetPC() != null) currentPcCount++;
        }
    }

    private bool EnsureCharacterCapacity(string context)
    {
        if (currentCharacterCount >= Game.MAX_CHARACTERS)
        {
            Debug.LogWarning($"Max characters reached. {context}");
            return false;
        }
        return true;
    }

    private bool EnsurePcCapacity()
    {
        if (currentPcCount >= Game.MAX_PCS)
        {
            Debug.LogWarning("Max PCs reached. Skipping PC instantiation.");
            return false;
        }
        return true;
    }

    public void Spawn()
    {
        if (!isInitialized)
        {
            Debug.LogError("NationSpawner not initialized!");
            return;
        }

        if (board.terrainGrid == null)
        {
            Debug.LogError("terrainGrid is not initialized!");
            return;
        }

        RecountExistingEntities();
        leaderPositions.Clear();
        startingCityPositionsByRegion.Clear();
        ownerlessAnchorPositionByRegion.Clear();
        // placedPositions is only allocated in Initialize(); clear it here so a board
        // regeneration in the same session doesn't spread new nations against stale,
        // last-board coordinates.
        placedPositions.Clear();

        // Pre-spawn ownerless PCs first so their startingCityRegions are registered
        // before playable leaders look them up (e.g. Sauron needs "Gorgoroth" from Barad-dur)
        PreSpawnOwnerlessPcs(nonPlayableLeaders.nonPlayableLeaders.biomes, placedPositions);

        InstantiateLeadersAndCharacters(playableLeaders.playableLeaders.biomes, placedPositions);
        InstantiateLeadersAndCharacters(nonPlayableLeaders.nonPlayableLeaders.biomes, placedPositions);
        VerifyStartingPlacements();
        AssignLandRegions();
    }

    public bool EnsureLandRegionsAssigned()
    {
        if (landRegionsAssigned) return true;
        AssignLandRegions();
        return landRegionsAssigned;
    }

    private void AssignLandRegions()
    {
        if (board == null || board.hexes == null || board.hexes.Count == 0) return;

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        List<string> allLandRegions;
        Dictionary<string, string> pcRegionsByName = new(StringComparer.OrdinalIgnoreCase);

        if (deckManager != null)
        {
            if (deckManager.cards == null || deckManager.cards.Count == 0)
            {
                deckManager.InitializeFromResources();
            }

            allLandRegions = deckManager.cards != null
                ? deckManager.cards
                    .Where(card => card != null && card.GetCardType() == CardTypeEnum.Land && !string.IsNullOrWhiteSpace(card.name))
                    .Select(card => card.name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();

            foreach (CardData card in deckManager.cards ?? new List<CardData>())
            {
                if (card == null || string.IsNullOrWhiteSpace(card.name) || string.IsNullOrWhiteSpace(card.region)) continue;
                if (card.GetCardType() != CardTypeEnum.PC) continue;
                string key = card.name.Trim();
                if (!pcRegionsByName.ContainsKey(key))
                {
                    pcRegionsByName[key] = card.region.Trim();
                }
            }
        }
        else if (!TryLoadRegionDataFromResources(out allLandRegions, out pcRegionsByName))
        {
            Debug.LogWarning("NationSpawner: Could not load card data for land region assignment.");
            return;
        }

        if (allLandRegions.Count == 0) return;

        foreach (Hex hex in board.GetHexes())
        {
            hex?.SetLandRegion(null);
        }

        HashSet<Vector2Int> assignedPositions = new();
        Queue<RegionSeed> seedQueue = new();
        HashSet<string> seededRegions = new(StringComparer.OrdinalIgnoreCase);

        foreach (Hex hex in board.GetHexes())
        {
            if (hex == null) continue;
            PC pc = hex.GetPCData();
            if (pc == null) continue;

            string region = deckManager != null
                ? deckManager.ResolveRegionForPc(pc)
                : ResolveRegionForPcFromLookup(pc, pcRegionsByName);
            if (string.IsNullOrWhiteSpace(region)) continue;

            seedQueue.Enqueue(new RegionSeed(hex.v2, region.Trim()));
            seededRegions.Add(region.Trim());
        }

        List<string> fallbackRegions = allLandRegions
            .Where(region => !seededRegions.Contains(region))
            .ToList();

        List<Hex> unassignedHexes = board.GetHexes()
            .Where(hex => hex != null && string.IsNullOrWhiteSpace(hex.GetLandRegion()))
            .ToList();

        // Collect all PC seed positions so fallback seeds can be spread away from them
        var existingSeedPositions = seedQueue.Select(s => s.position).ToList();

        foreach (string region in fallbackRegions)
        {
            if (unassignedHexes.Count == 0) break;

            Hex startHex = PickFurthestUnassignedHex(unassignedHexes, existingSeedPositions);
            if (startHex == null) continue;

            startHex.SetLandRegion(region.Trim());
            seedQueue.Enqueue(new RegionSeed(startHex.v2, region.Trim()));
            existingSeedPositions.Add(startHex.v2);

            unassignedHexes = board.GetHexes()
                .Where(hex => hex != null && string.IsNullOrWhiteSpace(hex.GetLandRegion()))
                .ToList();
        }

        FloodAssignRegions(seedQueue, assignedPositions);

        if (board.GetHexes().Any(hex => hex != null && string.IsNullOrWhiteSpace(hex.GetLandRegion())))
        {
            string defaultRegion = allLandRegions[0];
            foreach (Hex hex in board.GetHexes())
            {
                if (hex == null || !string.IsNullOrWhiteSpace(hex.GetLandRegion())) continue;
                hex.SetLandRegion(defaultRegion);
            }
        }

        landRegionsAssigned = board.GetHexes().All(hex => hex != null && !string.IsNullOrWhiteSpace(hex.GetLandRegion()));
    }

    private static bool TryLoadRegionDataFromResources(out List<string> landRegions, out Dictionary<string, string> pcRegionsByName)
    {
        landRegions = new List<string>();
        pcRegionsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TextAsset manifestAsset = Resources.Load<TextAsset>("Cards");
        if (manifestAsset == null) return false;

        CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (manifest?.decks == null || manifest.decks.Count == 0) return false;

        foreach (DeckManifestEntry entry in manifest.decks)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.resourcePath)) continue;

            TextAsset deckAsset = Resources.Load<TextAsset>(entry.resourcePath);
            if (deckAsset == null) continue;

            DeckData deckData = JsonUtility.FromJson<DeckData>(deckAsset.text);
            if (deckData?.cards == null || deckData.cards.Count == 0) continue;

            foreach (CardData card in deckData.cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.name)) continue;
                if (card.GetCardType() == CardTypeEnum.Land)
                {
                    landRegions.Add(card.name.Trim());
                    continue;
                }

                if (card.GetCardType() != CardTypeEnum.PC || string.IsNullOrWhiteSpace(card.region)) continue;
                string key = card.name.Trim();
                if (!pcRegionsByName.ContainsKey(key))
                {
                    pcRegionsByName[key] = card.region.Trim();
                }
            }
        }

        landRegions = landRegions
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return landRegions.Count > 0;
    }

    private static string ResolveRegionForPcFromLookup(PC pc, IReadOnlyDictionary<string, string> pcRegionsByName)
    {
        if (pc == null || pcRegionsByName == null) return null;
        if (!string.IsNullOrWhiteSpace(pc.pcName) && pcRegionsByName.TryGetValue(pc.pcName.Trim(), out string region))
        {
            return region;
        }

        return null;
    }

    private static Hex PickFurthestUnassignedHex(List<Hex> candidates, List<Vector2Int> existingSeeds)
    {
        if (candidates.Count == 0) return null;

        Hex best = null;
        float bestMinDist = float.MinValue;

        // Two passes: prefer non-PC land hexes, fall back to anything
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var hex in candidates)
            {
                if (hex == null) continue;
                if (pass == 0 && (hex.HasAnyPC() || hex.IsWaterTerrain())) continue;

                if (existingSeeds.Count == 0)
                    return hex;

                float minDist = float.MaxValue;
                foreach (var seed in existingSeeds)
                {
                    float dx = hex.v2.x - seed.x;
                    float dy = hex.v2.y - seed.y;
                    float d = dx * dx + dy * dy;
                    if (d < minDist) minDist = d;
                }

                if (minDist > bestMinDist) { bestMinDist = minDist; best = hex; }
            }

            if (best != null) return best;
        }

        return best;
    }

    private void FloodAssignRegions(Queue<RegionSeed> seeds, HashSet<Vector2Int> assignedPositions, int maxAssignmentsPerRegion = int.MaxValue)
    {
        if (board == null || board.hexes == null || seeds == null) return;

        Queue<RegionSeed> queue = new(seeds);
        Dictionary<string, int> regionCounts = new(StringComparer.OrdinalIgnoreCase);
        HashSet<Vector2Int> visited = new();

        while (queue.Count > 0)
        {
            RegionSeed current = queue.Dequeue();
            if (current == null || string.IsNullOrWhiteSpace(current.region)) continue;
            if (!board.hexes.TryGetValue(current.position, out Hex hex) || hex == null) continue;

            string regionKey = current.region.Trim();
            if (!regionCounts.TryGetValue(regionKey, out int count))
            {
                count = 0;
            }
            if (count >= maxAssignmentsPerRegion) continue;

            string existingRegion = hex.GetLandRegion();
            if (!string.IsNullOrWhiteSpace(existingRegion))
            {
                if (!string.Equals(existingRegion.Trim(), regionKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (assignedPositions.Add(current.position))
                {
                    regionCounts[regionKey] = count + 1;
                }

                var matchingNeighbors = ((current.position.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
                for (int i = 0; i < matchingNeighbors.Length; i++)
                {
                    Vector2Int next = new(current.position.x + matchingNeighbors[i].x, current.position.y + matchingNeighbors[i].y);
                    if (!visited.Add(next)) continue;
                    if (!board.hexes.ContainsKey(next)) continue;
                    queue.Enqueue(new RegionSeed(next, regionKey));
                }

                continue;
            }

            hex.SetLandRegion(regionKey);
            assignedPositions.Add(current.position);
            regionCounts[regionKey] = count + 1;

            var neighbors = ((current.position.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
            for (int i = 0; i < neighbors.Length; i++)
            {
                Vector2Int next = new(current.position.x + neighbors[i].x, current.position.y + neighbors[i].y);
                if (!visited.Add(next)) continue;
                if (!board.hexes.ContainsKey(next)) continue;
                queue.Enqueue(new RegionSeed(next, regionKey));
            }
        }
    }

    private void InstantiateLeadersAndCharacters(List<LeaderBiomeConfig> leaderBiomes, List<Vector2Int> placedPositions)
    {
        foreach (LeaderBiomeConfig leaderBiomeConfig in leaderBiomes)
        {
            Vector2Int? position = InstantiateLeaderAndCharacters(leaderBiomeConfig, placedPositions, true, null);
            if (position.HasValue && !string.IsNullOrWhiteSpace(leaderBiomeConfig.characterName))
            {
                leaderPositions[leaderBiomeConfig.characterName] = position.Value;
            }
        }
    }

    private void PreSpawnOwnerlessPcs(List<NonPlayableLeaderBiomeConfig> nonPlayableleaderBiomes, List<Vector2Int> placedPositions)
    {
        // These are the starting-nation anchor cities (e.g. Barad-dur, Hobbiton, Orthanc).
        // Force them apart so two nations can never spawn on top of each other — that is
        // what made playable leaders appear to start in another nation's city.
        float separation = GetMinStartSeparation();
        foreach (NonPlayableLeaderBiomeConfig config in nonPlayableleaderBiomes)
        {
            if (config == null || !config.spawnPcWithoutOwner) continue;
            Vector2Int? position = InstantiateOwnerlessPc(config, placedPositions, null, separation);
            if (!position.HasValue) continue;
            if (!string.IsNullOrWhiteSpace(config.characterName))
                leaderPositions[config.characterName] = position.Value;
            if (!string.IsNullOrWhiteSpace(config.startingCityRegion))
                ownerlessAnchorPositionByRegion[config.startingCityRegion] = position.Value;
        }
    }

    private void InstantiateLeadersAndCharacters(List<NonPlayableLeaderBiomeConfig> nonPlayableleaderBiomes, List<Vector2Int> placedPositions)
    {
        IEnumerable<NonPlayableLeaderBiomeConfig> orderedBiomes = nonPlayableleaderBiomes
            .OrderByDescending(b => !string.IsNullOrWhiteSpace(b.characterName) && tutorialAnchorByNpl.ContainsKey(b.characterName))
            .ThenBy(b => b.characterName);

        foreach (NonPlayableLeaderBiomeConfig nonPlayableleaderBiomeConfig in orderedBiomes)
        {
            // Skip ownerless PCs already placed in the pre-spawn pass
            if (nonPlayableleaderBiomeConfig.spawnPcWithoutOwner &&
                !string.IsNullOrWhiteSpace(nonPlayableleaderBiomeConfig.characterName) &&
                leaderPositions.ContainsKey(nonPlayableleaderBiomeConfig.characterName))
            {
                continue;
            }

            Vector2Int? preferredPosition = null;
            if (!string.IsNullOrWhiteSpace(nonPlayableleaderBiomeConfig.characterName) &&
                tutorialAnchorByNpl.TryGetValue(nonPlayableleaderBiomeConfig.characterName, out string anchorName) &&
                leaderPositions.TryGetValue(anchorName, out Vector2Int anchorPosition))
            {
                preferredPosition = anchorPosition;
            }

            // Tutorial dummies: spawn only the city, no leader or characters.
            bool ownerlessSpawn = nonPlayableleaderBiomeConfig.tutorialDummy || nonPlayableleaderBiomeConfig.spawnPcWithoutOwner;
            Vector2Int? position = ownerlessSpawn
                ? InstantiateOwnerlessPc(nonPlayableleaderBiomeConfig, placedPositions, preferredPosition)
                : InstantiateLeaderAndCharacters(nonPlayableleaderBiomeConfig, placedPositions, false, preferredPosition);
            if (position.HasValue && !string.IsNullOrWhiteSpace(nonPlayableleaderBiomeConfig.characterName))
            {
                leaderPositions[nonPlayableleaderBiomeConfig.characterName] = position.Value;
            }
        }
    }
    
    private Vector2Int? InstantiateLeaderAndCharacters(LeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, bool isPlayable, Vector2Int? preferredPosition, float minSeparation = 0f)
    {
        /*if (FindObjectsByType<Leader>(FindObjectsSortMode.None).Length >= Game.MAX_LEADERS)
        {
            Debug.LogWarning("Max leaders reached. Skipping leader instantiation.");
            return;
        }*/
        if (!isPlayable && !EnsurePcCapacity())
        {
            string leaderName = string.IsNullOrWhiteSpace(leaderBiomeConfig.characterName) ? "Unknown" : leaderBiomeConfig.characterName;
            Debug.LogError($"Skipping non-playable leader instantiation for {leaderName} because max PCs reached.");
            return null;
        }
        preferredPosition ??= GetPreferredPositionForStartingCityRegion(leaderBiomeConfig);
        TerrainEnum chosenTerrain;
        Vector2Int bestPosition = preferredPosition.HasValue
            ? SelectClosestPosition(leaderBiomeConfig, preferredPosition.Value, out chosenTerrain)
            : SelectSpreadPosition(leaderBiomeConfig, placedPositions, minSeparation, out chosenTerrain);
        placedPositions.Add(bestPosition);

        Vector2Int v2 = new(bestPosition.x, bestPosition.y);
        Hex hex = board.hexes[v2];

        if (!EnsureCharacterCapacity("Skipping leader instantiation."))
            return null;

        Leader leader;
        if (isPlayable)
        {
            leader = characterInstantiator.InstantiatePlayableLeader(hex, leaderBiomeConfig);
        }
        else if (leaderBiomeConfig is NonPlayableLeaderBiomeConfig nonPlayableConfig)
        {
            leader = characterInstantiator.InstantiateNonPlayableLeader(hex, nonPlayableConfig);
        }
        else
        {
            Debug.LogError("Non playable leader biome config expected but not provided.");
            return null;
        }

        currentCharacterCount++;

        foreach (var character in leader.GetBiome().startingCharacters)
        {
            if (!EnsureCharacterCapacity("Skipping leader instantiation."))
                return null;

            characterInstantiator.InstantiateCharacter(leader, hex, character);
            currentCharacterCount++;
        }

        bool skipStartingPc = isPlayable && leaderBiomeConfig.startingCitySize == PCSizeEnum.NONE;
        if (!skipStartingPc)
        {
            if (!EnsurePcCapacity())
                return null;

            PC pc = new(leader, hex);
            hex.SetPC(pc, leaderBiomeConfig.pcFeature, leaderBiomeConfig.fortFeature, leaderBiomeConfig.isIsland);

            // If we fell back from a shore start and the PC was meant to have a port, strip the port on non-shore terrain.
            if (leaderBiomeConfig.startsWithPort && leaderBiomeConfig.terrain == TerrainEnum.shore && chosenTerrain != TerrainEnum.shore)
            {
                pc.hasPort = false;
                hex.RedrawPC();
            }

            // Non-playable leaders that start with a port but have no adjacent water lose the port and warships.
            if (!isPlayable && leaderBiomeConfig.startsWithPort && !HasNeighboringWater(hex))
            {
                if (pc != null && pc.hasPort)
                {
                    pc.hasPort = false;
                }
                RemoveWarshipsFromLeaderArmiesAtHex(leader, hex);
                hex.RedrawPC();
                hex.RedrawArmies();
            }

            currentPcCount++;
            RegisterStartingCityPosition(leaderBiomeConfig, bestPosition);
        }
        return bestPosition;
    }

    private void PositionPlayableLeadersNearTutorialPcs()
    {
        if (board == null || board.hexes == null) return;

        PlayableLeader[] leaders = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None);
        if (leaders == null || leaders.Length == 0) return;

        foreach (PlayableLeader playableLeader in leaders)
        {
            if (playableLeader == null || playableLeader.hex == null) continue;
            LeaderBiomeConfig biome = playableLeader.GetBiome();
            if (biome == null || biome.tutorialAnchors == null || biome.tutorialAnchors.Count == 0) continue;

            Hex targetHex = FindPcHexByAnchorNames(biome.tutorialAnchors);
            if (targetHex == null) continue;

            Hex neighbor = FindNeighborHex(targetHex);
            if (neighbor == null || neighbor == playableLeader.hex) continue;

            RelocateCharacter(playableLeader, neighbor);
        }
    }


    private Hex FindPcHexByAnchorNames(List<string> anchorNames)
    {
        if (anchorNames == null || anchorNames.Count == 0) return null;

        foreach (string anchorName in anchorNames)
        {
            if (string.IsNullOrWhiteSpace(anchorName)) continue;
            foreach (Hex hex in board.hexes.Values)
            {
                if (hex == null) continue;
                PC pc = hex.GetPCData();
                if (pc == null) continue;
                if (string.Equals(pc.pcName, anchorName, StringComparison.OrdinalIgnoreCase))
                {
                    return hex;
                }
            }
        }

        return null;
    }

    private Hex FindNeighborHex(Hex targetHex)
    {
        if (targetHex == null || board == null) return null;
        var neighbors = ((targetHex.v2.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int pos = new(targetHex.v2.x + neighbors[i].x, targetHex.v2.y + neighbors[i].y);
            if (!board.hexes.TryGetValue(pos, out Hex neighbor) || neighbor == null) continue;
            if (neighbor.IsWaterTerrain()) continue;
            if (neighbor.HasAnyPC()) continue;
            if (neighbor.characters != null && neighbor.characters.Count > 0) continue;
            return neighbor;
        }

        return null;
    }

    private static void RelocateCharacter(Character actor, Hex targetHex)
    {
        if (actor == null || targetHex == null) return;
        Hex oldHex = actor.hex;
        if (oldHex != null)
        {
            oldHex.characters.Remove(actor);
            if (actor.IsArmyCommander())
            {
                oldHex.armies.Remove(actor.GetArmy());
            }
            oldHex.RedrawCharacters();
            oldHex.RedrawArmies();
        }

        actor.hex = targetHex;
        if (!targetHex.characters.Contains(actor)) targetHex.characters.Add(actor);
        if (actor.IsArmyCommander() && !targetHex.armies.Contains(actor.GetArmy()))
        {
            targetHex.armies.Add(actor.GetArmy());
        }
        Character.RefreshArtifactPcVisibilityForHex(oldHex);
        Character.RefreshArtifactPcVisibilityForHex(targetHex);
        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();
    }

    private Vector2Int? InstantiateOwnerlessPc(NonPlayableLeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, Vector2Int? preferredPosition, float minSeparation = 0f)
    {
        if (!EnsurePcCapacity())
        {
            string leaderName = string.IsNullOrWhiteSpace(leaderBiomeConfig.characterName) ? "Unknown" : leaderBiomeConfig.characterName;
            Debug.LogError($"Skipping ownerless PC instantiation for {leaderName} because max PCs reached.");
            return null;
        }

        preferredPosition ??= GetPreferredPositionForStartingCityRegion(leaderBiomeConfig);
        TerrainEnum chosenTerrain;
        Vector2Int bestPosition = preferredPosition.HasValue
            ? SelectClosestPosition(leaderBiomeConfig, preferredPosition.Value, out chosenTerrain)
            : SelectSpreadPosition(leaderBiomeConfig, placedPositions, minSeparation, out chosenTerrain);
        placedPositions.Add(bestPosition);

        Vector2Int v2 = new(bestPosition.x, bestPosition.y);
        Hex hex = board.hexes[v2];

        PC pc = new(null, leaderBiomeConfig.startingCityName, leaderBiomeConfig.startingCitySize, leaderBiomeConfig.startingCityFortSize,
            leaderBiomeConfig.startsWithPort, leaderBiomeConfig.startingCityIsHidden, hex, true);
        hex.SetPC(pc, leaderBiomeConfig.pcFeature, leaderBiomeConfig.fortFeature, leaderBiomeConfig.isIsland);

        if (leaderBiomeConfig.startsWithPort && leaderBiomeConfig.terrain == TerrainEnum.shore && chosenTerrain != TerrainEnum.shore)
        {
            pc.hasPort = false;
            hex.RedrawPC();
        }

        currentPcCount++;
        RegisterStartingCityPosition(leaderBiomeConfig, bestPosition);

        if (leaderBiomeConfig.startingCharacters != null && leaderBiomeConfig.startingCharacters.Count > 0)
        {
            Debug.LogWarning($"Ownerless PC '{leaderBiomeConfig.startingCityName}' has starting characters configured; skipping those.");
        }

        return bestPosition;
    }

    private Vector2Int? GetPreferredPositionForStartingCityRegion(LeaderBiomeConfig leaderBiomeConfig)
    {
        if (leaderBiomeConfig == null || string.IsNullOrWhiteSpace(leaderBiomeConfig.startingCityRegion))
        {
            return null;
        }

        if (!startingCityPositionsByRegion.TryGetValue(leaderBiomeConfig.startingCityRegion, out List<Vector2Int> positions) ||
            positions == null || positions.Count == 0)
        {
            return null;
        }

        int avgX = Mathf.RoundToInt((float)positions.Average(p => p.x));
        int avgY = Mathf.RoundToInt((float)positions.Average(p => p.y));
        return new Vector2Int(avgX, avgY);
    }

    private void RegisterStartingCityPosition(LeaderBiomeConfig leaderBiomeConfig, Vector2Int position)
    {
        if (leaderBiomeConfig == null || string.IsNullOrWhiteSpace(leaderBiomeConfig.startingCityRegion))
        {
            return;
        }

        if (!startingCityPositionsByRegion.TryGetValue(leaderBiomeConfig.startingCityRegion, out List<Vector2Int> positions))
        {
            positions = new List<Vector2Int>();
            startingCityPositionsByRegion[leaderBiomeConfig.startingCityRegion] = positions;
        }

        positions.Add(position);
    }

    // Minimum hex distance enforced between starting-nation anchor cities. Scaled to the
    // board so the three anchors land in different thirds of the map; never so large the
    // map can't satisfy it (callers relax gracefully when it can't be met).
    private float GetMinStartSeparation()
    {
        if (board == null) return 0f;
        int minDim = Mathf.Min(board.GetWidth(), board.GetHeight());
        return Mathf.Max(4f, minDim / 3f);
    }

    private List<TerrainEnum> BuildTerrainPreferenceOrder(TerrainEnum primary)
    {
        List<TerrainEnum> order = new(StartFallbackTerrains.Length + 1) { primary };
        foreach (TerrainEnum terrain in StartFallbackTerrains)
        {
            if (terrain != primary) order.Add(terrain);
        }
        return order;
    }

    // Cluster a leader onto its starting city. PROXIMITY TO THE ANCHOR DOMINATES TERRAIN:
    // a leader one hex from its own city on the "wrong" terrain is correct; the configured
    // terrain across the map (next to a rival's city) is the "Sauron starts in Orthanc" bug.
    // That bug happened because the old code returned the closest hex of the first preferred
    // terrain that had any free hex — and rare terrains like wastelands spawn in scattered
    // patches, so the only free wastelands hex could be beside another nation's anchor while
    // grassland sat one step from Barad-dur. We now search every land terrain at once, pick
    // the genuinely nearest available hex, and use the configured terrain order only to break
    // ties between hexes the same distance from the anchor.
    private Vector2Int SelectClosestPosition(LeaderBiomeConfig config, Vector2Int target, out TerrainEnum chosenTerrain)
    {
        const float epsilon = 0.0001f;
        Vector3Int targetCube = GetCachedCubeCoordinate(target);
        List<TerrainEnum> preference = BuildTerrainPreferenceOrder(config.terrain);

        bool found = false;
        Vector2Int best = default;
        float bestDist = float.MaxValue;
        int bestRank = int.MaxValue;
        TerrainEnum bestTerrain = config.terrain;

        foreach (TerrainEnum terrain in LandTerrains)
        {
            List<Vector2Int> available = GetAvailableHexes(terrain, config.feature);
            if (available.Count == 0) continue;

            int rank = preference.IndexOf(terrain);
            if (rank < 0) rank = preference.Count; // non-preferred land terrain: ranked last, still eligible

            foreach (Vector2Int candidate in available)
            {
                float d = CubeDistance(GetCachedCubeCoordinate(candidate), targetCube);
                // Closer always wins; only at (near-)equal distance does terrain rank decide.
                bool closer = d < bestDist - epsilon;
                bool tieBetterTerrain = Mathf.Abs(d - bestDist) <= epsilon && rank < bestRank;
                if (!found || closer || tieBetterTerrain)
                {
                    found = true;
                    best = candidate;
                    bestDist = d;
                    bestRank = rank;
                    bestTerrain = terrain;
                }
            }
        }

        if (!found)
            throw new Exception($"No suitable hexes found for '{config.characterName}' near its starting city.");

        if (bestTerrain != config.terrain)
            Debug.LogWarning($"Placing '{config.characterName}' on {bestTerrain} (its {config.terrain} hexes near the anchor are taken/unavailable); staying next to its own city.");

        chosenTerrain = bestTerrain;
        return best;
    }

    // Pick a well-separated position. Separation wins over terrain: a starting city on the
    // "wrong" terrain far from its neighbours is better than the correct terrain stacked on
    // top of another nation. Only relaxes separation when no terrain can satisfy it.
    private Vector2Int SelectSpreadPosition(LeaderBiomeConfig config, List<Vector2Int> placedPositions, float minSeparation, out TerrainEnum chosenTerrain)
    {
        if (minSeparation > 0f)
        {
            foreach (TerrainEnum terrain in BuildTerrainPreferenceOrder(config.terrain))
            {
                List<Vector2Int> available = GetAvailableHexes(terrain, config.feature);
                if (available.Count == 0) continue;
                List<Vector2Int> separated = FilterBySeparation(available, placedPositions, minSeparation);
                if (separated.Count == 0) continue;
                if (terrain != config.terrain)
                    Debug.LogWarning($"Relaxing terrain to {terrain} for '{config.characterName}' to keep starting nations apart.");
                chosenTerrain = terrain;
                return FindFarthestPosition(separated, placedPositions);
            }
            Debug.LogWarning($"Could not honor minimum start separation ({minSeparation}) for '{config.startingCityName ?? config.characterName}'; placing as far as the map allows.");
        }

        foreach (TerrainEnum terrain in BuildTerrainPreferenceOrder(config.terrain))
        {
            List<Vector2Int> available = GetAvailableHexes(terrain, config.feature);
            if (available.Count == 0) continue;
            chosenTerrain = terrain;
            return FindFarthestPosition(available, placedPositions);
        }
        throw new Exception($"No suitable hexes found for '{config.characterName}' with terrain {config.terrain} (including fallbacks).");
    }

    private List<Vector2Int> FilterBySeparation(List<Vector2Int> candidates, List<Vector2Int> placedPositions, float minSeparation)
    {
        if (placedPositions == null || placedPositions.Count == 0) return candidates;

        List<Vector2Int> result = new(candidates.Count);
        foreach (Vector2Int candidate in candidates)
        {
            Vector3Int candidateCube = GetCachedCubeCoordinate(candidate);
            bool farEnough = true;
            foreach (Vector2Int placed in placedPositions)
            {
                if (CubeDistance(candidateCube, GetCachedCubeCoordinate(placed)) < minSeparation)
                {
                    farEnough = false;
                    break;
                }
            }
            if (farEnough) result.Add(candidate);
        }
        return result;
    }

    // Self-check: a playable leader must never end up closer to a rival nation's starting
    // city than to its own. If it does, the separation pass failed for this map and we log
    // loudly rather than letting "Sauron starts in Hobbiton" ship silently.
    private void VerifyStartingPlacements()
    {
        if (ownerlessAnchorPositionByRegion.Count == 0) return;

        float minSeparation = GetMinStartSeparation();
        List<Vector2Int> anchors = ownerlessAnchorPositionByRegion.Values.ToList();
        for (int i = 0; i < anchors.Count; i++)
        {
            for (int j = i + 1; j < anchors.Count; j++)
            {
                float d = CubeDistance(GetCachedCubeCoordinate(anchors[i]), GetCachedCubeCoordinate(anchors[j]));
                if (d < minSeparation * 0.5f)
                    Debug.LogWarning($"NationSpawner: two starting cities spawned only {d} hexes apart (target >= {minSeparation}). Map terrain is unusually constrained.");
            }
        }

        // Anchor positions for reference.
        foreach (KeyValuePair<string, Vector2Int> a in ownerlessAnchorPositionByRegion)
            Debug.Log($"[Placement] anchor region '{a.Key}' city at {a.Value}.");

        // Snapshot every city on the board (name + position) once, for the per-leader report.
        List<KeyValuePair<string, Vector2Int>> allCities = new();
        foreach (Hex h in board.hexes.Values)
        {
            if (h == null) continue;
            PC pc = h.GetPCData();
            if (pc == null) continue;
            allCities.Add(new KeyValuePair<string, Vector2Int>(string.IsNullOrWhiteSpace(pc.pcName) ? "(unnamed)" : pc.pcName, h.v2));
        }

        foreach (PlayableLeader leader in FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None))
        {
            if (leader == null || leader.hex == null) continue;
            string region = leader.GetBiome()?.startingCityRegion;
            Vector3Int leaderCube = GetCachedCubeCoordinate(leader.hex.v2);

            string ownInfo = "(no anchor registered)";
            if (!string.IsNullOrWhiteSpace(region) && ownerlessAnchorPositionByRegion.TryGetValue(region, out Vector2Int ownAnchor))
            {
                float distToOwn = CubeDistance(leaderCube, GetCachedCubeCoordinate(ownAnchor));
                ownInfo = $"own region '{region}' anchor at {ownAnchor} (dist {distToOwn})";
            }

            // Three nearest cities to this leader, to expose any foreign city sitting on top of it.
            string nearest = string.Join(", ", allCities
                .Select(c => new { c.Key, Dist = CubeDistance(leaderCube, GetCachedCubeCoordinate(c.Value)) })
                .OrderBy(c => c.Dist)
                .Take(3)
                .Select(c => $"{c.Key}@{c.Dist}"));

            Debug.Log($"[Placement] PLAYABLE '{leader.characterName}' at {leader.hex.v2}; {ownInfo}; nearest cities: {nearest}.");
        }
    }

    private List<Vector2Int> GetAvailableHexes(TerrainEnum terrain, FeaturesEnum feature)
    {
        List<Vector2Int> suitableHexes = GetCachedHexesWithTerrain(terrain, feature);

        if (suitableHexes.Count == 0)
        {
            return new List<Vector2Int>();
        }

        return suitableHexes
            .Where(pos => board.hexes.TryGetValue(pos, out Hex h) && !h.HasAnyPC() && (h.characters == null || h.characters.Count == 0))
            .ToList();
    }

    private List<Vector2Int> GetCachedHexesWithTerrain(TerrainEnum terrain, FeaturesEnum feature)
    {
        if (!terrainHexCache.TryGetValue(terrain, out List<Vector2Int> suitableTerrain))
        {
            return new List<Vector2Int>();
        }

        List<Vector2Int> suitableFeature = suitableTerrain;
        switch (feature)
        {
            case FeaturesEnum.river:
            case FeaturesEnum.lake:
                suitableFeature = featuresHexCache[feature];
                break;
        }

        List<Vector2Int> union = suitableTerrain.Intersect(suitableFeature).ToList();
        if (union.Count < 1)
        {
            Debug.LogWarning($"Could not get hexes that have both {terrain.ToString()} and {feature.ToString()}. Ignoring terrain restriction.");
            union = suitableFeature;
        }

        return union;
    }

    private bool HasNeighboringWater(Hex hex)
    {
        if (hex == null || board == null) return false;
        if (hex.IsWaterTerrain()) return true;

        var neighbors = ((hex.v2.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int pos = new(hex.v2.x + neighbors[i].x, hex.v2.y + neighbors[i].y);
            if (board.hexes.TryGetValue(pos, out Hex neighbor) && neighbor != null && neighbor.IsWaterTerrain())
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveWarshipsFromLeaderArmiesAtHex(Leader leader, Hex hex)
    {
        if (leader == null || hex == null || hex.armies == null) return;
        for (int i = 0; i < hex.armies.Count; i++)
        {
            Army army = hex.armies[i];
            if (army == null || army.commander == null) continue;
            if (army.commander.GetOwner() != leader) continue;
            if (army.ws > 0) army.ws = 0;
        }
    }

    private Vector2Int FindFarthestPosition(List<Vector2Int> candidates, List<Vector2Int> existingPositions)
    {
        if (existingPositions.Count == 0)
        {
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        Vector2Int bestPosition = Vector2Int.zero;
        float maxMinDistance = -1;

        // Pre-calculate cube coordinates for existing positions
        var existingCubes = new Vector3Int[existingPositions.Count];
        for (int i = 0; i < existingPositions.Count; i++)
        {
            existingCubes[i] = GetCachedCubeCoordinate(existingPositions[i]);
        }

        foreach (var candidate in candidates)
        {
            float minDistance = float.MaxValue;
            var candidateCube = GetCachedCubeCoordinate(candidate);

            foreach (var existingCube in existingCubes)
            {
                float distance = CubeDistance(candidateCube, existingCube);
                minDistance = Mathf.Min(minDistance, distance);
            }

            if (minDistance > maxMinDistance)
            {
                maxMinDistance = minDistance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    private static Dictionary<string, string> BuildTutorialAnchors(List<LeaderBiomeConfig> leaderBiomes)
    {
        Dictionary<string, string> anchors = new(StringComparer.OrdinalIgnoreCase);
        if (leaderBiomes == null) return anchors;

        foreach (LeaderBiomeConfig leader in leaderBiomes)
        {
            if (leader == null || string.IsNullOrWhiteSpace(leader.characterName)) continue;
            if (leader.tutorialAnchors == null || leader.tutorialAnchors.Count == 0) continue;

            foreach (string nplName in leader.tutorialAnchors)
            {
                if (string.IsNullOrWhiteSpace(nplName)) continue;
                if (!anchors.ContainsKey(nplName))
                {
                    anchors[nplName] = leader.characterName;
                }
            }
        }

        return anchors;
    }

    private Vector3Int GetCachedCubeCoordinate(Vector2Int offset)
    {
        if (!cubeCoordinateCache.TryGetValue(offset, out var cube))
        {
            cube = OffsetToCube(offset);
            cubeCoordinateCache[offset] = cube;
        }
        return cube;
    }

    private float CubeDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2f;
    }

    private Vector3Int OffsetToCube(Vector2Int offset)
    {
        int x = offset.x;
        int z = offset.y - (offset.x - (offset.x & 1)) / 2;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }
}
