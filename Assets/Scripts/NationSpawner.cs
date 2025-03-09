using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Board))]
public class NationSpawner : MonoBehaviour
{
    public Board board;

    private List<Leader> leaders;

    public void Initialize(Board board)
    {
        leaders = new();
        this.board = board;

        PlayableLeaders playableLeaders = FindFirstObjectByType<PlayableLeaders>();
        playableLeaders.Initialize();
        NonPlayableLeaders nonPlayableLeaders = FindFirstObjectByType<NonPlayableLeaders>();
        nonPlayableLeaders.Initialize();
        leaders.AddRange(playableLeaders.playableLeaders);
        leaders.AddRange(nonPlayableLeaders.nonPlayableLeaders);
    }

    public void Spawn()
    {
        if (board == null)
        {
            throw new System.Exception("Board reference is null. Did you call Initialize?");
        }

        if (leaders == null)
        {
            throw new System.Exception("Players reference is null. Check if Players component exists.");
        }

        // Get all BiomeConfig fields from Leaders
        FieldInfo biomeField = typeof(Leader).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(BiomeConfig)).First();

        // List to track placed player positions
        List<Vector2Int> placedPositions = new List<Vector2Int>();

        // For each player field, find a suitable location
        foreach(Leader leader in leaders)
        {
            BiomeConfig biomeConfig = biomeField.GetValue(leader) as BiomeConfig;

            // Find suitable hexes for this terrain type
            List<Vector2Int> suitableHexes = FindHexesWithTerrain(biomeConfig.terrain);

            if (suitableHexes.Count == 0)
            {
                throw new System.Exception($"No suitable hexes found for {biomeField.Name} with terrain {biomeConfig.terrain}. Skipping.");
            }

            // Find the hex that is farthest from all other players
            Vector2Int bestPosition = FindFarthestPosition(suitableHexes, placedPositions);
            placedPositions.Add(bestPosition);

            // Log the placement
            // Debug.Log($"Placed player {field.Name} at position ({bestPosition.x}, {bestPosition.y}) with terrain {biomeConfig.terrain}");

            // Place
            Vector2Int v2 = new (bestPosition.x, bestPosition.y);
            Hex hex = board.hexes[v2];
            hex.SpawnCapitalAtStart(leader, v2);

            // Reveal area
            leader.RefreshVisibleHexes();
        }
        
    }

    private List<Vector2Int> FindHexesWithTerrain(TerrainEnum terrain)
    {
        List<Vector2Int> matchingHexes = new List<Vector2Int>();

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