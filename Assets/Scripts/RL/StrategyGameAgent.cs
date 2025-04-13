using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System;
using Unity.MLAgents.Policies;

public class StrategyGameAgent : Agent
{
    [Header("Character info references")]
    [SerializeField] private GameState gameState;
    [SerializeField] private List<CharacterAction> allPossibleActions;
    [SerializeField] private Character controlledCharacter;

    // Pre-calculate terrain type count
    int terrainTypeCount;
    int alignmentTypeCount;

    public override void OnEpisodeBegin()
    {
        base.Initialize();
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent.GetComponent<Character>();
        allPossibleActions = FindObjectsByType<CharacterAction>(FindObjectsSortMode.None).ToList();

        // Pre-calculate terrain type count
        terrainTypeCount = Enum.GetValues(typeof(TerrainEnum)).Length - 1;
        alignmentTypeCount = Enum.GetValues(typeof(AlignmentEnum)).Length - 1;

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

        // Only observe relevant hexes around the character: 190 Hexes
        var relevantHexes = gameState.GetRelevantHexes(controlledCharacter);

        // 0...189
        for (int i = 0; i < relevantHexes.Count; i++)
        {
            // 11 observations each
            Hex hex = relevantHexes[i];
            if (hex == null)
            {
                // Add minimal null hex observations
                sensor.AddObservation(-1f); // x position
                sensor.AddObservation(-1f); // y position
                sensor.AddObservation(1f); // movement cost
                sensor.AddObservation(0f); // enemy unit presence
                sensor.AddObservation(0f); // friendly unit presence
                sensor.AddObservation(0f); // army presence
                sensor.AddObservation(0f); // enemy presence
                sensor.AddObservation(-1f); // enemy PC presence
                sensor.AddObservation(-1f); // friendly PC presence
                sensor.AddObservation(-1f); // NPC presence
                sensor.AddObservation(-1f); // artifact PC presence
                continue;
            }
            // 11 observations each
            /***************************************/

            // Position (normalized)
            sensor.AddObservation(hex.v2.x / gameState.GetMaxX());
            sensor.AddObservation(hex.v2.y / gameState.GetMaxY());

            // Movement cost
            sensor.AddObservation(hex.GetTerrainCost(controlledCharacter) / (float)gameState.GetMaxMovement()); // Normalize

            // Enemy Unit presence
            int totalEnemyUnits = hex.characters.Count(x => x != null && !x.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()));
            sensor.AddObservation(totalEnemyUnits / (float)gameState.GetMaxCharacters()); // Normalize

            // Friendly Unit presence
            int totalFriendlyUnits = hex.characters.Count(x => x != null && !x.killed && x.GetAlignment() == controlledCharacter.GetAlignment());
            sensor.AddObservation(totalFriendlyUnits / (float)gameState.GetMaxCharacters()); // Normalize

            // Enemy Army presence
            int totalEnemyArmies = hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()));
            hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed);
            sensor.AddObservation(totalEnemyArmies / (float)gameState.GetMaxCharacters()); // Normalize (1 army per character max)

            // Friendly Army presence
            int totalFriendlyArmies = hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed && x.GetAlignment() == controlledCharacter.GetAlignment());
            hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed);
            sensor.AddObservation(totalFriendlyArmies / (float)gameState.GetMaxCharacters()); // Normalize (1 army per character max)

            // Enemy PC defense
            var pc = hex.GetPC();
            sensor.AddObservation(pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != controlledCharacter.GetAlignment())? pc.GetDefense() / 10000f : -1f);

            // Friendly PC defense
            sensor.AddObservation(pc != null &&  pc.owner.GetAlignment() == controlledCharacter.GetAlignment() ? pc.GetDefense() / 10000f : -1f);

            // NPC presence
            sensor.AddObservation(pc != null && pc.owner is NonPlayableLeader ? 1f : -1f);

            // Artifact presence
            sensor.AddObservation(hex.hiddenArtifacts.Count > 0 ? hex.hiddenArtifacts.Count / (float)gameState.GetMaxArtifacts() : -1f);
        }

        // TOTAL SO FAR: 11 * 190 = 2090 OBSERVATIONS

        // 15 OBSERVATIONS
        /**********************************************************/

        // Add character's own state (compressed)
        var owner = controlledCharacter.GetOwner();
        sensor.AddObservation(owner!= null? gameState.GetIndexOfLeader(owner) / (float)gameState.GetMaxLeaders() : -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader? 1f: -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader && (owner as NonPlayableLeader).joined? 1f : -1f);
        sensor.AddObservation(controlledCharacter.health / 100f);
        sensor.AddObservation(owner != null || controlledCharacter.killed ? 0f : 1f);
        sensor.AddOneHotObservation((int)controlledCharacter.GetAlignment(), alignmentTypeCount);

        // Add leader stores
        sensor.AddObservation(owner != null ? owner.goldAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.leatherAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.timberAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.mountsAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.ironAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.mithrilAmount / 10000f : 0f);

        // Add leader stats
        sensor.AddObservation(owner != null ? owner.controlledCharacters.Count / (float)gameState.GetMaxCharacters() : 0f);
        sensor.AddObservation(owner != null ? owner.controlledPcs.Count / (float)gameState.GetMaxCharacters() : 0f);

        // Turn
        sensor.AddObservation(gameState.GetTurn() / (float)gameState.GetMaxTurns());

        /**********************************************************/

        // TOTAL SO FAR: 2090 + 15 + 1(automatically temporal observations) = 2106 OBSERVATIONS
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