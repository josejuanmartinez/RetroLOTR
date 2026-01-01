using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System;

[RequireComponent(typeof(Board))]
public class NationSpawner : MonoBehaviour
{
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
    private Dictionary<string, string> tutorialAnchorByNpl = new(StringComparer.OrdinalIgnoreCase);

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

        InstantiateLeadersAndCharacters(playableLeaders.playableLeaders.biomes, placedPositions);
        InstantiateLeadersAndCharacters(nonPlayableLeaders.nonPlayableLeaders.biomes, placedPositions);
        PositionPlayableLeadersNearTutorialPcs();
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

    private void InstantiateLeadersAndCharacters(List<NonPlayableLeaderBiomeConfig> nonPlayableleaderBiomes, List<Vector2Int> placedPositions)
    {
        IEnumerable<NonPlayableLeaderBiomeConfig> orderedBiomes = nonPlayableleaderBiomes
            .OrderByDescending(b => !string.IsNullOrWhiteSpace(b.characterName) && tutorialAnchorByNpl.ContainsKey(b.characterName))
            .ThenBy(b => b.characterName);

        foreach (NonPlayableLeaderBiomeConfig nonPlayableleaderBiomeConfig in orderedBiomes)
        {
            Vector2Int? preferredPosition = null;
            if (!string.IsNullOrWhiteSpace(nonPlayableleaderBiomeConfig.characterName) &&
                tutorialAnchorByNpl.TryGetValue(nonPlayableleaderBiomeConfig.characterName, out string anchorName) &&
                leaderPositions.TryGetValue(anchorName, out Vector2Int anchorPosition))
            {
                preferredPosition = anchorPosition;
            }

            Vector2Int? position = nonPlayableleaderBiomeConfig.spawnPcWithoutOwner
                ? InstantiateOwnerlessPc(nonPlayableleaderBiomeConfig, placedPositions, preferredPosition)
                : InstantiateLeaderAndCharacters(nonPlayableleaderBiomeConfig, placedPositions, false, preferredPosition);
            if (position.HasValue && !string.IsNullOrWhiteSpace(nonPlayableleaderBiomeConfig.characterName))
            {
                leaderPositions[nonPlayableleaderBiomeConfig.characterName] = position.Value;
            }
        }
    }
    
    private Vector2Int? InstantiateLeaderAndCharacters(LeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, bool isPlayable, Vector2Int? preferredPosition)
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
        TerrainEnum chosenTerrain = leaderBiomeConfig.terrain;
        List<Vector2Int> suitableHexes = GetAvailableHexes(chosenTerrain, leaderBiomeConfig.feature);

        if (suitableHexes.Count == 0)
        {
            TerrainEnum[] fallbackTerrains = { TerrainEnum.plains, TerrainEnum.grasslands, TerrainEnum.hills, TerrainEnum.shore };
            foreach (var fallbackTerrain in fallbackTerrains)
            {
                if (fallbackTerrain == leaderBiomeConfig.terrain) continue;
                suitableHexes = GetAvailableHexes(fallbackTerrain, leaderBiomeConfig.feature);
                if (suitableHexes.Count > 0)
                {
                    chosenTerrain = fallbackTerrain;
                    Debug.LogWarning($"Falling back to terrain {fallbackTerrain} because all {leaderBiomeConfig.terrain} hexes already have PCs.");
                    break;
                }
            }
        }

        if (suitableHexes.Count == 0)
        {
            throw new Exception($"No suitable hexes found for leader with terrain {leaderBiomeConfig.terrain} (including fallbacks).");
        }

        Vector2Int bestPosition = preferredPosition.HasValue
            ? FindClosestPosition(suitableHexes, preferredPosition.Value)
            : FindFarthestPosition(suitableHexes, placedPositions);
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
        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();
    }

    private Vector2Int? InstantiateOwnerlessPc(NonPlayableLeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, Vector2Int? preferredPosition)
    {
        if (!EnsurePcCapacity())
        {
            string leaderName = string.IsNullOrWhiteSpace(leaderBiomeConfig.characterName) ? "Unknown" : leaderBiomeConfig.characterName;
            Debug.LogError($"Skipping ownerless PC instantiation for {leaderName} because max PCs reached.");
            return null;
        }

        TerrainEnum chosenTerrain = leaderBiomeConfig.terrain;
        List<Vector2Int> suitableHexes = GetAvailableHexes(chosenTerrain, leaderBiomeConfig.feature);

        if (suitableHexes.Count == 0)
        {
            TerrainEnum[] fallbackTerrains = { TerrainEnum.plains, TerrainEnum.grasslands, TerrainEnum.hills, TerrainEnum.shore };
            foreach (var fallbackTerrain in fallbackTerrains)
            {
                if (fallbackTerrain == leaderBiomeConfig.terrain) continue;
                suitableHexes = GetAvailableHexes(fallbackTerrain, leaderBiomeConfig.feature);
                if (suitableHexes.Count > 0)
                {
                    chosenTerrain = fallbackTerrain;
                    Debug.LogWarning($"Falling back to terrain {fallbackTerrain} because all {leaderBiomeConfig.terrain} hexes already have PCs.");
                    break;
                }
            }
        }

        if (suitableHexes.Count == 0)
        {
            throw new Exception($"No suitable hexes found for ownerless PC with terrain {leaderBiomeConfig.terrain} (including fallbacks).");
        }

        Vector2Int bestPosition = preferredPosition.HasValue
            ? FindClosestPosition(suitableHexes, preferredPosition.Value)
            : FindFarthestPosition(suitableHexes, placedPositions);
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

        if (leaderBiomeConfig.startingCharacters != null && leaderBiomeConfig.startingCharacters.Count > 0)
        {
            Debug.LogWarning($"Ownerless PC '{leaderBiomeConfig.startingCityName}' has starting characters configured; skipping those.");
        }

        return bestPosition;
    }

    private List<Vector2Int> GetAvailableHexes(TerrainEnum terrain, FeaturesEnum feature)
    {
        List<Vector2Int> suitableHexes = GetCachedHexesWithTerrain(terrain, feature);

        if (suitableHexes.Count == 0)
        {
            return new List<Vector2Int>();
        }

        return suitableHexes
            .Where(pos => board.hexes.TryGetValue(pos, out Hex h) && !h.HasAnyPC())
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

    private Vector2Int FindClosestPosition(List<Vector2Int> candidates, Vector2Int target)
    {
        Vector2Int bestPosition = candidates[0];
        float minDistance = float.MaxValue;
        Vector3Int targetCube = GetCachedCubeCoordinate(target);

        foreach (var candidate in candidates)
        {
            float distance = CubeDistance(GetCachedCubeCoordinate(candidate), targetCube);
            if (distance < minDistance)
            {
                minDistance = distance;
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
