using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum HexObjectiveType
{
    None,
    AttackEnemy,
    DefendAlly,
    GatherResource,
    SecureTerritory,
    RetrieveArtifact,
    RetreatToSafety
}

/// <summary>
/// Handles evaluation of hex objectives and strategic scoring
/// </summary>
public class ObjectiveEvaluator
{
    private readonly GameState gameState;
    private readonly Dictionary<Hex, float> hexObjectiveScores = new();
    private readonly Dictionary<Hex, HexObjectiveType> hexObjectiveTypes = new();

    public IReadOnlyDictionary<Hex, float> HexObjectiveScores => hexObjectiveScores;
    public IReadOnlyDictionary<Hex, HexObjectiveType> HexObjectiveTypes => hexObjectiveTypes;

    public ObjectiveEvaluator(GameState gameState)
    {
        this.gameState = gameState;
    }

    /// <summary>
    /// Evaluates all hex objectives for the given character
    /// </summary>
    public void EvaluateHexObjectives(Character character)
    {
        hexObjectiveScores.Clear();
        hexObjectiveTypes.Clear();

        if (character?.relevantHexes == null) return;

        foreach (var hex in character.relevantHexes)
        {
            if (hex == null) continue;

            var scores = CalculateObjectiveScores(hex, character);
            var (maxScore, objectiveType) = DetermineTopObjective(scores);

            hexObjectiveScores[hex] = maxScore;
            hexObjectiveTypes[hex] = objectiveType;
        }
    }

    /// <summary>
    /// Gets candidate hexes for a specific objective type, sorted by score
    /// </summary>
    public List<Hex> GetCandidateHexes(Character character, HexObjectiveType objectiveType)
    {
        var candidates = character.relevantHexes
            .Where(h => h != null && hexObjectiveTypes.ContainsKey(h) && hexObjectiveTypes[h] == objectiveType)
            .OrderByDescending(h => hexObjectiveScores[h])
            .ToList();

        // Fallback to all hexes if no specific candidates found
        if (candidates.Count == 0)
        {
            candidates = character.relevantHexes
                .Where(h => h != null && hexObjectiveScores.ContainsKey(h))
                .OrderByDescending(h => hexObjectiveScores[h])
                .ToList();
        }

        return candidates;
    }

    private ObjectiveScores CalculateObjectiveScores(Hex hex, Character character)
    {
        return new ObjectiveScores
        {
            Attack = gameState.EvaluateAttackScore(hex, character),
            Defense = gameState.EvaluateDefenseScore(hex, character),
            Resource = gameState.EvaluateResourceScore(hex, character),
            Territory = gameState.EvaluateTerritoryScore(hex, character),
            Artifact = gameState.EvaluateArtifactScore(hex),
            Safety = gameState.EvaluateSafetyScore(hex, character)
        };
    }

    private (float maxScore, HexObjectiveType objectiveType) DetermineTopObjective(ObjectiveScores scores)
    {
        var scoreValues = new[]
        {
            (scores.Attack, HexObjectiveType.AttackEnemy),
            (scores.Defense, HexObjectiveType.DefendAlly),
            (scores.Resource, HexObjectiveType.GatherResource),
            (scores.Territory, HexObjectiveType.SecureTerritory),
            (scores.Artifact, HexObjectiveType.RetrieveArtifact),
            (scores.Safety, HexObjectiveType.RetreatToSafety)
        };

        var best = scoreValues.Where(s => s.Item1 > 0).OrderByDescending(s => s.Item1).FirstOrDefault();
        return best.Item1 > 0 ? (best.Item1, best.Item2) : (0f, HexObjectiveType.None);
    }

    private struct ObjectiveScores
    {
        public float Attack;
        public float Defense;
        public float Resource;
        public float Territory;
        public float Artifact;
        public float Safety;
    }
}