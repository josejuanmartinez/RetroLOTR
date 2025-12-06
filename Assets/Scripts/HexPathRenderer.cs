using System;
using System.Collections.Generic;
using System.Linq;
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
    public static int MAX_REACHABLE_HEXES = 631; // MAX_MOVEMENT OF 14, hex, supposing all cost 1 => 1 + 6 * (n(n+1)/2)

    private LineRenderer lineRenderer;
    private Board board;
    private Dictionary<(Character, int), HashSet<Vector2Int>> rangeCache = new();
    private Dictionary<Vector2Int, bool> waterTerrainCache = new();
    private Dictionary<(Vector2Int, Character), float> terrainCostCache = new();

    void Start()
    {
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

    public void DrawPathBetweenHexes(Vector2Int from, Vector2Int to, Character character)
    {
        // Round the input coordinates to ensure they match actual hex positions
        Vector2Int fromRounded = new (Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y));
        Vector2Int toRounded = new (Mathf.RoundToInt(to.x), Mathf.RoundToInt(to.y));

        // Find the path using A* algorithm
        List<Vector2Int> path = FindPath(fromRounded, toRounded, character);

        lineRenderer.positionCount = 0;
        lineRenderer.SetPositions(new Vector3[] { });

        // Check if path exists
        if (path == null || path.Count == 0) return;

        int movementLeft = character.GetMovementLeft();
        // Set the line renderer positions
        lineRenderer.positionCount = path.Count;

        bool wasInWater = IsWaterTerrain(from);

        // Convert hex coordinates to world positions
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int hexPos = path[i];
            bool isInWater = IsWaterTerrain(hexPos);
            if (board.hexes.TryGetValue(hexPos, out Hex hexObj))
            {
                // Get the world position of the hex center
                Vector3 worldPos = hexObj.transform.position;
                lineRenderer.SetPosition(i, worldPos);
                if(i != 0)
                {
                    if(wasInWater != isInWater)
                    { 
                        movementLeft -= movementLeft;
                    } else
                    {
                        movementLeft -= hexObj.GetTerrainCost(character);
                    }
                }
                
                hexObj.ShowMovementLeft(movementLeft, character);
            }
            else
            {
                Debug.LogError($"Hex at position {hexPos} not found in board.hexes dictionary!");
                HidePath();
            }
        }
    }

    public List<Vector2Int> FindPath(Vector2Int startPos, Vector2Int goalPos, Character character)
    {
        int movementLeft = character.GetMovementLeft();
        if (movementLeft < 1) return new List<Vector2Int> { };
        // If start and goal are the same, return just that position
        if (startPos == goalPos) return new List<Vector2Int> { startPos };

        var openSet = new List<Vector2Int>();
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();
        var hasTransition = new Dictionary<Vector2Int, bool>();

        // Initialize the starting position
        openSet.Add(startPos);
        gScore[startPos] = 0;
        fScore[startPos] = HexDistance(startPos, goalPos);
        hasTransition[startPos] = false;

        // Track the best path so far
        Vector2Int bestEnd = startPos;
        float bestDistanceToGoal = HexDistance(startPos, goalPos);
        bool foundTransitionPath = false;
        bool foundNonTransitionPath = false;

        while (openSet.Count > 0)
        {
            // Find hex with lowest fScore in openSet
            Vector2Int current = openSet[0];
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
            if (current == goalPos)
            {
                return ReconstructPath(cameFrom, current, startPos);
            }

            // Check if this could be a better endpoint
            float distanceToGoal = HexDistance(current, goalPos);
            bool nodeHasTransition = hasTransition[current];

            // Update best path logic:
            // 1. If we haven't found any transition path yet, update bestEnd
            // 2. If this is a non-transition path and we don't have one yet, prefer it
            // 3. If types match (both transition or both non-transition), prefer closer to goal
            if ((!foundTransitionPath && !foundNonTransitionPath) ||
                (!nodeHasTransition && !foundNonTransitionPath) ||
                (nodeHasTransition == foundTransitionPath && nodeHasTransition == foundNonTransitionPath && distanceToGoal < bestDistanceToGoal))
            {
                bestEnd = current;
                bestDistanceToGoal = distanceToGoal;

                if (nodeHasTransition)
                    foundTransitionPath = true;
                else
                    foundNonTransitionPath = true;
            }

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

                // Calculate movement cost
                float terrainCost = GetTerrainCost(neighbor, character);
                float tentativeGScore = gScore[current] + terrainCost;

                // Check if this would exceed movement
                bool isFirstStep = current == startPos;
                if (tentativeGScore > movementLeft && !isFirstStep) continue;
                if (tentativeGScore > movementLeft && isFirstStep)
                {
                    // Allow one costly step by spending all remaining movement
                    tentativeGScore = movementLeft;
                }

                // Special case: Allow transition if neighbor is the goal position
                bool isGoalPosition = neighbor == goalPos;

                // If this is a transition hex and NOT the goal, mark it as an endpoint candidate but don't extend the path
                if (isTerrainTransition && !isGoalPosition)
                {
                    // Update the neighbor's path information
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;

                    // Track it as a potential best endpoint
                    float distanceToGoalNeighbor = HexDistance(neighbor, goalPos);
                    if (!foundTransitionPath || distanceToGoalNeighbor < bestDistanceToGoal)
                    {
                        bestEnd = neighbor;
                        bestDistanceToGoal = distanceToGoalNeighbor;
                        foundTransitionPath = true;
                    }

                    // Don't add to openSet - this prevents the path from extending beyond this hex
                    continue;
                }

                // For non-transition hexes or if it's the goal position, continue as normal
                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
                else if (gScore.TryGetValue(neighbor, out float neighborG) && tentativeGScore >= neighborG)
                {
                    continue;
                }

                // Update path information
                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + HexDistance(neighbor, goalPos);
                hasTransition[neighbor] = hasTransition[current] || isTerrainTransition;
            }
        }

        // If we couldn't reach the goal but found a valid path
        if (bestEnd != startPos)
        {
            return ReconstructPath(cameFrom, bestEnd, startPos);
        }

        // No path found
        return new List<Vector2Int> { };
    }

    // Helper method to check if a hex is water terrain
    private bool IsWaterTerrain(Vector2Int position)
    {
        if (waterTerrainCache.TryGetValue(position, out bool isWater))
        {
            return isWater;
        }

        if (board.hexes.TryGetValue(position, out var hex))
        {
            isWater = hex.IsWaterTerrain();
            waterTerrainCache[position] = isWater;
            return isWater;
        }
        return false;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current, Vector2Int startPos)
    {
        var path = new List<Vector2Int> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        // Make sure the starting position is included
        if (path.Count == 0 || path[0] != startPos)
        {
            path.Insert(0, startPos);
        }

        return path;
    }

    private List<Vector2Int> GetNeighbors(Vector2Int hexPos)
    {
        var neighbors = new List<Vector2Int>();
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
            Vector2Int neighborPos = new Vector2Int(x + dir.x, y + dir.y);

            // Only add if the hex exists on the board
            if (board.hexes.ContainsKey(neighborPos)) neighbors.Add(neighborPos);
        }

        return neighbors;
    }

    // Get the terrain cost for a hex
    private float GetTerrainCost(Vector2Int hexPos, Character character)
    {
        var cacheKey = (hexPos, character);
        if (terrainCostCache.TryGetValue(cacheKey, out float cost))
        {
            return cost;
        }

        if (board.hexes.TryGetValue(hexPos, out Hex hex))
        {
            cost = hex.GetTerrainCost(character);
            terrainCostCache[cacheKey] = cost;
            return cost;
        }

        return 1f; // Default cost if terrain can't be determined
    }

    // Calculate the hex distance (in a hex grid)
    private float HexDistance(Vector2Int a, Vector2Int b)
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

    private Vector3 OffsetToCube(Vector2Int hex)
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
        FindObjectsByType<MovementCostManager>(FindObjectsSortMode.None).ToList().ForEach((x) => x.Hide()); 
        lineRenderer.SetPositions(new Vector3[] { });
        lineRenderer.positionCount = 0;
    }

    public HashSet<Vector2Int> FindAllHexesV2InRange(Character character)
    {
        Vector2Int startPos = character.hex.v2;
        int maxMovement = character.GetMaxMovement();

        // Check cache first
        var cacheKey = (character, maxMovement);
        if (rangeCache.TryGetValue(cacheKey, out var cachedRange))
        {
            return cachedRange;
        }

        var reachableHexes = new HashSet<Vector2Int>();
        var openSet = new List<Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();

        openSet.Add(startPos);
        reachableHexes.Add(startPos);
        gScore[startPos] = 0;

        while (openSet.Count > 0)
        {
            // Find node with lowest cost
            Vector2Int current = openSet[0];
            float lowestGScore = gScore[current];
            int currentIndex = 0;

            for (int i = 1; i < openSet.Count; i++)
            {
                if (gScore[openSet[i]] < lowestGScore)
                {
                    lowestGScore = gScore[openSet[i]];
                    current = openSet[i];
                    currentIndex = i;
                }
            }

            // Remove current hex efficiently
            openSet[currentIndex] = openSet[openSet.Count - 1];
            openSet.RemoveAt(openSet.Count - 1);

            // Get neighbors
            var neighbors = GetNeighbors(current);
            foreach (var neighbor in neighbors)
            {
                if (!board.hexes.ContainsKey(neighbor)) continue;

                bool isCurrentWater = IsWaterTerrain(current);
                bool isNeighborWater = IsWaterTerrain(neighbor);
                bool isTerrainTransition = isCurrentWater != isNeighborWater;

                float terrainCost = GetTerrainCost(neighbor, character);
                float tentativeGScore = gScore[current] + terrainCost;

                if (tentativeGScore > maxMovement) continue;
                bool isFirstStep = current == startPos;
                if (tentativeGScore > maxMovement && !isFirstStep) continue;
                if (tentativeGScore > maxMovement && isFirstStep)
                {
                    tentativeGScore = maxMovement;
                }

                if (isTerrainTransition)
                {
                    if (!reachableHexes.Contains(neighbor))
                    {
                        reachableHexes.Add(neighbor);
                        gScore[neighbor] = tentativeGScore;
                    }
                    continue;
                }

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    gScore[neighbor] = tentativeGScore;

                    if (!reachableHexes.Contains(neighbor))
                    {
                        reachableHexes.Add(neighbor);
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        // Cache the result
        rangeCache[cacheKey] = reachableHexes;
        return reachableHexes;
    }

    public List<Hex> FindAllHexesInRange(Character character)
    {
        var v2Hexes = FindAllHexesV2InRange(character);
        var result = new List<Hex>(v2Hexes.Count);
        foreach (var v2 in v2Hexes)
        {
            if (board.hexes.TryGetValue(v2, out var hex))
            {
                result.Add(hex);
            }
        }
        return result;
    }

    public void ClearCache()
    {
        rangeCache.Clear();
        waterTerrainCache.Clear();
        terrainCostCache.Clear();
    }

    public float GetPathCost(Vector2Int startHex, Vector2Int endHex, Character character)
    {
        // Find the path first
        List<Vector2Int> path = FindPath(startHex, endHex, character);

        // If there's no valid path, return an impossible cost (or -1 if you prefer)
        if (path == null || path.Count < 2)
        {
            return -1f;
        }

        float totalCost = 0f;

        // Sum terrain costs between each hex in the path
        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int currentHex = path[i];
            totalCost += GetTerrainCost(currentHex, character);
        }

        return totalCost;
    }
}
