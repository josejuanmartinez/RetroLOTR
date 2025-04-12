using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Board))]
public class NationSpawner : MonoBehaviour
{
    public Board board;

    private PlayableLeaders playableLeaders;
    private NonPlayableLeaders nonPlayableLeaders;
    
    // List to track placed player positions
    private List<Vector2Int> placedPositions = new();
    private CharacterInstantiator characterInstantiator;

    public void Initialize(Board board)
    {
        this.board = board;
        characterInstantiator = FindFirstObjectByType<CharacterInstantiator>();

        playableLeaders = FindFirstObjectByType<PlayableLeaders>();
        playableLeaders.Initialize();
        nonPlayableLeaders = FindFirstObjectByType<NonPlayableLeaders>();
        nonPlayableLeaders.Initialize();
    }

    public void Spawn()
    {
        InstantiateLeadersAndCharacters(playableLeaders.playableLeaders.biomes, placedPositions);
        InstantiateLeadersAndCharacters(nonPlayableLeaders.nonPlayableLeaders.biomes, placedPositions);
    }

    private void InstantiateLeadersAndCharacters(List<LeaderBiomeConfig> leaderBiomes, List<Vector2Int> placedPositions)
    {
        foreach (LeaderBiomeConfig leaderBiomeConfig in leaderBiomes) InstantiateLeaderAndCharacters(leaderBiomeConfig, placedPositions, true);
    }
    private void InstantiateLeadersAndCharacters(List<NonPlayableLeaderBiomeConfig> nonPlayableleaderBiomes, List<Vector2Int> placedPositions)
    {
        foreach (NonPlayableLeaderBiomeConfig nonPlayableleaderBiomeConfig in nonPlayableleaderBiomes) InstantiateLeaderAndCharacters(nonPlayableleaderBiomeConfig, placedPositions, false);
    }
    
    private void InstantiateLeaderAndCharacters(LeaderBiomeConfig leaderBiomeConfig, List<Vector2Int> placedPositions, bool isPlayable)
    {
        // Find suitable hexes for this terrain type
        List<Vector2Int> suitableHexes = FindHexesWithTerrain(leaderBiomeConfig.terrain);

        if (suitableHexes.Count == 0)
        {
            throw new System.Exception($"No suitable hexes found with terrain {leaderBiomeConfig.terrain}. Skipping.");
        }

        // Find the hex that is farthest from all other players
        Vector2Int bestPosition = FindFarthestPosition(suitableHexes, placedPositions);
        placedPositions.Add(bestPosition);

        // Log the placement
        // Debug.Log($"Placed player {field.Name} at position ({bestPosition.x}, {bestPosition.y}) with terrain {leaderBiomeConfig.terrain}");

        // Place
        Vector2Int v2 = new(bestPosition.x, bestPosition.y);
        Hex hex = board.hexes[v2];
        
        string leaderName = leaderBiomeConfig.characterName;

        Leader leader = leaderBiomeConfig is NonPlayableLeaderBiomeConfig? characterInstantiator.InstantiateNonPlayableLeader(hex, leaderBiomeConfig as NonPlayableLeaderBiomeConfig) : characterInstantiator.InstantiatePlayableLeader(hex, leaderBiomeConfig);

        leader.GetBiome().startingCharacters.ForEach(x => characterInstantiator.InstantiateCharacter(leader, hex, x));

        PC pc = new (leader, hex);
        hex.SetPC(pc);
    }

    private List<Vector2Int> FindHexesWithTerrain(TerrainEnum terrain)
    {
        List<Vector2Int> matchingHexes = new ();

        if (board.terrainGrid == null)
        {
            Debug.LogError("terrainGrid is null or couldn't be accessed.");
            return matchingHexes;
        }

        // Scan the entire grid for matching terrain
        for (int x = 0; x < board.height; x++)
        {
            for (int y = 0; y < board.width; y++)
            {
                if (board.terrainGrid[x, y] == terrain)
                {
                    matchingHexes.Add(new Vector2Int(x, y));
                }
            }
        }

        return matchingHexes;
    }

    private Vector2Int FindFarthestPosition(List<Vector2Int> candidates, List<Vector2Int> existingPositions)
    {
        // If no existing positions, just pick a random candidate
        if (existingPositions.Count == 0)
        {
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        Vector2Int bestPosition = Vector2Int.zero;
        float maxMinDistance = -1;

        foreach (var candidate in candidates)
        {
            // Calculate minimum distance to any placed player
            float minDistance = float.MaxValue;

            foreach (var position in existingPositions)
            {
                // For hex grid distance, we need to use the correct distance calculation
                float distance = HexDistance(candidate, position);
                minDistance = Mathf.Min(minDistance, distance);
            }

            // If this candidate's minimum distance is greater than our previous best
            if (minDistance > maxMinDistance)
            {
                maxMinDistance = minDistance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    private float HexDistance(Vector2Int a, Vector2Int b)
    {
        // For flat-top hex grids, we need to convert to cube coordinates
        // and then calculate distance

        // Convert to cube coordinates
        Vector3Int aCube = OffsetToCube(a);
        Vector3Int bCube = OffsetToCube(b);

        // Calculate cube distance
        return (Mathf.Abs(aCube.x - bCube.x) +
                Mathf.Abs(aCube.y - bCube.y) +
                Mathf.Abs(aCube.z - bCube.z)) / 2f;
    }

    private Vector3Int OffsetToCube(Vector2Int offset)
    {
        // Convert from odd-row offset to cube coordinates
        int x = offset.x;
        int z = offset.y - (offset.x - (offset.x & 1)) / 2;
        int y = -x - z;

        return new Vector3Int(x, y, z);
    }
}