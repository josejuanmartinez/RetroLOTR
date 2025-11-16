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
    }

    private void InstantiateLeadersAndCharacters(List<LeaderBiomeConfig> leaderBiomes, List<Vector2Int> placedPositions)
    {
        foreach (LeaderBiomeConfig leaderBiomeConfig in leaderBiomes)
        {
            InstantiateLeaderAndCharacters(leaderBiomeConfig, placedPositions, true);
        }
    }

    private void InstantiateLeadersAndCharacters(List<NonPlayableLeaderBiomeConfig> nonPlayableleaderBiomes, List<Vector2Int> placedPositions)
    {
        foreach (NonPlayableLeaderBiomeConfig nonPlayableleaderBiomeConfig in nonPlayableleaderBiomes)
        {
            InstantiateLeaderAndCharacters(nonPlayableleaderBiomeConfig, placedPositions, false);
        }
    }
    
    private void InstantiateLeaderAndCharacters(LeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, bool isPlayable)
    {
        /*if (FindObjectsByType<Leader>(FindObjectsSortMode.None).Length >= Game.MAX_LEADERS)
        {
            Debug.LogWarning("Max leaders reached. Skipping leader instantiation.");
            return;
        }*/
        List<Vector2Int> suitableHexes = GetCachedHexesWithTerrain(leaderBiomeConfig.terrain, leaderBiomeConfig.feature);

        if (suitableHexes.Count == 0)
        {
            throw new Exception($"No suitable hexes found with terrain {leaderBiomeConfig.terrain}.");
        }

        Vector2Int bestPosition = FindFarthestPosition(suitableHexes, placedPositions);
        placedPositions.Add(bestPosition);

        Vector2Int v2 = new(bestPosition.x, bestPosition.y);
        Hex hex = board.hexes[v2];

        if (!EnsureCharacterCapacity("Skipping leader instantiation."))
            return;

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
            return;
        }

        currentCharacterCount++;

        foreach (var character in leader.GetBiome().startingCharacters)
        {
            if (!EnsureCharacterCapacity("Skipping leader instantiation."))
                return;

            characterInstantiator.InstantiateCharacter(leader, hex, character);
            currentCharacterCount++;
        }

        if (!EnsurePcCapacity())
            return;

        PC pc = new(leader, hex);
        hex.SetPC(pc);
        currentPcCount++;
    }

    private List<Vector2Int> GetCachedHexesWithTerrain(TerrainEnum terrain, FeaturesEnum feature)
    {
        List<Vector2Int> suitableTerrain = terrainHexCache[terrain];
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
