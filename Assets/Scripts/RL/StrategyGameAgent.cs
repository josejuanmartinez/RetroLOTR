using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Main ML Agent for strategy game decisions.
/// 
/// ML Pipeline Flow:
/// RequestDecision() → CollectObservations() → (maybe WriteDiscreteActionMask()) → Heuristic() or model inference → OnActionReceived()
/// 
/// Refactored to use specialized components:
/// - ObjectiveEvaluator: Handles hex objective evaluation and scoring
/// - MovementPlanner: Handles movement strategies and pathfinding  
/// - ActionExecutor: Handles action execution and reward calculation
/// - ObservationBuilder: Handles observation collection
/// </summary>
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
    [SerializeField] private bool isTrainingMode = true;

    // Core components
    private BehaviorParameters behaviorParams;
    private Board board;
    private HexPathRenderer hexPathRenderer;
    private CharacterAction DEFAULT_ACTION;

    // Specialized components for different responsibilities
    private ObjectiveEvaluator objectiveEvaluator;
    private MovementPlanner movementPlanner;
    private ActionExecutor actionExecutor;
    private ObservationBuilder observationBuilder;
    private TrainingManager trainingManager;

    // Agent state
    private bool awaken = false;
    private bool hasEpisodeBegun = false;
    private AgentPhase phase = AgentPhase.EvaluateObjectives;
    private Hex strategicTargetHex;
    private float lastDistanceToTarget = float.MaxValue;
    private HexObjectiveType currentObjective = HexObjectiveType.None;

    // Action space constants
    private const int OBJECTIVE_TYPE_COUNT = 6;
    private const int TARGET_PRIORITY_LEVELS = 3;   // High, Medium, Low priority targets
    private const int MOVEMENT_STRATEGY_COUNT = 3;  // Direct, Cautious, Aggressive

    // Action branches
    private const int OBJECTIVE_TYPE_BRANCH = 0;    // What type of objective to pursue
    private const int TARGET_HEX_BRANCH = 1;        // Which specific hex to target
    private const int MOVEMENT_BRANCH = 2;          // How to move toward the target
    private const int ACTION_BRANCH = 3;            // What action to take once there

    public enum AgentPhase { EvaluateObjectives, MoveTowardsObjective, ChooseAction }

    /// <summary>
    /// Initialize all components and references
    /// </summary>
    private void Awaken()
    {
        // Find core game components
        gameState = GameObject.FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent?.GetComponent<Character>();
        allPossibleActions = GameObject.FindFirstObjectByType<ActionsManager>()?.characterActions.ToList();
        behaviorParams = GetComponent<BehaviorParameters>();
        DEFAULT_ACTION = GameObject.FindFirstObjectByType<ActionsManager>()?.DEFAULT;
        board = GameObject.FindFirstObjectByType<Board>();
        hexPathRenderer = GameObject.FindFirstObjectByType<HexPathRenderer>();

        // Initialize specialized components
        objectiveEvaluator = new ObjectiveEvaluator(gameState);
        movementPlanner = new MovementPlanner(gameState, board);
        actionExecutor = new ActionExecutor(gameState, this);
        observationBuilder = new ObservationBuilder(gameState, hexPathRenderer);
        trainingManager = new TrainingManager(this, allPossibleActions);

        // Reset state
        chosenAction = null;
        awaken = true;
    }

    #region 1. Initialization & Setup (Called Once)

    public override void OnEpisodeBegin()
    {
        if (!awaken) Awaken();

        // Reset episode state
        strategicTargetHex = null;
        lastDistanceToTarget = float.MaxValue;
        phase = AgentPhase.EvaluateObjectives;
        currentObjective = HexObjectiveType.None;

        // Configure action space
        ConfigureActionSpace();

        base.OnEpisodeBegin();
        hasEpisodeBegun = true;
    }

    /// <summary>
    /// Configure the discrete action space for the ML agent
    /// </summary>
    private void ConfigureActionSpace()
    {
        behaviorParams ??= GetComponent<BehaviorParameters>();
        if (behaviorParams != null)
        {
            behaviorParams.BehaviorName = "Character";
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(new int[] {
                OBJECTIVE_TYPE_COUNT,
                TARGET_PRIORITY_LEVELS,
                MOVEMENT_STRATEGY_COUNT,
                allPossibleActions?.Count ?? 1
            });
        }
    }

    #endregion

    #region 2. Turn Initiation (Called Each Turn)

    /// <summary>
    /// Called at the start of each turn to initiate decision making
    /// Triggers: RequestDecision() → CollectObservations() → WriteDiscreteActionMask() → OnActionReceived()
    /// </summary>
    public void NewTurn(bool isPlayerControlled, bool isTrainingMode)
    {
        if (!awaken) Awaken();
        
        this.isPlayerControlled = isPlayerControlled;
        this.isTrainingMode = isTrainingMode;

        if (!hasEpisodeBegun) OnEpisodeBegin();

        // Reset phase for new turn
        phase = AgentPhase.EvaluateObjectives;
        RequestDecision();
    }

    #endregion

    #region 3. ML Pipeline - Observation Collection

    /// <summary>
    /// Called immediately after RequestDecision() to collect observations for the ML model
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (!awaken) Awaken();

        // Ensure objectives are evaluated for current step
        if (hasEpisodeBegun && controlledCharacter != null)
        {
            objectiveEvaluator.EvaluateHexObjectives(controlledCharacter);
        }

        // Use ObservationBuilder to construct observations
        observationBuilder.BuildObservations(sensor, controlledCharacter, currentObjective, 
            objectiveEvaluator, hasEpisodeBegun);
    }

    #endregion

    #region 4. ML Pipeline - Action Masking

    /// <summary>
    /// Masks unavailable actions to guide the ML model's decision making
    /// Called after CollectObservations() and before model inference
    /// </summary>
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!awaken) Awaken();
        if (!hasEpisodeBegun) return;

        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams == null) return;

        // Apply phase-specific masking
        switch (phase)
        {
            case AgentPhase.EvaluateObjectives:           
                objectiveEvaluator.EvaluateHexObjectives(controlledCharacter);
                break;
                
            case AgentPhase.ChooseAction:
            
                // Enable all actions by default
                for (int branch = 0; branch < behaviorParams.BrainParameters.ActionSpec.BranchSizes.Length; branch++)
                {
                    for (int i = 0; i < behaviorParams.BrainParameters.ActionSpec.BranchSizes[branch]; i++)
                    {
                        actionMask.SetActionEnabled(branch, i, true);
                    }
                }

                MaskUnavailableActions(actionMask);
                break;
        }
    }

    /// <summary>
    /// Mask actions that are not currently available
    /// </summary>
    private void MaskUnavailableActions(IDiscreteActionMask actionMask)
    {
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
        availableActionsIds = GetAvailableActionIds(controlledCharacter);
        
        if (availableActionsIds.Count < 1)
        {
            availableActionsIds.Add(DEFAULT_ACTION.actionId);
        }

        for (int actionIndex = 0; actionIndex < allPossibleActions.Count; actionIndex++)
        {
            int actionId = allPossibleActions[actionIndex].actionId;
            bool isAvailable = availableActionsIds.Contains(actionId);
            actionMask.SetActionEnabled(ACTION_BRANCH, actionIndex, isAvailable);
        }
    }

    #endregion

    #region 5. ML Pipeline - Heuristic (Fallback)

    /// <summary>
    /// Provides default heuristic actions when not using trained model
    /// Called instead of model inference when in heuristic mode
    /// </summary>
    /*public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!awaken) Awaken();
        var discreteActions = actionsOut.DiscreteActions;

        if (!hasEpisodeBegun)
        {
            // Provide safe default actions
            discreteActions[OBJECTIVE_TYPE_BRANCH] = 0; // First objective type
            discreteActions[TARGET_HEX_BRANCH] = 0;     // High priority target
            discreteActions[MOVEMENT_BRANCH] = 0;       // Direct movement
            discreteActions[ACTION_BRANCH] = GameObject.FindFirstObjectByType<ActionsManager>()?.GetDefault() ?? 0;
        }
    }*/

    #endregion

    #region 6. ML Pipeline - Action Processing (Core Logic)

    /// <summary>
    /// Main ML pipeline method - processes actions from the model/heuristic
    /// Implements the three-phase decision process: Objectives → Movement → Action
    /// Called after model inference or heuristic
    /// </summary>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (!awaken) Awaken();
        if (!hasEpisodeBegun) return;

        switch (phase)
        {
            case AgentPhase.EvaluateObjectives:
                ProcessObjectiveSelection(actionBuffers);
                break;

            case AgentPhase.MoveTowardsObjective:
                ProcessMovementPlanning(actionBuffers);
                break;

            case AgentPhase.ChooseAction:
                ProcessActionSelection(actionBuffers);
                break;
        }
    }

    /// <summary>
    /// Phase 1: Select objective type and target hex
    /// </summary>
    private void ProcessObjectiveSelection(ActionBuffers actionBuffers)
    {
        int objectiveTypeIndex = actionBuffers.DiscreteActions[OBJECTIVE_TYPE_BRANCH];
        currentObjective = (HexObjectiveType)(objectiveTypeIndex + 1);

        var candidateHexes = objectiveEvaluator.GetCandidateHexes(controlledCharacter, currentObjective);
        
        // Select target based on priority level
        int priorityLevel = actionBuffers.DiscreteActions[TARGET_HEX_BRANCH];
        int targetIndex = CalculateTargetIndex(priorityLevel, candidateHexes.Count);
        
        if (candidateHexes.Count > 0)
        {
            strategicTargetHex = candidateHexes[targetIndex];
            lastDistanceToTarget = Vector2Int.Distance(controlledCharacter.hex.v2, strategicTargetHex.v2);
        }
        else if (controlledCharacter.relevantHexes.Count > 0)
        {
            strategicTargetHex = controlledCharacter.relevantHexes[0];
            lastDistanceToTarget = Vector2Int.Distance(controlledCharacter.hex.v2, strategicTargetHex.v2);
        }

        phase = AgentPhase.MoveTowardsObjective;
        RequestDecision();
    }

    /// <summary>
    /// Phase 2: Plan and execute movement towards target
    /// </summary>
    private void ProcessMovementPlanning(ActionBuffers actionBuffers)
    {
        if (strategicTargetHex == null || controlledCharacter.reachableHexes.Count == 0)
        {
            phase = AgentPhase.ChooseAction;
            RequestDecision();
            return;
        }

        int movementStrategyIndex = actionBuffers.DiscreteActions[MOVEMENT_BRANCH];
        var movementStrategy = (MovementStrategy)movementStrategyIndex;
        
        var movementResult = movementPlanner.PlanMovement(strategicTargetHex, controlledCharacter, movementStrategy);
        
        if (movementResult.Success && !isPlayerControlled)
        {
            movementPlanner.ExecuteMovement(controlledCharacter, movementResult.DestinationHex);
            
            // Reward movement progress
            if (movementResult.DistanceImproved)
            {
                AddReward(0.05f);
            }
            
            lastDistanceToTarget = movementResult.NewDistance;
        }

        phase = AgentPhase.ChooseAction;
        RequestDecision();
    }

    /// <summary>
    /// Phase 3: Select and execute final action
    /// </summary>
    private void ProcessActionSelection(ActionBuffers actionBuffers)
    {
        int selectedActionIndex = actionBuffers.DiscreteActions[ACTION_BRANCH];
        
        // Validate and select action
        if (selectedActionIndex >= 0 && selectedActionIndex < allPossibleActions.Count)
        {
            chosenAction = allPossibleActions[selectedActionIndex];
            if (!availableActionsIds.Contains(chosenAction.actionId))
            {
                chosenAction = DEFAULT_ACTION;
            }
        }
        else
        {
            chosenAction = DEFAULT_ACTION;
        }

        // Execute action if not player controlled
        if (!isPlayerControlled)
        {
            actionExecutor.ExecuteAction(chosenAction, controlledCharacter, currentObjective, 
            strategicTargetHex, isPlayerControlled, isTrainingMode);
        }

        // Reset phase for next turn
        phase = AgentPhase.EvaluateObjectives;
    }
    #endregion

    #region 7. Training Interface

    /// <summary>
    /// Handles player feedback when actions are chosen manually during training
    /// Delegates to TrainingManager for training-specific logic
    /// </summary>
    public void FeedbackWithPlayerActions(CharacterAction action)
    {
        trainingManager.FeedbackWithPlayerActions(action, currentObjective);
    }

    #endregion

    #region 8. Helper Methods

    /// <summary>
    /// Calculate target index based on priority level
    /// </summary>
    private int CalculateTargetIndex(int priorityLevel, int candidateCount)
    {
        if (candidateCount == 0) return 0;

        int targetIndex = priorityLevel switch
        {
            0 => 0, // High priority - first target
            1 => candidateCount > 2 ? candidateCount / 2 : 0, // Medium priority - middle target
            2 => candidateCount > 3 ? candidateCount * 2 / 3 : 0, // Low priority - later target
            _ => 0
        };

        return Mathf.Clamp(targetIndex, 0, candidateCount - 1);
    }

    /// <summary>
    /// Get list of action IDs that are currently available
    /// </summary>
    private List<int> GetAvailableActionIds(Character character)
    {
        if (character == null || allPossibleActions == null) return new List<int>();
        return allPossibleActions
            .Where(action => action != null && action.FulfillsConditions())
            .Select(a => a.actionId)
            .ToList();
    }

    #endregion

    #region 9. Public Interface Methods

    public Character GetCharacter() => controlledCharacter;
    public List<CharacterAction> GetAvailableActions() => allPossibleActions?.Where(a => a != null && a.ResourcesAvailable()).ToList() ?? new List<CharacterAction>();
    public void SetPlayerControlled(bool isControlled) => isPlayerControlled = isControlled;
    public void SetTrainingMode(bool training) => isTrainingMode = training;
    public CharacterAction GetChosenAction() => chosenAction;
    
    // Internal methods for TrainingManager
    internal void SetChosenAction(CharacterAction action) => chosenAction = action;
    internal void SetPhase(AgentPhase newPhase) => phase = newPhase;
    internal bool IsTrainingMode => isTrainingMode;

    #endregion
}
