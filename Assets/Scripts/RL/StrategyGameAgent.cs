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

    // Constants for observation sizes
    private const int MAX_HEXES = 100; // Maximum number of hexes to observe
    private const int MAX_LEADERS = 8; // Maximum number of leaders
    private const int MAX_CHARACTERS = 50; // Maximum number of characters
    private const int MAX_ARTIFACTS = 20; // Maximum number of artifacts

    // Observation component sizes
    private const int POSITION_OBS_SIZE = 2; // x, y
    private const int TERRAIN_OBS_SIZE = 2; // type, cost
    private const int UNITS_OBS_SIZE = 1; // count
    private const int ARMIES_OBS_SIZE = 1; // count
    private const int PC_OBS_SIZE = 4; // presence, artifacts, owner type, defense
    private const int RESOURCES_OBS_SIZE = 12; // 6 resources * 2 (amount and per turn)
    private const int CHARACTER_OBS_SIZE = 20; // status, position, health, alignment, etc.
    private const int TURN_OBS_SIZE = 1;

    public override void Initialize()
    {
        base.Initialize();
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = GetComponent<Character>();
        allPossibleActions = FindObjectsByType<CharacterAction>(FindObjectsSortMode.None).ToList();
    }

    public void NewTurn()
    {
        allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (gameState == null || controlledCharacter == null) return;

        int observationCount = 0;

        // Cache frequently used values
        var relevantHexes = gameState.GetRelevantHexes(controlledCharacter);
        var boardSize = gameState.GetBoardSize();
        var maxX = gameState.GetMaxX();
        var maxY = gameState.GetMaxY();
        var leadersNum = gameState.GetLeadersNum();
        var allArtifactsNum = gameState.GetAllArtifactsNum();
        var allCharacters = gameState.GetAllCharacters();
        var leaders = gameState.GetLeaders();
        var characterAlignment = controlledCharacter.GetAlignment();

        // Add count of relevant hexes (normalized)
        sensor.AddObservation(relevantHexes.Count / (float)boardSize);
        observationCount++;

        // Pre-calculate terrain type count
        int terrainTypeCount = Enum.GetValues(typeof(TerrainEnum)).Length - 1;
        float maxTerrainCost = TerrainData.terrainCosts.Values.Max();

        // Process hexes (up to MAX_HEXES)
        int hexesProcessed = 0;
        foreach (Hex hex in relevantHexes)
        {
            if (hexesProcessed >= MAX_HEXES) break;
            hexesProcessed++;

            // Position (normalized)
            sensor.AddObservation(hex.v2.x / maxX);
            sensor.AddObservation(hex.v2.y / maxY);
            observationCount += 2;

            // Terrain type and cost
            sensor.AddOneHotObservation((int)hex.terrainType, terrainTypeCount);
            sensor.AddObservation(TerrainData.terrainCosts[hex.terrainType] / maxTerrainCost);
            observationCount += terrainTypeCount + 1;

            // Unit presence
            int[] unitsByOwner = new int[MAX_LEADERS];
            int totalUnits = 0;
            foreach (Character character in hex.characters)
            {
                if (character.killed) continue;
                int leaderIndex = gameState.GetIndexOfLeader(character.GetOwner());
                if (leaderIndex < MAX_LEADERS)
                {
                    unitsByOwner[leaderIndex]++;
                    totalUnits++;
                }
            }
            
            // Normalize and add unit observations
            float totalUnitsFloat = totalUnits > 0 ? totalUnits : 1f;
            for (int i = 0; i < MAX_LEADERS; i++)
            {
                sensor.AddObservation(unitsByOwner[i] / totalUnitsFloat);
                observationCount++;
            }

            // Army presence
            int[] armiesByOwner = new int[MAX_LEADERS];
            int totalArmies = 0;
            foreach (Army army in hex.armies)
            {
                if (army.commander == null || army.commander.killed) continue;
                int leaderIndex = gameState.GetIndexOfLeader(army.commander.GetOwner());
                if (leaderIndex < MAX_LEADERS)
                {
                    armiesByOwner[leaderIndex]++;
                    totalArmies++;
                }
            }

            // Normalize and add army observations
            float totalArmiesFloat = totalArmies > 0 ? totalArmies : 1f;
            for (int i = 0; i < MAX_LEADERS; i++)
            {
                sensor.AddObservation(armiesByOwner[i] / totalArmiesFloat);
                observationCount++;
            }

            // PC presence and properties
            var pc = hex.GetPC();
            bool hasPC = pc != null;
            sensor.AddOneHotObservation(hasPC ? 1 : 0, 2);
            sensor.AddObservation(allArtifactsNum < 1 ? 0 : hex.hiddenArtifacts.Count / (float)MAX_ARTIFACTS);
            sensor.AddOneHotObservation(hasPC && pc.owner is NonPlayableLeader ? 1 : 0, 2);
            observationCount += 2 + 1 + 2;

            // PC alignment and defense
            if (hasPC)
            {
                float defense = pc.GetDefense() / 1000f;
                if (pc.owner.alignment != AlignmentEnum.neutral && pc.owner.alignment == characterAlignment)
                    sensor.AddObservation(defense);
                else
                    sensor.AddObservation(0f);

                if (pc.owner.alignment != characterAlignment)
                    sensor.AddObservation(defense);
                else
                    sensor.AddObservation(0f);

                if (pc.owner.alignment == AlignmentEnum.neutral)
                    sensor.AddObservation(defense);
                else
                    sensor.AddObservation(0f);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            observationCount += 3;
        }

        // Add padding for remaining hexes
        for (int i = hexesProcessed; i < MAX_HEXES; i++)
        {
            // Add zero observations for unused hex slots
            sensor.AddObservation(0f); // x
            sensor.AddObservation(0f); // y
            sensor.AddOneHotObservation(0, terrainTypeCount); // terrain type
            sensor.AddObservation(0f); // terrain cost
            for (int j = 0; j < MAX_LEADERS; j++) sensor.AddObservation(0f); // units
            for (int j = 0; j < MAX_LEADERS; j++) sensor.AddObservation(0f); // armies
            sensor.AddOneHotObservation(0, 2); // PC presence
            sensor.AddObservation(0f); // artifacts
            sensor.AddOneHotObservation(0, 2); // PC owner type
            sensor.AddObservation(0f); // PC defense 1
            sensor.AddObservation(0f); // PC defense 2
            sensor.AddObservation(0f); // PC defense 3
            observationCount += 2 + terrainTypeCount + 1 + MAX_LEADERS + MAX_LEADERS + 2 + 1 + 2 + 3;
        }

        // Add player resources (normalized)
        int leadersProcessed = 0;
        foreach (var player in leaders)
        {
            if (leadersProcessed >= MAX_LEADERS) break;
            leadersProcessed++;

            sensor.AddObservation(player.goldAmount / 1000f);
            sensor.AddObservation(player.leatherAmount / 1000f);
            sensor.AddObservation(player.mithrilAmount / 1000f);
            sensor.AddObservation(player.mountsAmount / 1000f);
            sensor.AddObservation(player.ironAmount / 1000f);
            sensor.AddObservation(player.timberAmount / 1000f);

            sensor.AddObservation(player.GetGoldPerTurn() / 100f);
            sensor.AddObservation(player.GetLeatherPerTurn() / 100f);
            sensor.AddObservation(player.GetMithrilPerTurn() / 100f);
            sensor.AddObservation(player.GetMountsPerTurn() / 100f);
            sensor.AddObservation(player.GetIronPerTurn() / 100f);
            sensor.AddObservation(player.GetTimberPerTurn() / 100f);
            observationCount += 12;
        }

        // Add padding for remaining leaders
        for (int i = leadersProcessed; i < MAX_LEADERS; i++)
        {
            for (int j = 0; j < RESOURCES_OBS_SIZE; j++)
            {
                sensor.AddObservation(0f);
                observationCount++;
            }
        }

        // Add character information
        int charactersProcessed = 0;
        foreach (var character in allCharacters)
        {
            if (charactersProcessed >= MAX_CHARACTERS) break;
            charactersProcessed++;

            sensor.AddOneHotObservation(character.killed ? 1 : 0, 2);
            sensor.AddOneHotObservation(character is NonPlayableLeader && (character as NonPlayableLeader).joined ? 1 : 0, 2);
            sensor.AddObservation(gameState.GetIndexOfLeader(character.GetOwner()) / (float)MAX_LEADERS);
            sensor.AddObservation(character.hex.v2.x / maxX);
            sensor.AddObservation(character.hex.v2.y / maxY);
            sensor.AddObservation(character.health / 100f);
            sensor.AddOneHotObservation((int)character.GetAlignment(), terrainTypeCount);
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
            observationCount += 2 + 2 + 1 + 2 + 1 + terrainTypeCount + 2 + 2 + 2 + 2 + 2 + 4 + 2;

            if (character.IsArmyCommander())
            {
                var army = character.GetArmy();
                sensor.AddObservation(army.GetSize() / 100f);
                sensor.AddObservation(army.GetDefence() / 100f);
                sensor.AddObservation(army.GetOffence() / 100f);
                sensor.AddObservation(army.ma / 100f);
                sensor.AddObservation(army.ar / 100f);
                sensor.AddObservation(army.li / 100f);
                sensor.AddObservation(army.hi / 100f);
                sensor.AddObservation(army.lc / 100f);
                sensor.AddObservation(army.hc / 100f);
                sensor.AddObservation(army.ca / 100f);
                sensor.AddObservation(army.ws / 100f);
                observationCount += 11;
            }
            else
            {
                for (int i = 0; i < 11; i++) sensor.AddObservation(0f);
                observationCount += 11;
            }

            sensor.AddObservation(allArtifactsNum < 1 ? 0 : character.artifacts.Count / (float)MAX_ARTIFACTS);
            observationCount++;
        }

        // Add padding for remaining characters
        for (int i = charactersProcessed; i < MAX_CHARACTERS; i++)
        {
            for (int j = 0; j < CHARACTER_OBS_SIZE; j++)
            {
                sensor.AddObservation(0f);
                observationCount++;
            }
        }

        // Add turn information
        sensor.AddObservation(gameState.GetTurn() / 100f);
        observationCount++;

        Debug.Log($"Total observations added: {observationCount}");
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

    public int GetTotalObservationSize()
    {
        int terrainTypeCount = Enum.GetValues(typeof(TerrainEnum)).Length - 1;

        // Calculate size for each component
        int hexObservations = 1 + // hex count
            (POSITION_OBS_SIZE + // x, y
            terrainTypeCount + // terrain type (one-hot)
            1 + // terrain cost
            MAX_LEADERS + // units by owner
            MAX_LEADERS + // armies by owner
            2 + // PC presence (one-hot)
            1 + // artifacts
            2 + // PC owner type (one-hot)
            3) * MAX_HEXES; // PC defense values

        int resourceObservations = RESOURCES_OBS_SIZE * MAX_LEADERS;

        int characterObservations = 
            (2 + // killed (one-hot)
            2 + // joined (one-hot)
            1 + // owner index
            2 + // position
            1 + // health
            terrainTypeCount + // alignment (one-hot)
            2 + // killed (one-hot)
            2 + // hasMoved (one-hot)
            2 + // hasActioned (one-hot)
            2 + // isEmbarked (one-hot)
            2 + // startingCharacter (one-hot)
            4 + // commander, agent, emmissary, mage
            2 + // isArmyCommander (one-hot)
            11 + // army stats
            1) * MAX_CHARACTERS; // artifacts

        int turnObservations = TURN_OBS_SIZE;

        int totalSize = hexObservations + resourceObservations + characterObservations + turnObservations;
        Debug.Log($"Calculated observation size: {totalSize}");
        return totalSize;
    }
}