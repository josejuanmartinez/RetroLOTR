using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System;
using NUnit.Framework;
using Unity.MLAgents.Policies;

public class StrategyGameAgent : Agent
{
    [Header("Character info references")]
    [SerializeField] private GameState gameState;
    [SerializeField] private List<CharacterAction> allPossibleActions;
    [SerializeField] private Character controlledCharacter;

    int maxX;
    int maxY;
    int allArtifactsNum;

    List<Character> allCharacters = new();
    List<Leader> leaders = new();
    AlignmentEnum characterAlignment;

    // Pre-calculate terrain type count
    int terrainTypeCount;
    int alignmentTypeCount;
    float maxTerrainCost;

    public override void OnEpisodeBegin()
    {
        base.Initialize();
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent.GetComponent<Character>();
        allPossibleActions = FindObjectsByType<CharacterAction>(FindObjectsSortMode.None).ToList();

        maxX = gameState.GetMaxX();
        maxY = gameState.GetMaxY();
        allArtifactsNum = gameState.GetAllArtifactsNum();
        allCharacters = gameState.GetAllCharacters();
        leaders = gameState.GetLeaders();
        characterAlignment = controlledCharacter.GetAlignment();

        // Pre-calculate terrain type count
        terrainTypeCount = Enum.GetValues(typeof(TerrainEnum)).Length - 1;
        alignmentTypeCount = Enum.GetValues(typeof(AlignmentEnum)).Length - 1;
        maxTerrainCost = TerrainData.terrainCosts.Values.Max();

        // Add and configure Behavior Parameters component
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams != null)
        {
            behaviorParams.BehaviorName = "Character";
            // Set the correct action space size
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(48); // Match your actual number of actions
        }
        else
        {
            Debug.LogError("Behavior Parameters component not found!");
        }
    }

    public void NewTurn()
    {
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (gameState == null || controlledCharacter == null) return;

        // Only observe relevant hexes around the character
        var relevantHexes = gameState.GetRelevantHexes(controlledCharacter);
        int maxRelevantHexes = 10; // Reduced from 20 to 10
        int hexesToProcess = Mathf.Min(relevantHexes.Count, maxRelevantHexes);

        for (int i = 0; i < hexesToProcess; i++)
        {
            Hex hex = relevantHexes[i];
            if (hex == null)
            {
                // Add minimal null hex observations
                sensor.AddObservation(0f); // x position
                sensor.AddObservation(0f); // y position
                sensor.AddOneHotObservation(0, terrainTypeCount); // terrain type
                continue;
            }

            // Position (normalized)
            sensor.AddObservation(hex.v2.x / maxX);
            sensor.AddObservation(hex.v2.y / maxY);

            // Terrain type
            sensor.AddOneHotObservation((int)hex.terrainType, terrainTypeCount);

            // Simplified unit and army presence (combined into single value)
            int totalUnits = hex.characters.Count(x => x != null && !x.killed) +
                           hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed);
            sensor.AddObservation(totalUnits / 10f); // Normalize

            // Simplified PC presence (just defense if present)
            var pc = hex.GetPC();
            sensor.AddObservation(pc != null ? pc.GetDefense() / 1000f : 0f);
        }

        // Add character's own state (compressed)
        sensor.AddObservation(controlledCharacter.health / 100f);
        sensor.AddOneHotObservation((int)controlledCharacter.GetAlignment(), alignmentTypeCount);
        sensor.AddObservation(controlledCharacter.hasMovedThisTurn ? 0f : 1f);
        sensor.AddObservation(controlledCharacter.hasActionedThisTurn ? 0f : 1f);

        // Add simplified leader state (just gold)
        var owner = controlledCharacter.GetOwner();
        sensor.AddObservation(owner != null ? owner.goldAmount / 1000f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Get the action chosen by the agent
        int actionId = actionBuffers.DiscreteActions[0];

        // Only execute if the action is valid
        List<int> availableActions = GetAvailableActionIds(controlledCharacter);
        if (availableActions.Contains(actionId))
        {
            bool wasBankrupted = controlledCharacter.GetOwner().GetGoldPerTurn() < 0;
            bool wasNegative = controlledCharacter.GetOwner().goldAmount < 0;
            int storesBefore = controlledCharacter.GetOwner().GetStorePoints();
            int enemiesStrengthBefore = gameState.GetEnemyPoints(controlledCharacter.GetOwner());
            int friendlyStrengthBefore = gameState.GetFriendlyPoints(controlledCharacter.GetOwner());
            int characterPointsBefore = controlledCharacter.GetOwner().GetCharacterPoints();
            int pcsStrengthBefore = controlledCharacter.GetOwner().GetPCPoints();
            int armiesStrengthBefore = controlledCharacter.GetOwner().GetArmyPoints();

            // Execute the action
            CharacterAction chosenAction = allPossibleActions.Find(a => a.actionId == actionId);
            chosenAction.Execute();

            bool isBankrupted = controlledCharacter.GetOwner().GetGoldPerTurn() < 0;
            bool isNegative = controlledCharacter.GetOwner().goldAmount < 0;
            int storesAfter = controlledCharacter.GetOwner().GetStorePoints();
            int enemiesStrengthAfter = gameState.GetEnemyPoints(controlledCharacter.GetOwner());
            int friendlyStrengthAfter = gameState.GetFriendlyPoints(controlledCharacter.GetOwner());
            int charactersPointsAfter = controlledCharacter.GetOwner().GetCharacterPoints();
            int pcsStrengthAfter = controlledCharacter.GetOwner().GetPCPoints();
            int armiesStrengthAfter = controlledCharacter.GetOwner().GetArmyPoints();

            // Give immediate reward
            AddReward(chosenAction.reward / 10f); // Normalize reward

            // STRATEGIC REWARDS
            // <-------------------------->
            if (wasBankrupted && !isBankrupted) AddReward(5 / 10f);
            if (wasNegative && !isNegative) AddReward(5 / 10f);
            if (!wasBankrupted && isBankrupted) AddReward(-5 / 10f);
            if (!wasNegative && isNegative) AddReward(-5 / 10f);
            AddReward(storesAfter - storesBefore / 100f);
            AddReward(friendlyStrengthAfter - friendlyStrengthBefore / 100f);
            AddReward(enemiesStrengthBefore - enemiesStrengthAfter / 100f);
            AddReward(charactersPointsAfter - characterPointsBefore / 100f);
            AddReward(pcsStrengthAfter - pcsStrengthBefore / 100f);
            AddReward(armiesStrengthAfter - armiesStrengthBefore / 100f);
            // <-------------------------->
        }
        else
        {
            // Penalize invalid actions
            AddReward(-0.1f);
        }

        // Check if the episode should end
        if (IsGameOver(controlledCharacter.GetOwner()))
        {
            AddReward(IsWinner(controlledCharacter.GetOwner()) ? 25 : -25);
            EndEpisode();
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (controlledCharacter == null || allPossibleActions == null)
        {
            Debug.LogError("Controlled character or possible actions are null!");
            return;
        }

        // Get available actions
        List<int> availableActions = GetAvailableActionIds(controlledCharacter);
        
        // Get the behavior parameters to validate action space
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams == null)
        {
            Debug.LogError("Behavior Parameters component not found!");
            return;
        }

        // Validate action space size
        int expectedActionCount = behaviorParams.BrainParameters.ActionSpec.BranchSizes[0];
        if (allPossibleActions.Count != expectedActionCount)
        {
            Debug.LogError($"Action count mismatch! Expected {expectedActionCount}, got {allPossibleActions.Count}");
            return;
        }

        // Mask all actions first
        for (int i = 0; i < allPossibleActions.Count; i++)
        {
            actionMask.SetActionEnabled(0, i, false);
        }

        // Enable only available actions
        foreach (int actionId in availableActions)
        {
            if (actionId >= 0 && actionId < allPossibleActions.Count)
            {
                actionMask.SetActionEnabled(0, actionId, true);
            }
            else
            {
                Debug.LogWarning($"Invalid action ID: {actionId}");
            }
        }
    }

    private List<int> GetAvailableActionIds(Character character)
    {
        if (character == null || allPossibleActions == null)
        {
            Debug.LogError("Character or possible actions are null!");
            return new List<int>();
        }

        return allPossibleActions
            .Where(action => action != null && action.IsAvailable())
            .Select(action => action.actionId)
            .Where(id => id >= 0 && id < allPossibleActions.Count)
            .ToList();
    }

    private bool IsGameOver(Leader leader)
    {
        return leader.killed;
    }

    private bool IsWinner(Leader leader)
    {
        return gameState.GetWinner() == leader;
    }

    public Character GetCharacter()
    {
        return controlledCharacter;
    }

}