﻿using System.Collections.Generic;
using UnityEngine;

public enum MovementType
{
    ArmyCommander,
    ArmyCommanderCavalryOnly,
    Character
}

[RequireComponent(typeof(Board), typeof(LineRenderer))]
public class HexPathRenderer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Board board;
    private Game game;

    void Start()
    {
        game = FindFirstObjectByType<Game>();
        board = GetComponent<Board>();
        lineRenderer = GetComponent<LineRenderer>();
        if (board == null)
        {
            Debug.LogError("Board component not found!");
        }
    }
    void Update()
    {
        // Check right mouse button state and update it in the OnHoverTile class
        OnHoverTile.UpdateMouseState(Input.GetMouseButton(1));
    }

    public void DrawPathBetweenHexes(Vector2 from, Vector2 to, Character character)
    {
        // Round the input coordinates to ensure they match actual hex positions
        Vector2 fromRounded = new (Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y));
        Vector2 toRounded = new (Mathf.RoundToInt(to.x), Mathf.RoundToInt(to.y));

        // Find the path using A* algorithm
        List<Vector2> path = FindPath(fromRounded, toRounded, character);

        lineRenderer.positionCount = 0;
        lineRenderer.SetPositions(new Vector3[] { });

        // Check if path exists
        if (path == null || path.Count == 0) return;

        // Set the line renderer positions
        lineRenderer.positionCount = path.Count;

        // Convert hex coordinates to world positions
        for (int i = 0; i < path.Count; i++)
        {
            Vector2 hexPos = path[i];
            if (board.hexes.TryGetValue(hexPos, out Hex hexObj))
            {
                // Get the world position of the hex center
                Vector3 worldPos = hexObj.transform.position;
                lineRenderer.SetPosition(i, worldPos);
            }
            else
            {
                Debug.LogError($"Hex at position {hexPos} not found in board.hexes dictionary!");
            }
        }
    }

    public List<Vector2> FindPath(Vector2 startPos, Vector2 goalPos, Character character)
    {
        int movementLeft = character.GetMovementLeft();
        if(movementLeft < 1) return new List<Vector2> {  };
        // If start and goal are the same, return just that position
        if (startPos == goalPos) return new List<Vector2> {  };

        var openSet = new List<Vector2>();
        var closedSet = new HashSet<Vector2>();
        var cameFrom = new Dictionary<Vector2, Vector2>();
        var gScore = new Dictionary<Vector2, float>();
        var fScore = new Dictionary<Vector2, float>();
        var terrainTransition = new Dictionary<Vector2, bool>();

        // Track if we've crossed from land to water or water to land
        terrainTransition[startPos] = false;

        openSet.Add(startPos);
        gScore[startPos] = 0;
        fScore[startPos] = HexDistance(startPos, goalPos);

        // Determine if the start position is water
        bool isStartWater = IsWaterTerrain(startPos);

        while (openSet.Count > 0)
        {
            // Find hex with lowest fScore in openSet
            Vector2 current = openSet[0];
            float lowestFScore = float.MaxValue;
            if (fScore.TryGetValue(current, out float currentF)) lowestFScore = currentF;

            for (int i = 1; i < openSet.Count; i++)
            {
                if (fScore.TryGetValue(openSet[i], out float f) && f < lowestFScore)
                {
                    current = openSet[i];
                    lowestFScore = f;
                }
            }

            // Check if we've reached the goal
            if (current == goalPos) return ReconstructPath(cameFrom, current);

            // If we've already crossed from land to water or water to land, 
            // and this isn't the goal, we stop the path here
            if (terrainTransition[current] && current != goalPos) return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            // Get all neighbors of the current hex
            foreach (var neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor) || !board.hexes.ContainsKey(neighbor)) continue;

                // Check terrain transition (land to water or water to land)
                bool isCurrentWater = IsWaterTerrain(current);
                bool isNeighborWater = IsWaterTerrain(neighbor);
                bool isTerrainTransition = isCurrentWater != isNeighborWater;

                float terrainCost = GetTerrainCost(neighbor, character);
                float tentativeGScore = gScore[current] + terrainCost;

                // Skip if the movement cost exceeds the maximum limit
                if (tentativeGScore > movementLeft) continue;

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                    terrainTransition[neighbor] = terrainTransition[current] || isTerrainTransition;
                }
                else if (gScore.TryGetValue(neighbor, out float neighborG) && tentativeGScore >= neighborG)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + HexDistance(neighbor, goalPos);
                terrainTransition[neighbor] = terrainTransition[current] || isTerrainTransition;
            }
        }

        // If we can't reach the goal within movement limit, find the closest reachable point
        if (cameFrom.Count > 0)
        {
            Vector2 bestPos = startPos;
            float bestDistance = float.MaxValue;
            foreach (var pos in cameFrom.Keys)
            {
                // Skip any position that would cost more than the available movement
                if (!gScore.TryGetValue(pos, out float positionCost) || positionCost > movementLeft) continue;

                // Don't consider positions after a terrain transition unless it's the goal
                if (terrainTransition[pos] && pos != goalPos) continue;

                float distance = HexDistance(pos, goalPos);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPos = pos;
                }
            }

            // Verify the best position is within movement range before returning
            if (gScore.TryGetValue(bestPos, out float bestPosCost) && bestPosCost <= movementLeft)
            {
                return ReconstructPath(cameFrom, bestPos);
            }
            else
            {
                // If even the best pos exceeds movement, just return the starting position
                return new List<Vector2> { };
            }
        }

        // No path found
        return new List<Vector2> { };
    }

    // Helper method to check if a hex is water terrain
    private bool IsWaterTerrain(Vector2 position)
    {
        if (board.hexes.TryGetValue(position, out var hex))
        {
            return hex.terrainType == TerrainEnum.shallowWater ||
                   hex.terrainType == TerrainEnum.deepWater;
        }
        return false;
    }

    private List<Vector2> ReconstructPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
    {
        var path = new List<Vector2> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        return path;
    }

    private List<Vector2> GetNeighbors(Vector2 hexPos)
    {
        var neighbors = new List<Vector2>();
        Vector2Int[] directionsToCheck;

        // Round to ensure we're using integer coordinates for lookup
        int x = Mathf.RoundToInt(hexPos.x);
        int y = Mathf.RoundToInt(hexPos.y);

        // Determine if we're in an even or odd row
        // We need to check X (not Y) to determine whether to use odd or even directions
        if (x % 2 == 0)
        {
            directionsToCheck = board.evenRowNeighbors;
        }
        else
        {
            directionsToCheck = board.oddRowNeighbors;
        }

        // Add all six neighbors
        foreach (var dir in directionsToCheck)
        {
            Vector2 neighborPos = new Vector2(x + dir.x, y + dir.y);

            // Only add if the hex exists on the board
            if (board.hexes.ContainsKey(neighborPos)) neighbors.Add(neighborPos);
        }

        return neighbors;
    }

    // Get the terrain cost for a hex
    private float GetTerrainCost(Vector2 hexPos, Character character)
    {
        // For now, all terrain types have a cost of 1
        if (board.hexes.TryGetValue(hexPos, out Hex hex))
        {
            if (hex != null) return hex.GetTerrainCost(character);
        }

        return 1f; // Default cost if terrain can't be determined
    }

    // Calculate the hex distance (in a hex grid)
    private float HexDistance(Vector2 a, Vector2 b)
    {
        // Convert to cube coordinates for easier distance calculation
        Vector3 aCube = OffsetToCube(a);
        Vector3 bCube = OffsetToCube(b);

        // The distance in a hex grid is the maximum component distance
        return Mathf.Max(
            Mathf.Abs(aCube.x - bCube.x),
            Mathf.Abs(aCube.y - bCube.y),
            Mathf.Abs(aCube.z - bCube.z)
        );
    }

    private Vector3 OffsetToCube(Vector2 hex)
    {
        int x = Mathf.RoundToInt(hex.x);
        int y = Mathf.RoundToInt(hex.y);

        // For this specific coordinate system with the given direction vectors
        // We can use the standard odd-offset conversion, but adjusted for your grid orientation
        float q = (x - (x & 1)) / 2 + y;
        float r = x;
        float s = -q - r;

        return new Vector3(q, r, s);
    }

    public void HidePath()
    {
        lineRenderer.SetPositions(new Vector3[] { });
        lineRenderer.positionCount = 0;
    }
}