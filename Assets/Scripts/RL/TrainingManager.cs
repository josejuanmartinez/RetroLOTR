using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;

/// <summary>
/// Handles training-specific functionality for the StrategyGameAgent
/// Manages player feedback, training mode settings, and training-related utilities
/// </summary>
public class TrainingManager
{
    private readonly StrategyGameAgent agent;
    private readonly List<CharacterAction> allPossibleActions;

    public TrainingManager(StrategyGameAgent agent, List<CharacterAction> allPossibleActions)
    {
        this.agent = agent;
        this.allPossibleActions = allPossibleActions;
    }

    /// <summary>
    /// Handles feedback when player manually chooses actions during training
    /// Converts player action to ML action format and feeds it through the pipeline
    /// </summary>
    public void FeedbackWithPlayerActions(CharacterAction action, HexObjectiveType currentObjective)
    {
        if (!agent.IsTrainingMode) return;

        // Set the chosen action
        agent.SetChosenAction(action);

        // Create action buffer that represents what the ML model would have chosen
        int[] discreteActionsArray = new int[4];
        discreteActionsArray[0] = (int)currentObjective; // OBJECTIVE_TYPE_BRANCH
        discreteActionsArray[1] = 0; // TARGET_HEX_BRANCH - default to high priority
        discreteActionsArray[2] = 0; // MOVEMENT_BRANCH - default to direct movement
        discreteActionsArray[3] = GetActionIndex(action); // ACTION_BRANCH

        ActionBuffers buffers = new ActionBuffers(new float[] { }, discreteActionsArray);
        
        // Force agent to action selection phase and process the feedback
        agent.SetPhase(StrategyGameAgent.AgentPhase.ChooseAction);
        agent.OnActionReceived(buffers);
    }

    /// <summary>
    /// Gets the index of an action in the possible actions list
    /// </summary>
    private int GetActionIndex(CharacterAction action)
    {
        if (allPossibleActions == null || action == null) return 0;
        
        int index = allPossibleActions.FindIndex(x => x.actionId == action.actionId);
        return index >= 0 ? index : 0;
    }

    /// <summary>
    /// Configures training parameters for the agent
    /// </summary>
    public void ConfigureTraining(bool isTrainingMode, float learningRate = 0.0003f)
    {
        agent.SetTrainingMode(isTrainingMode);
        
        // Additional training configuration can be added here
        // e.g., curriculum learning parameters, exploration settings, etc.
    }

    /// <summary>
    /// Provides training statistics and metrics
    /// </summary>
    public TrainingMetrics GetTrainingMetrics()
    {
        return new TrainingMetrics
        {
            CurrentReward = agent.GetCumulativeReward(),
            EpisodeCount = agent.CompletedEpisodes,
            StepCount = agent.StepCount,
            IsTraining = agent.IsTrainingMode
        };
    }

    /// <summary>
    /// Resets training state for new episode
    /// </summary>
    public void ResetTrainingState()
    {
        // Any training-specific reset logic can go here
        // Currently handled by the main agent's OnEpisodeBegin
    }
}

/// <summary>
/// Training metrics for monitoring agent performance
/// </summary>
public struct TrainingMetrics
{
    public float CurrentReward;
    public int EpisodeCount;
    public int StepCount;
    public bool IsTraining;
}