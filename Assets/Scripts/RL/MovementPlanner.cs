using UnityEngine;

/// <summary>
/// Handles movement planning and pathfinding strategies
/// </summary>
public class MovementPlanner
{
    private readonly GameState gameState;
    private readonly Board board;

    public MovementPlanner(GameState gameState, Board board)
    {
        this.gameState = gameState;
        this.board = board;
    }

    /// <summary>
    /// Plans movement towards target hex using specified strategy
    /// </summary>
    public MovementResult PlanMovement(Hex targetHex, Character character, MovementStrategy strategy)
    {
        if (targetHex == null || character?.reachableHexes == null || character.reachableHexes.Count == 0)
        {
            return new MovementResult { Success = false };
        }

        Hex destinationHex = strategy switch
        {
            MovementStrategy.Direct => gameState.FindDirectPathHex(targetHex, character),
            MovementStrategy.Cautious => gameState.FindCautiousPathHex(targetHex, character),
            MovementStrategy.Aggressive => gameState.FindAggressivePathHex(targetHex, character),
            _ => gameState.FindDirectPathHex(targetHex, character)
        };

        if (destinationHex == null)
        {
            return new MovementResult { Success = false };
        }

        float previousDistance = Vector2Int.Distance(character.hex.v2, targetHex.v2);
        float newDistance = Vector2Int.Distance(destinationHex.v2, targetHex.v2);

        return new MovementResult
        {
            Success = true,
            DestinationHex = destinationHex,
            PreviousDistance = previousDistance,
            NewDistance = newDistance,
            DistanceImproved = newDistance < previousDistance
        };
    }

    /// <summary>
    /// Executes the planned movement
    /// </summary>
    public void ExecuteMovement(Character character, Hex destinationHex)
    {
        board.MoveCharacterOneHex(character, character.hex, destinationHex, true);
    }
}

public enum MovementStrategy
{
    Direct = 0,
    Cautious = 1,
    Aggressive = 2
}

public struct MovementResult
{
    public bool Success;
    public Hex DestinationHex;
    public float PreviousDistance;
    public float NewDistance;
    public bool DistanceImproved;
}