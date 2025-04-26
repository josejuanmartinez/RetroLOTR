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
    [SerializeField] private CharacterAction chosenAction;

    [Header("Training Settings")]
    [SerializeField] private bool isPlayerControlled = false;
    [SerializeField] private bool isTrainingMode = true;

    private bool hasGameStarted = false;

    int alignmentTypeCount;

    // For decision visualization
    public List<int> availableActionsIds = new List<int>();

    public override void OnEpisodeBegin()
    {
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent.GetComponent<Character>();
        allPossibleActions = FindObjectsByType<CharacterAction>(FindObjectsSortMode.None).ToList();

        // Pre-calculate alignment type count
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

        // Reset chosen action
        chosenAction = null;

        if (!hasGameStarted) return;

        base.Initialize();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // This method is called when using Heuristic mode in the agent
        var discreteActions = actionsOut.DiscreteActions;

        // Skip processing if game hasn't started
        if (!hasGameStarted)
        {
            // Just set a default action (0) to avoid errors
            discreteActions[0] = 0;
            return;
        }

        // If we have a chosen action, use that
        if (chosenAction != null)
        {
            discreteActions[0] = chosenAction.actionId;
        }
        else
        {
            // Default to first available action or 0 if none are available
            var availableActions = GetAvailableActionIds(controlledCharacter);
            discreteActions[0] = availableActions.Count > 0 ? availableActions[0] : 0;
        }
    }
    public void NewTurn(bool isPlayerControlled, bool isTrainingMode)
    {
        this.isPlayerControlled = isPlayerControlled;
        this.isTrainingMode = isTrainingMode;

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            OnEpisodeBegin();
        }

        // Initialize all possible actions
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));

        if (isPlayerControlled)
        {
            // In player mode, we'll wait for SetPlayerAction to be called
            // Optionally, we can visualize what the agent would do
            if (isTrainingMode)
            {
                VisualizeAgentDecisions();
            }
        }
        else
        {
            // In AI mode, request and immediately execute decision
            Debug.Log("AI controlled character making decision...");
            RequestDecision();

            // Check if we got a valid action, if not use fallback
            if (chosenAction == null)
            {
                List<int> availableActions = GetAvailableActionIds(controlledCharacter);
                if (availableActions.Count > 0)
                {
                    int fallbackActionId = availableActions[0]; // Just take the first available
                    chosenAction = allPossibleActions.Find(a => a.actionId == fallbackActionId);
                    Debug.LogWarning($"AI controlled character made no decision! Taking fallback action: {chosenAction?.name} (ID: {fallbackActionId}) WITHOUT SUPPORTING DATA");
                }
                else
                {
                    Debug.LogError("No available actions for AI controlled character!");
                }
            }

            ExecuteMovementBefore();
            ExecuteChosenAction();
            ExecuteMovementAfter();
        }
    }

    private void ExecuteMovementBefore()
    {
        Debug.Log($"Moving before to: {controlledCharacter.hex}");
    }
    private void ExecuteMovementAfter()
    {
        Debug.Log($"Moving after to: {controlledCharacter.hex}");
    }

    private void VisualizeAgentDecisions()
    {
        // Store available actions for UI
        availableActionsIds = GetAvailableActionIds(controlledCharacter);

        // DEBUG: Log available actions count
        Debug.Log($"Available actions count: {availableActionsIds.Count}");
        foreach (var actionId in availableActionsIds)
        {
            var action = allPossibleActions.Find(a => a.actionId == actionId);
            Debug.Log($"  Available action: {action?.name} (ID: {actionId})");
        }

        if (availableActionsIds.Count == 0)
        {
            Debug.LogWarning("No available actions for this character! Taking fallback action...");
            // If we want to force a fallback action here, we could set chosenAction to something
            return;
        }

        // Store current chosen action
        CharacterAction previousAction = chosenAction;
        chosenAction = null;

        // Request a decision but don't execute it yet
        Debug.Log("Requesting agent decision for visualization...");
        RequestDecision();

        // DEBUG: Add a small delay and check if chosenAction got set
        Debug.Log($"After RequestDecision, chosenAction is: {(chosenAction != null ? chosenAction.name : "NULL")}");

        // If no action was chosen but we have available actions, select a fallback
        if (chosenAction == null && availableActionsIds.Count > 0)
        {
            int fallbackActionId = availableActionsIds[0]; // Just take the first available
            chosenAction = allPossibleActions.Find(a => a.actionId == fallbackActionId);
            Debug.LogWarning($"AI made no suggestion! Taking fallback action: {chosenAction?.name} (ID: {fallbackActionId}) WITHOUT SUPPORTING DATA");
        }

        ExecuteMovementBefore();

        // Now chosenAction contains what the AI would do
        CharacterAction aiSuggestion = chosenAction;

        // Reset to previous state
        chosenAction = previousAction;

        // Log the AI's suggestion (you would show this in UI)
        if (aiSuggestion != null)
        {
            Debug.Log($"AI suggestion: {aiSuggestion.name} (ID: {aiSuggestion.actionId})");
        }
        else
        {
            Debug.Log("AI has no suggestion after attempting fallback");
        }

        ExecuteMovementAfter();
    }

    public void SetPlayerAction(CharacterAction action)
    {
        // Called from UI when player selects an action
        chosenAction = action;

        if (isTrainingMode)
        {
            // Convert player choice to action buffers for training
            ActionBuffers buffers = new ActionBuffers();
            var discreteActions = buffers.DiscreteActions;
            discreteActions[0] = action.actionId;  // Changed from Array access to indexer

            // Process the action for training purposes
            OnActionReceived(buffers);
        }
        else
        {
            // Just execute the action without training
            ExecuteChosenAction();
        }
    }

    private void ExecuteChosenAction()
    {
        if (chosenAction != null)
        {
            Debug.Log($"Action chosen: {chosenAction.name}");

            // Get leader state before action
            var leader = controlledCharacter.GetOwner();

            if (leader == null)
            {
                Debug.LogError("Leader is null! Cannot execute action properly.");
                return;
            }

            bool wasBankrupted = leader.GetGoldPerTurn() < 0;
            bool wasNegative = leader.goldAmount < 0;
            int storesBefore = leader.GetStorePoints();
            int enemiesStrengthBefore = gameState.GetEnemyPoints(leader);
            int friendlyStrengthBefore = gameState.GetFriendlyPoints(leader);
            int characterPointsBefore = leader.GetCharacterPoints();
            int pcsStrengthBefore = leader.GetPCPoints();
            int armiesStrengthBefore = leader.GetArmyPoints();

            // Execute the actual action
            chosenAction.Execute();

            if (isTrainingMode)
            {
                // Calculate post-action state
                bool isBankrupted = leader.GetGoldPerTurn() < 0;
                bool isNegative = leader.goldAmount < 0;
                int storesAfter = leader.GetStorePoints();
                int enemiesStrengthAfter = gameState.GetEnemyPoints(leader);
                int friendlyStrengthAfter = gameState.GetFriendlyPoints(leader);
                int charactersPointsAfter = leader.GetCharacterPoints();
                int pcsStrengthAfter = leader.GetPCPoints();
                int armiesStrengthAfter = leader.GetArmyPoints();

                // Add rewards
                float actionReward = chosenAction.reward / 10f; // Normalize reward
                AddReward(actionReward);

                // STRATEGIC REWARDS
                if (wasBankrupted && !isBankrupted) AddReward(5 / 10f);
                if (wasNegative && !isNegative) AddReward(5 / 10f);
                if (!wasBankrupted && isBankrupted) AddReward(-5 / 10f);
                if (!wasNegative && isNegative) AddReward(-5 / 10f);
                AddReward((storesAfter - storesBefore) / 100f);
                AddReward((friendlyStrengthAfter - friendlyStrengthBefore) / 100f);
                AddReward((enemiesStrengthBefore - enemiesStrengthAfter) / 100f);
                AddReward((charactersPointsAfter - characterPointsBefore) / 100f);
                AddReward((pcsStrengthAfter - pcsStrengthBefore) / 100f);
                AddReward((armiesStrengthAfter - armiesStrengthBefore) / 100f);

                // Check for game over condition
                if (IsGameOver(leader))
                {
                    AddReward(IsWinner(leader) ? 25 : -25);
                    EndEpisode();
                }
            }
        }
        else
        {
            Debug.LogError("No action chosen to execute! This should never happen due to fallback mechanism.");

            // Last resort fallback - try to find any available action
            List<int> availableActions = GetAvailableActionIds(controlledCharacter);
            if (availableActions.Count > 0)
            {
                int emergencyActionId = availableActions[0];
                chosenAction = allPossibleActions.Find(a => a.actionId == emergencyActionId);
                Debug.LogWarning($"EMERGENCY FALLBACK! Taking action: {chosenAction?.name} (ID: {emergencyActionId}) WITHOUT SUPPORTING DATA");
                ExecuteChosenAction(); // Recursive call, but should have chosenAction set now
            }
        }
    }

    private void AddDummyObservations(VectorSensor sensor)
    {
        // Add dummy hex observations (11 per hex * 190 hexes = 2090)
        for (int i = 0; i < 190; i++)
        {
            // Position
            sensor.AddObservation(-1f);
            sensor.AddObservation(-1f);

            // Movement cost
            sensor.AddObservation(1f);

            // Unit presence
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);

            // Army presence
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);

            // PC presence
            sensor.AddObservation(-1f);
            sensor.AddObservation(-1f);
            sensor.AddObservation(-1f);

            // Artifact presence
            sensor.AddObservation(-1f);
        }

        // Add dummy character state observations (15)
        // Leader info
        sensor.AddObservation(-1f);
        sensor.AddObservation(-1f);
        sensor.AddObservation(-1f);

        // Character health
        sensor.AddObservation(1f);
        sensor.AddObservation(0f);

        // Alignment (one-hot)
        for (int i = 0; i < alignmentTypeCount; i++)
        {
            sensor.AddObservation(i == 0 ? 1f : 0f);
        }

        // Leader stores
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);

        // Leader stats
        sensor.AddObservation(0f);
        sensor.AddObservation(0f);

        // Turn
        sensor.AddObservation(0f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {

        // Check if game has started
        if (!hasGameStarted)
        {
            AddDummyObservations(sensor);
            return;
        }

        for (int i = 0; i < controlledCharacter.relevantHexes.Count; i++)
        {
            // 11 observations each
            Hex hex = controlledCharacter.relevantHexes[i];
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
            sensor.AddObservation(totalEnemyArmies / (float)gameState.GetMaxCharacters()); // Normalize (1 army per character max)

            // Friendly Army presence
            int totalFriendlyArmies = hex.armies.Count(x => x != null && !x.killed && x.commander != null && !x.commander.killed && x.GetAlignment() == controlledCharacter.GetAlignment());
            sensor.AddObservation(totalFriendlyArmies / (float)gameState.GetMaxCharacters()); // Normalize (1 army per character max)

            // Enemy PC defense
            var pc = hex.GetPC();
            sensor.AddObservation(pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != controlledCharacter.GetAlignment()) ? pc.GetDefense() / 10000f : -1f);

            // Friendly PC defense
            sensor.AddObservation(pc != null && pc.owner.GetAlignment() == controlledCharacter.GetAlignment() ? pc.GetDefense() / 10000f : -1f);

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
        sensor.AddObservation(owner != null ? gameState.GetIndexOfLeader(owner) / (float)gameState.GetMaxLeaders() : -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader ? 1f : -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader && (owner as NonPlayableLeader).joined ? 1f : -1f);
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
        int selectedActionId = actionBuffers.DiscreteActions[0];

        Debug.Log($"OnActionReceived: Selected action ID = {selectedActionId}");

        // Only set the action if it's valid
        List<int> availableActions = GetAvailableActionIds(controlledCharacter);

        Debug.Log($"Available actions: {string.Join(", ", availableActions)}");

        if (availableActions.Contains(selectedActionId))
        {
            chosenAction = allPossibleActions.Find(a => a.actionId == selectedActionId);
            Debug.Log($"Valid action selected: {chosenAction?.name} (ID: {selectedActionId})");

            // In AI mode, execute immediately
            if (!isPlayerControlled)
            {
                ExecuteChosenAction();
            }
        }
        else
        {
            // Penalize invalid actions
            Debug.LogWarning($"Invalid action selected (ID: {selectedActionId})! Penalizing agent.");
            AddReward(-0.1f);

            // If no valid action was selected but we have available actions, select a fallback
            if (availableActions.Count > 0)
            {
                int fallbackActionId = availableActions[0]; // Just take the first available
                chosenAction = allPossibleActions.Find(a => a.actionId == fallbackActionId);
                Debug.LogWarning($"Taking fallback action: {chosenAction?.name} (ID: {fallbackActionId}) WITHOUT SUPPORTING DATA");

                // In AI mode, execute immediately
                if (!isPlayerControlled)
                {
                    ExecuteChosenAction();
                }
            }
            else
            {
                chosenAction = null;
                Debug.LogError("No available actions for this character!");
            }
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

        // DEBUG: Log action mask details
        Debug.Log($"Writing action mask. Available actions: {availableActions.Count}");

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
                Debug.Log($"Enabled action ID: {actionId} ({allPossibleActions.Find(a => a.actionId == actionId)?.name})");
            }
            else
            {
                Debug.LogWarning($"Invalid action ID: {actionId}");
            }
        }

        // If no actions are available, enable at least one as fallback
        if (availableActions.Count == 0 && allPossibleActions.Count > 0)
        {
            actionMask.SetActionEnabled(0, 0, true);
            Debug.LogWarning("No available actions! Enabling action 0 as fallback.");
        }
    }

    private List<int> GetAvailableActionIds(Character character)
    {
        if (character == null || allPossibleActions == null)
        {
            Debug.LogError("Character or possible actions are null!");
            return new List<int>();
        }

        var availableActions = allPossibleActions
            .Where(action => action != null && action.IsAvailable())
            .Select(action => action.actionId)
            .Where(id => id >= 0 && id < allPossibleActions.Count)
            .ToList();

        // Debug: Log available action count
        Debug.Log($"GetAvailableActionIds: Found {availableActions.Count} available actions");

        return availableActions;
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

    public List<CharacterAction> GetAvailableActions()
    {
        return allPossibleActions
            .Where(action => action != null && action.IsAvailable())
            .ToList();
    }

    // Toggle between player and AI control
    public void SetPlayerControlled(bool isControlled)
    {
        isPlayerControlled = isControlled;
    }

    // Toggle training mode
    public void SetTrainingMode(bool training)
    {
        isTrainingMode = training;
    }

    // Get the agent's chosen action (useful for UI display)
    public CharacterAction GetChosenAction()
    {
        return chosenAction;
    }
}