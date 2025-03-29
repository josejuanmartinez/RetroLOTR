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
        if (movementLeft < 1) return new List<Vector2> { };
        // If start and goal are the same, return just that position
        if (startPos == goalPos) return new List<Vector2> { startPos };

        var openSet = new List<Vector2>();
        var closedSet = new HashSet<Vector2>();
        var cameFrom = new Dictionary<Vector2, Vector2>();
        var gScore = new Dictionary<Vector2, float>();
        var fScore = new Dictionary<Vector2, float>();
        var hasTransition = new Dictionary<Vector2, bool>();

        // Initialize the starting position
        openSet.Add(startPos);
        gScore[startPos] = 0;
        fScore[startPos] = HexDistance(startPos, goalPos);
        hasTransition[startPos] = false;

        // Determine if the start position is water
        bool isStartWater = IsWaterTerrain(startPos);

        // Track the best path so far
        Vector2 bestEnd = startPos;
        float bestDistanceToGoal = HexDistance(startPos, goalPos);
        bool foundTransitionPath = false;
        bool foundNonTransitionPath = false;

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
                if (tentativeGScore > movementLeft) continue;

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
        return new List<Vector2> { };
    }

    // Helper method to check if a hex is water terrain
    private bool IsWaterTerrain(Vector2 position)
    {
        if (board.hexes.TryGetValue(position, out var hex)) return hex.IsWaterTerrain();
        return false;
    }

    private List<Vector2> ReconstructPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current, Vector2 startPos)
    {
        var path = new List<Vector2> { current };

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

    public HashSet<Vector2> FindAllHexesV2InRange(Character character)
    {
        Vector2 startPos = character.hex.v2;
        int maxMovement = character.GetMaxMovement();

        // Use a HashSet for O(1) lookups and to ensure no duplicates
        var reachableHexes = new HashSet<Vector2>();
        var openSet = new List<Vector2>();
        var gScore = new Dictionary<Vector2, float>();

        // Add starting position
        openSet.Add(startPos);
        reachableHexes.Add(startPos);
        gScore[startPos] = 0;

        // Determine if the start position is water
        bool isStartWater = IsWaterTerrain(startPos);

        while (openSet.Count > 0)
        {
            // Find the node with the lowest cost so far
            Vector2 current = openSet[0];
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

            // Remove the current hex from openSet for efficiency
            // (faster than Remove which searches the entire list)
            openSet[currentIndex] = openSet[openSet.Count - 1];
            openSet.RemoveAt(openSet.Count - 1);

            // Check all neighbors
            foreach (var neighbor in GetNeighbors(current))
            {
                // Skip if not on the board
                if (!board.hexes.ContainsKey(neighbor)) continue;

                // Check terrain transition (land to water or water to land)
                bool isCurrentWater = IsWaterTerrain(current);
                bool isNeighborWater = IsWaterTerrain(neighbor);
                bool isTerrainTransition = isCurrentWater != isNeighborWater;

                // Calculate movement cost to reach this neighbor
                float terrainCost = GetTerrainCost(neighbor, character);
                float tentativeGScore = gScore[current] + terrainCost;

                // Check if this would exceed movement
                if (tentativeGScore > maxMovement) continue;

                // If this is a terrain transition (water to land or land to water)
                // We mark it as reachable but don't expand from it (just like in FindPath)
                if (isTerrainTransition)
                {
                    if (!reachableHexes.Contains(neighbor))
                    {
                        reachableHexes.Add(neighbor);
                        gScore[neighbor] = tentativeGScore;
                    }
                    continue; // Don't expand beyond a transition hex
                }

                // For regular hexes, continue expanding
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    // This path to neighbor is better than any previous one
                    gScore[neighbor] = tentativeGScore;

                    if (!reachableHexes.Contains(neighbor))
                    {
                        reachableHexes.Add(neighbor);
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return reachableHexes;
    }

    public List<Hex> FindAllHexesInRange(Character character)
    {
        return FindAllHexesV2InRange(character).Select(v2 => board.hexes[v2]).ToList();
    }


}