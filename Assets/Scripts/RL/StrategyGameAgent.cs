using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class StrategyGameAgent : Agent
{
    [Header("Character info references")]
    [SerializeField] private GameState gameState;
    [SerializeField] private List<CharacterAction> allPossibleActions;
    [SerializeField] private List<int> availableActionsIds;
    [SerializeField] private Character controlledCharacter;
    [SerializeField] private CharacterAction chosenAction;

    [Header("Training Settings")]
    [SerializeField] private bool isPlayerControlled = false;
    [SerializeField] private bool autoplay = false;
    [SerializeField] private bool isTrainingMode = true;

    BehaviorParameters behaviorParams;

    private bool initialized = false;
    private bool hasGameStarted = false;

    CharacterAction DEFAULT;

    int alignmentTypeCount;

    void Awaken()
    {
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent.GetComponent<Character>();
        allPossibleActions = FindFirstObjectByType<ActionsManager>().characterActions.ToList();
        behaviorParams = GetComponent<BehaviorParameters>();
        DEFAULT = FindFirstObjectByType<ActionsManager>().DEFAULT;

        // Pre-calculate alignment type count
        alignmentTypeCount = Enum.GetValues(typeof(AlignmentEnum)).Length - 1;

        // Reset chosen action
        chosenAction = null;

        initialized = true;
    }


    public override void OnEpisodeBegin()
    {
        if(!initialized) Awaken();
        behaviorParams.BehaviorName = "Character";
        // Set the correct action space size
        behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(allPossibleActions.Count);

        base.Initialize();
    }

    /**
     * This method is called at the end to modify the action taken by the agent with custom heuristics
     */
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!initialized) Awaken();

        var discreteActions = actionsOut.DiscreteActions;

        // Skip processing if game hasn't started
        if (!hasGameStarted)
        {
            // Just set a default action to avoid errors when not started
            discreteActions[0] = FindFirstObjectByType<ActionsManager>().GetDefault();
        }
        else
        {
            // Ignoring heuristics for now
            // discreteActions[0] = chosenAction.actionId;
        }

    }
    public void NewTurn(bool isPlayerControlled, bool autoplay, bool isTrainingMode)
    {
        if (!initialized) Awaken();
        this.isPlayerControlled = isPlayerControlled;
        this.autoplay = autoplay;
        this.isTrainingMode = isTrainingMode;

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            OnEpisodeBegin();
        }

        // Initialize all possible actions
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
        availableActionsIds = GetAvailableActionIds(controlledCharacter);

        if (isPlayerControlled && !autoplay)
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
            Debug.Log($"- [{controlledCharacter.characterName}] AI controlled character making decision...");
            RequestDecision();
        }
    }

    private void ExecuteMovementBefore()
    {
        Debug.Log($"- [{controlledCharacter.characterName}] moving before to: {controlledCharacter.hex}");
    }
    private void ExecuteMovementAfter()
    {
        Debug.Log($"- [{controlledCharacter.characterName}] moving after to: {controlledCharacter.hex}");
    }

    private void VisualizeAgentDecisions()
    {
        // DEBUG: Log available actions count
        foreach (var actionId in availableActionsIds)
        {
            var action = allPossibleActions.Find(a => a.actionId == actionId);
            Debug.Log($"- [{controlledCharacter.characterName}] Available action: {action?.name} {action?.actionName} (ID: {actionId})");
        }

        // Request a decision but don't execute it yet
        RequestDecision();

    }

    public void FeedbackWithPlayerActions(CharacterAction action)
    {
        // Called from UI when player selects an action
        chosenAction = action;

        if (isTrainingMode)
        {
            // Create an array for discrete actions
            int[] discreteActionsArray = new int[1]; // or whatever number of discrete actions you have
            discreteActionsArray[0] = action.actionId;

            // Create ActionBuffers with discrete actions
            ActionBuffers buffers = new ActionBuffers(
                new float[] { }, // No continuous actions
                discreteActionsArray
            );

            // Process the action for training purposes
            OnActionReceived(buffers);
        }
    }

    private void ExecuteChosenAction()
    {
        Debug.Log($"- [{controlledCharacter.characterName}] Executing action: {chosenAction.actionName}");

        // Get leader state before action
        var leader = controlledCharacter.GetOwner();

        if (leader == null)
        {
            Debug.LogError($"- [{controlledCharacter.characterName}] Leader is null! Cannot execute action properly.");
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


        ExecuteMovementBefore();
        chosenAction.Execute();
        ExecuteMovementAfter();

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
        //int selectedActionIndex = actionBuffers.DiscreteActions[0];
        int selectedActionId = actionBuffers.DiscreteActions[0];
        chosenAction = allPossibleActions.Find(x => x.actionId == selectedActionId);

        // Log the received action index for debugging
        //Debug.Log($"- [{controlledCharacter.characterName}] Received action index: {selectedActionIndex}");

        //if (selectedActionIndex >= 0 && selectedActionIndex < allPossibleActions.Count)
        if (chosenAction != null)
        {
            // Check if this action ID is in our available actions list
            if (!availableActionsIds.Contains(selectedActionId))
            {
                Debug.LogWarning($"- [{controlledCharacter.characterName}] Action masking failure: Selected action {selectedActionId} " +
                    $": {chosenAction.actionName}) is not in available action! Falling back to PASS");

                // Debug output to see what actions are available
                Debug.LogWarning($"- [{controlledCharacter.characterName}] Available action IDs: {string.Join(", ", availableActionsIds)}");

                chosenAction = DEFAULT;
            }
        }
        else
        {
            Debug.LogError($"- [{controlledCharacter.characterName}] Invalid action id {selectedActionId}: not found in all possible actions! Falling back to PASS");
            chosenAction = DEFAULT;
        }

        Debug.Log($"- [{controlledCharacter.characterName}] Final chosen action: {chosenAction.actionName} (ID: {chosenAction.actionId})");

        // In AI mode, execute immediately
        if (!isPlayerControlled || autoplay)
        {
            ExecuteChosenAction();
        }
        else
        {
            Debug.Log($"- [{controlledCharacter.characterName}] Not executed as it was just a suggestion");
        }
    }

    // Ensure the action mask is correctly set up
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!initialized) Awaken();

        if (controlledCharacter == null)
        {
            Debug.LogError("Controlled character is null!");
            return;
        }

        if (availableActionsIds == null || availableActionsIds.Count < 1)
        {
            Debug.LogError($"- [{controlledCharacter.characterName}] Available actions are null or empty!");
            return;
        }

        // Get the behavior parameters to validate action space
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams == null)
        {
            Debug.LogError($"- [{controlledCharacter.characterName}] Behavior Parameters component not found!");
            return;
        }

        // Validate action space size
        int expectedActionCount = behaviorParams.BrainParameters.ActionSpec.BranchSizes[0];
        Debug.Log($" - [{controlledCharacter.characterName}] Expected action count: {expectedActionCount}, Available actions: {availableActionsIds.Count}");

        // Important: First, disable ALL actions to ensure a clean slate
        for (int i = 0; i < expectedActionCount; i++)
        {
            actionMask.SetActionEnabled(0, i, false);
        }

        // Then enable ONLY available actions
        foreach (int actionId in availableActionsIds)
        {
            // Find the index in allPossibleActions that corresponds to this action ID
            int actionIndex = allPossibleActions.FindIndex(a => a.actionId == actionId);

            // Verify the index is valid before enabling
            if (actionIndex >= 0 && actionIndex < expectedActionCount)
            {
                actionMask.SetActionEnabled(0, actionIndex, true);
                Debug.Log($"- [{controlledCharacter.characterName}] Enabled action index: {actionIndex} for ID: {actionId} ({allPossibleActions[actionIndex].actionName})");
            }
            else
            {
                Debug.LogError($"- [{controlledCharacter.characterName}] Invalid action index {actionIndex} for ID: {actionId}. Cannot enable in mask!");
            }
        }
    }

    private List<int> GetAvailableActionIds(Character character)
    {
        if(controlledCharacter == null)
        {
            Debug.LogError("Controlled character actions is null!");
            return new List<int>();
        }

        if (allPossibleActions == null)
        {
            Debug.LogError($"- [{controlledCharacter.characterName}] Possible actions are null!");
            return new List<int>();
        }

        var availableActions = allPossibleActions
            .Where(action => action != null && action.FulfillsConditions())
            .Select(action => action.actionId)
            .ToList();

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
            .Where(action => action != null && action.ResourcesAvailable())
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