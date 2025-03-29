using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System;

[RequireComponent(typeof(Character))]
public class StrategyGameAgent : Agent
{
    [Header("Character info references")]
    [SerializeField] private GameState gameState;
    [SerializeField] private List<CharacterAction> allPossibleActions;
    [SerializeField] private Character controlledCharacter;

    public override void OnEpisodeBegin()
    {
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = GetComponent<Character>();
        allPossibleActions = FindObjectsByType<CharacterAction>(FindObjectsSortMode.None).ToList();
    }

    public void NewTurn()
    {
        // Reset the game state at the beginning of each episode
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        List<Hex> relevantHexes = gameState.GetRelevantHexes(controlledCharacter);
        // Add count of relevant hexes (so the agent knows how many to expect)
        sensor.AddObservation(relevantHexes.Count / gameState.GetBoardSize()); // Normalize
        
        foreach (Hex hex in gameState.GetRelevantHexes(controlledCharacter))
        {
            // Normalize position
            sensor.AddObservation(hex.v2.x / gameState.GetMaxX());
            sensor.AddObservation(hex.v2.y / gameState.GetMaxY());

            // One-hot encode terrain type
            sensor.AddOneHotObservation((int)hex.terrainType, Enum.GetValues(typeof(TerrainEnum)).Length - 1); // Assuming 5 terrain types
            sensor.AddObservation(TerrainData.terrainCosts[hex.terrainType] / TerrainData.terrainCosts.Values.Max()); // Normalize movement cost

            // Unit presence (count of units by owner)
            int[] unitsByOwner = new int[gameState.GetLeadersNum()]; // Assuming 5 players max
            int totalUnits = 0;
            foreach (Character character in hex.characters)
            {
                if(character.killed) return;
                unitsByOwner[gameState.GetIndexOfLeader(character.GetOwner())]++;
                totalUnits++;
            }

            for (int i = 0; i < unitsByOwner.Length; i++)
            {
                sensor.AddObservation(unitsByOwner[i] / totalUnits); // Normalize
            }

            // Army presence (count of armies by owner)
            int[] armiesByOwner = new int[gameState.GetLeadersNum()]; // Assuming 5 players max
            int totalArmies = 0;
            foreach (Army army in hex.armies)
            {
                if (army.commander == null || army.commander.killed) return;
                armiesByOwner[gameState.GetIndexOfLeader(army.commander.GetOwner())]++;
                totalArmies++;
            }

            for (int i = 0; i < unitsByOwner.Length; i++)
            {
                sensor.AddObservation(armiesByOwner[i] / totalArmies); // Normalize
            }

            // PC presence
            sensor.AddOneHotObservation(hex.GetPC() != null ? 1 : 0, 2); // One-hot encode presence of player character

            // Artifact presence
            sensor.AddObservation(hex.hiddenArtifacts.Count() / gameState.GetAllArtifactsNum());
            
            // NPC PC presence?
            sensor.AddOneHotObservation(hex.GetPC() != null && hex.GetPC().owner is NonPlayableLeader ? 1 : 0, 2);

            // Friendly or enemy PC presence?
            sensor.AddObservation(hex.GetPC() != null && hex.GetPC().owner.alignment != AlignmentEnum.neutral && hex.GetPC().owner.alignment == controlledCharacter.GetAlignment() ? hex.GetPC().GetDefense() / 1000f: 0f);

            // Enemy PC presence?
            sensor.AddObservation(hex.GetPC() != null && hex.GetPC().owner.alignment != controlledCharacter.GetAlignment() ? hex.GetPC().GetDefense() : 0);

            // Neutral PC presence?
            sensor.AddObservation(hex.GetPC() != null && hex.GetPC().owner.alignment == AlignmentEnum.neutral ? hex.GetPC().GetDefense() : 0);
        }

        // Add player resources
        foreach (var player in gameState.GetLeaders())
        {
            sensor.AddObservation(player.goldAmount / 1000f);
            sensor.AddObservation(player.leatherAmount / 1000f);
            sensor.AddObservation(player.mithrilAmount / 1000f);
            sensor.AddObservation(player.mountsAmount / 1000f);
            sensor.AddObservation(player.ironAmount / 1000f);
            sensor.AddObservation(player.timberAmount / 1000f);

            sensor.AddObservation(player.GetGoldPerTurn() / 100f);
            sensor.AddObservation(player.GetLeatherPerTurn()/ 100f);
            sensor.AddObservation(player.GetMithrilPerTurn() / 100f);
            sensor.AddObservation(player.GetMountsPerTurn() / 100f);
            sensor.AddObservation(player.GetIronPerTurn() / 100f);
            sensor.AddObservation(player.GetTimberPerTurn() / 100f);
        }

        // Add character information
        foreach (var character in gameState.GetAllCharacters())
        {
            sensor.AddOneHotObservation(character.killed ? 1 : 0, 2);
            sensor.AddOneHotObservation(character is NonPlayableLeader && (character as NonPlayableLeader).joined ? 1 : 0, 2);
            sensor.AddObservation(gameState.GetIndexOfLeader(character.GetOwner()) / gameState.GetLeadersNum());
            sensor.AddObservation(character.hex.v2.x / gameState.GetMaxX());
            sensor.AddObservation(character.hex.v2.y / gameState.GetMaxY());
            sensor.AddObservation(character.health / 100f);
            sensor.AddOneHotObservation((int)character.GetAlignment(), Enum.GetValues(typeof(AlignmentEnum)).Length - 1); // Assuming 3 alignments
            sensor.AddOneHotObservation(character.killed ? 0 : 1, 2);
            sensor.AddOneHotObservation(character.hasMovedThisTurn ? 0 : 1, 2);
            sensor.AddOneHotObservation(character.hasActionedThisTurn ? 0 : 1, 2);
            sensor.AddOneHotObservation(character.isEmbarked ? 0 : 1, 2);
            sensor.AddOneHotObservation(character.startingCharacter ? 0 : 1, 2);
            sensor.AddObservation(character.GetCommander() / 5f);
            sensor.AddObservation(character.GetAgent() / 5f);
            sensor.AddObservation(character.GetEmmissary() / 5f);
            sensor.AddObservation(character.GetMage() / 5f);
            sensor.AddOneHotObservation(character.IsArmyCommander() ? 1 : 0, 2);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().GetSize() / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().GetDefence() / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().GetOffence() / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().ma / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().ar / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().li / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().hi / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().lc / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().hc / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().ca / 100f : 0);
            sensor.AddObservation(character.IsArmyCommander() ? character.GetArmy().ws / 100f : 0);
            sensor.AddObservation(character.artifacts.Count() / gameState.GetAllArtifactsNum());
        }

        // Add turn information
        sensor.AddObservation(gameState.GetTurn() / 100f);
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
        // Mask unavailable actions
        List<int> availableActions = GetAvailableActionIds(controlledCharacter);

        // For branch 0 (assuming single branch with all actions)
        for (int i = 0; i < allPossibleActions.Count; i++)
        {
            // Set mask to false (unavailable) for all unavailable actions
            actionMask.SetActionEnabled(0, i, availableActions.Contains(i));
        }
    }

    private List<int> GetAvailableActionIds(Character character)
    {
        return allPossibleActions
        .Where(action => action.IsAvailable())
        .Select(action => action.actionId)
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