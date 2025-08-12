using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Handles building observations for the ML Agent
/// </summary>
public class ObservationBuilder
{
    private readonly GameState gameState;
    private readonly HexPathRenderer hexPathRenderer;

    // Observation space constants (HARD-LOCKED for training consistency)
    private const int ALIGNMENT_ONE_HOT = 10;
    private const int OBJECTIVE_TYPE_COUNT = 6;
    private const int OBJECTIVE_ONE_HOT = OBJECTIVE_TYPE_COUNT;
    private const int TOP_HEX_COUNT = 10;
    private const int OBS_PER_HEX = 12;
    private const int BASE_SCALARS = 19;

    public int ExpectedObservationCount => BASE_SCALARS + ALIGNMENT_ONE_HOT + OBJECTIVE_ONE_HOT + TOP_HEX_COUNT * OBS_PER_HEX;

    // Debug tracking
    private int obsCount;
    private readonly List<ObservationSection> sections = new();

    public ObservationBuilder(GameState gameState, HexPathRenderer hexPathRenderer)
    {
        this.gameState = gameState;
        this.hexPathRenderer = hexPathRenderer;
    }

    /// <summary>
    /// Builds complete observations for the agent
    /// </summary>
    public void BuildObservations(VectorSensor sensor, Character character, HexObjectiveType currentObjective, 
        ObjectiveEvaluator objectiveEvaluator, bool hasEpisodeBegun)
    {
        ResetTally();

        if (!hasEpisodeBegun)
        {
            BuildDummyObservations(sensor);
            return;
        }

        var owner = character?.GetOwner();

        BuildCharacterObservations(sensor, character, owner);
        BuildResourceObservations(sensor, owner);
        BuildGameStateObservations(sensor, owner);
        BuildObjectiveObservations(sensor, currentObjective);
        BuildHexObservations(sensor, character, objectiveEvaluator);

        ValidateObservationCount("BuildObservations");
    }

    private void BuildCharacterObservations(VectorSensor sensor, Character character, Leader owner)
    {
        BeginSection("Leader/Char Flags + Health + FreeAgent (5)");
        AddObs(sensor, owner != null ? gameState.GetIndexOfLeader(owner) / (float)gameState.GetMaxLeaders() : -1f);
        AddObs(sensor, owner != null && owner is NonPlayableLeader ? 1f : -1f);
        AddObs(sensor, owner != null && owner is NonPlayableLeader && (owner as NonPlayableLeader).joined ? 1f : -1f);
        AddObs(sensor, character != null ? character.health / 100f : 0f);
        AddObs(sensor, owner != null || (character != null && character.killed) ? 0f : 1f);
        EndSection();

        BeginSection($"Alignment one-hot ({ALIGNMENT_ONE_HOT})");
        int alignmentIndex = (int)(character?.GetAlignment() ?? 0);
        AddOneHot(sensor, alignmentIndex, ALIGNMENT_ONE_HOT);
        EndSection();
    }

    private void BuildResourceObservations(VectorSensor sensor, Leader owner)
    {
        BeginSection("Resources (6)");
        AddObs(sensor, owner?.goldAmount / 10000f ?? 0f);
        AddObs(sensor, owner?.leatherAmount / 10000f ?? 0f);
        AddObs(sensor, owner?.timberAmount / 10000f ?? 0f);
        AddObs(sensor, owner?.mountsAmount / 10000f ?? 0f);
        AddObs(sensor, owner?.ironAmount / 10000f ?? 0f);
        AddObs(sensor, owner?.mithrilAmount / 10000f ?? 0f);
        EndSection();
    }

    private void BuildGameStateObservations(VectorSensor sensor, Leader owner)
    {
        BeginSection("Status (2)");
        AddObs(sensor, owner?.controlledCharacters.Count / (float)gameState.GetMaxCharacters() ?? 0f);
        AddObs(sensor, owner?.controlledPcs.Count / (float)gameState.GetMaxCharacters() ?? 0f);
        EndSection();

        BeginSection("Game progress (1)");
        AddObs(sensor, gameState?.GetTurn() / (float)gameState.GetMaxTurns() ?? 0f);
        EndSection();

        BeginSection("Strategic metrics (5)");
        AddObs(sensor, owner != null ? gameState.CountControlledHexes(owner) / 100f : 0f);
        AddObs(sensor, owner?.GetResourceProductionPoints() / 500f ?? 0f);
        AddObs(sensor, owner != null ? gameState.GetAverageCharacterHealth(owner) / 100f : 0f);
        AddObs(sensor, owner != null ? gameState.CountStrategicLocations(owner) / 20f : 0f);
        AddObs(sensor, owner != null ? gameState.CountArtifacts(owner) / 10f : 0f);
        EndSection();
    }

    private void BuildObjectiveObservations(VectorSensor sensor, HexObjectiveType currentObjective)
    {
        BeginSection($"Current objective one-hot ({OBJECTIVE_ONE_HOT})");
        AddOneHot(sensor, (int)currentObjective, OBJECTIVE_ONE_HOT);
        EndSection();
    }

    private void BuildHexObservations(VectorSensor sensor, Character character, ObjectiveEvaluator objectiveEvaluator)
    {
        var sortedHexes = GetTopHexes(character, objectiveEvaluator);

        for (int i = 0; i < sortedHexes.Count; i++)
        {
            var hex = sortedHexes[i];
            BeginSection($"Hex[{i}] features ({OBS_PER_HEX})");
            
            if (hex == null || character?.hex == null)
            {
                AddEmptyHexObservations(sensor);
            }
            else
            {
                AddHexObservations(sensor, hex, character);
            }
            
            EndSection();
        }
    }

