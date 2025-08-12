using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles action execution and reward calculation for ML training
/// </summary>
public class ActionExecutor
{
    private readonly GameState gameState;
    private readonly StrategyGameAgent agent;

    public ActionExecutor(GameState gameState, StrategyGameAgent agent)
    {
        this.gameState = gameState;
        this.agent = agent;
    }

    /// <summary>
    /// Executes the chosen action and calculates rewards
    /// </summary>
    public void ExecuteAction(CharacterAction chosenAction, Character character, HexObjectiveType currentObjective, 
        Hex strategicTargetHex, bool isPlayerControlled, bool isTrainingMode)
    {
        if (chosenAction == null || character == null) return;

        var leader = character.GetOwner();
        if (leader == null) return;

        // Capture state before action
        var stateBefore = CaptureGameState(leader);

        // Execute action
        bool applyRewards = isTrainingMode;
        if (!isPlayerControlled)
        {
            applyRewards &= chosenAction.ExecuteAI();
        }

        // Calculate and apply rewards
        if (applyRewards)
        {
            var stateAfter = CaptureGameState(leader);
            CalculateAndApplyRewards(stateBefore, stateAfter, chosenAction, currentObjective, strategicTargetHex, character);
            
            CheckGameEndConditions(leader);
        }
    }

    private GameStateSnapshot CaptureGameState(Leader leader)
    {
        return new GameStateSnapshot
        {
            WasBankrupted = leader.GetGoldPerTurn() < 0,
            WasNegative = leader.goldAmount < 0,
            StorePoints = leader.GetStorePoints(),
            EnemyStrength = gameState.GetEnemyPoints(leader),
            FriendlyStrength = gameState.GetFriendlyPoints(leader),
            CharacterPoints = leader.GetCharacterPoints(),
            PcStrength = leader.GetPCPoints(),
            ArmyStrength = leader.GetArmyPoints(),
            TerritoryControl = gameState.CountControlledHexes(leader),
            ResourceProduction = leader.GetResourceProductionPoints(),
            AverageCharHealth = gameState.GetAverageCharacterHealth(leader),
            StrategicLocations = gameState.CountStrategicLocations(leader),
            Artifacts = gameState.CountArtifacts(leader),
            Leader = leader
        };
    }

    private void CalculateAndApplyRewards(GameStateSnapshot before, GameStateSnapshot after, 
        CharacterAction action, HexObjectiveType objective, Hex targetHex, Character character)
    {
        // Base action reward
        float actionReward = action.reward / 10f;
        agent.AddReward(actionReward);

        // Economic rewards
        ApplyEconomicRewards(before, after);

        // Strategic rewards
        ApplyStrategicRewards(before, after);

        // Objective completion reward
        ApplyObjectiveReward(before, after, objective, targetHex, character);
    }

    private void ApplyEconomicRewards(GameStateSnapshot before, GameStateSnapshot after)
    {
        // Bankruptcy recovery/prevention
        if (before.WasBankrupted && !after.IsBankrupted) agent.AddReward(0.5f);
        if (before.WasNegative && !after.IsNegative) agent.AddReward(0.5f);
        if (!before.WasBankrupted && after.IsBankrupted) agent.AddReward(-0.5f);
        if (!before.WasNegative && after.IsNegative) agent.AddReward(-0.5f);

        // Resource and strength improvements
        agent.AddReward((after.StorePoints - before.StorePoints) / 100f);
        agent.AddReward((after.FriendlyStrength - before.FriendlyStrength) / 100f);
        agent.AddReward((before.EnemyStrength - after.EnemyStrength) / 100f);
        agent.AddReward((after.CharacterPoints - before.CharacterPoints) / 100f);
        agent.AddReward((after.PcStrength - before.PcStrength) / 100f);
        agent.AddReward((after.ArmyStrength - before.ArmyStrength) / 100f);
    }

    private void ApplyStrategicRewards(GameStateSnapshot before, GameStateSnapshot after)
    {
        agent.AddReward((after.TerritoryControl - before.TerritoryControl) / 50f);
        agent.AddReward((after.ResourceProduction - before.ResourceProduction) / 50f);
        agent.AddReward((after.AverageCharHealth - before.AverageCharHealth) / 25f);
        agent.AddReward((after.StrategicLocations - before.StrategicLocations) / 5f);
        agent.AddReward((after.Artifacts - before.Artifacts) * 2f);
    }

    private void ApplyObjectiveReward(GameStateSnapshot before, GameStateSnapshot after, 
        HexObjectiveType objective, Hex targetHex, Character character)
    {
        if (objective == HexObjectiveType.None || targetHex == null) return;

        bool objectiveCompleted = objective switch
        {
            HexObjectiveType.AttackEnemy => after.EnemyStrength < before.EnemyStrength,
            HexObjectiveType.DefendAlly => after.AverageCharHealth > before.AverageCharHealth,
            HexObjectiveType.GatherResource => after.StorePoints > before.StorePoints || after.ResourceProduction > before.ResourceProduction,
            HexObjectiveType.SecureTerritory => after.TerritoryControl > before.TerritoryControl || after.StrategicLocations > before.StrategicLocations,
            HexObjectiveType.RetrieveArtifact => after.Artifacts > before.Artifacts,
            HexObjectiveType.RetreatToSafety => gameState.EvaluateSafetyScore(character.hex, character) > 0.6f,
            _ => false
        };

        if (objectiveCompleted)
        {
            agent.AddReward(3.0f);
        }
    }

    private void CheckGameEndConditions(Leader leader)
    {
        if (IsGameOver(leader))
        {
            agent.AddReward(IsWinner(leader) ? 25f : -25f);
            agent.EndEpisode();
        }
    }

    private bool IsGameOver(Leader leader)
    {
        return leader?.killed ?? false;
    }

    private bool IsWinner(Leader leader)
    {
        return gameState.GetWinner() == leader;
    }

    private struct GameStateSnapshot
    {
        public bool WasBankrupted;
        public bool WasNegative;
        public int StorePoints;
        public int EnemyStrength;
        public int FriendlyStrength;
        public int CharacterPoints;
        public int PcStrength;
        public int ArmyStrength;
        public int TerritoryControl;
        public int ResourceProduction;
        public float AverageCharHealth;
        public int StrategicLocations;
        public int Artifacts;
        public Leader Leader;

        public bool IsBankrupted => Leader != null && Leader.GetGoldPerTurn() < 0;
        public bool IsNegative => Leader != null && Leader.goldAmount < 0;
    }
}