    private List<Hex> GetTopHexes(Character character, ObjectiveEvaluator objectiveEvaluator)
    {
        var sortedHexes = character?.relevantHexes
            ?.Where(h => h != null)
            .OrderByDescending(h => objectiveEvaluator.HexObjectiveScores.ContainsKey(h) ? objectiveEvaluator.HexObjectiveScores[h] : 0f)
            .Take(TOP_HEX_COUNT)
            .ToList() ?? new List<Hex>();

        while (sortedHexes.Count < TOP_HEX_COUNT) 
            sortedHexes.Add(null);

        return sortedHexes;
    }

    private void AddEmptyHexObservations(VectorSensor sensor)
    {
        AddObs(sensor, -1f); // Distance
        for (int j = 0; j < 6; j++) AddObs(sensor, 0f); // Scores
        AddObs(sensor, 1f);  // Path cost
        for (int j = 0; j < 4; j++) AddObs(sensor, 0f); // Indicators
    }

    private void AddHexObservations(VectorSensor sensor, Hex hex, Character character)
    {
        float distance = Vector2Int.Distance(character.hex.v2, hex.v2);
        AddObs(sensor, distance / 20f);
        
        // Objective scores
        AddObs(sensor, gameState.EvaluateAttackScore(hex, character));
        AddObs(sensor, gameState.EvaluateDefenseScore(hex, character));
        AddObs(sensor, gameState.EvaluateResourceScore(hex, character));
        AddObs(sensor, gameState.EvaluateTerritoryScore(hex, character));
        AddObs(sensor, gameState.EvaluateArtifactScore(hex));
        AddObs(sensor, gameState.EvaluateSafetyScore(hex, character));

        // Path and tactical info
        float pathCost = hexPathRenderer?.GetPathCost(character.hex.v2, hex.v2, character) ?? 1f;
        AddObs(sensor, gameState?.GetMaxMovement() > 0 ? pathCost / gameState.GetMaxMovement() : 0f);

        AddObs(sensor, gameState.CalculateEnemyThreat(hex, character));
        AddObs(sensor, gameState.CalculateAllySupport(hex, character));
        AddObs(sensor, gameState.CalculateResourceValue(hex));
        AddObs(sensor, gameState.IsStrategicLocation(hex) ? 1f : 0f);
    }

    private void BuildDummyObservations(VectorSensor sensor)
    {
        ResetTally();
        
        // Build dummy observations with same structure
        BeginSection("Leader/Char Flags + Health + FreeAgent (5)");
        for (int i = 0; i < 4; i++) AddObs(sensor, -1f);
        AddObs(sensor, 1f);
        EndSection();

        BeginSection($"Alignment one-hot ({ALIGNMENT_ONE_HOT})");
        AddOneHot(sensor, 0, ALIGNMENT_ONE_HOT);
        EndSection();

        BeginSection("Resources (6)");
        for (int i = 0; i < 6; i++) AddObs(sensor, 0f);
        EndSection();

        BeginSection("Status (2)");
        for (int i = 0; i < 2; i++) AddObs(sensor, 0f);
        EndSection();

        BeginSection("Game progress (1)");
        AddObs(sensor, 0f);
        EndSection();

        BeginSection("Strategic metrics (5)");
        for (int i = 0; i < 5; i++) AddObs(sensor, 0f);
        EndSection();

        BeginSection($"Current objective one-hot ({OBJECTIVE_ONE_HOT})");
        AddOneHot(sensor, 0, OBJECTIVE_ONE_HOT);
        EndSection();

        for (int i = 0; i < TOP_HEX_COUNT; i++)
        {
            BeginSection($"Hex[{i}] features ({OBS_PER_HEX})");
            AddEmptyHexObservations(sensor);
            EndSection();
        }

        ValidateObservationCount("BuildDummyObservations");
    }

    #region Debug Helpers

    private void BeginSection(string name)
    {
        sections.Add(new ObservationSection(name, obsCount, obsCount));
    }

    private void EndSection()
    {
        if (sections.Count == 0) return;
        var section = sections[sections.Count - 1];
        section.EndIndex = obsCount;
        sections[sections.Count - 1] = section;
    }

    private void ResetTally()
    {
        obsCount = 0;
        sections.Clear();
    }

    private void AddObs(VectorSensor sensor, float value)
    {
        sensor.AddObservation(value);
        obsCount++;
    }

    private void AddOneHot(VectorSensor sensor, int index, int size)
    {
        if (index < 0 || index >= size) index = 0;
        sensor.AddOneHotObservation(index, size);
        obsCount += size;
    }

    private void ValidateObservationCount(string context)
    {
        if (obsCount != ExpectedObservationCount)
        {
            var message = $"[ObservationBuilder] OBS COUNT MISMATCH in {context}: emitted {obsCount}, expected {ExpectedObservationCount}\n";
            foreach (var section in sections)
            {
                int count = section.EndIndex - section.StartIndex;
                message += $"  - Section '{section.Name}': +{count} (indices {section.StartIndex}..{section.EndIndex - 1})\n";
            }
            Debug.LogWarning(message);
        }
    }

    private struct ObservationSection
    {
        public string Name;
        public int StartIndex;
        public int EndIndex;

        public ObservationSection(string name, int start, int end)
        {
            Name = name;
            StartIndex = start;
            EndIndex = end;
        }
    }

    #endregion
}