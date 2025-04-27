using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.UI;

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
    Board board;
    HexPathRenderer hexPathRenderer;

    private bool awaken = false;
    private bool hasEpisodeBegun = false;

    CharacterAction DEFAULT_ACTION;

    int alignmentTypeCount;

    // Feature-based decision making
    [SerializeField] private float enemyWeight = 1.5f;
    [SerializeField] private float resourceWeight = 1.0f;
    [SerializeField] private float territoryWeight = 0.8f;
    [SerializeField] private float artifactWeight = 2.0f;
    [SerializeField] private float allyWeight = 0.5f;

    // New phase system with feature-based decision making
    private enum AgentPhase { EvaluateObjectives, MoveTowardsObjective, ChooseAction }
    private AgentPhase phase = AgentPhase.EvaluateObjectives;

    private Hex strategicTargetHex;
    private float lastDistanceToTarget = float.MaxValue;
    private HexObjectiveType currentObjective = HexObjectiveType.None;

    // Objective types for more strategic decision making
    private enum HexObjectiveType
    {
        None,
        AttackEnemy,
        DefendAlly,
        GatherResource,
        SecureTerritory,
        RetrieveArtifact,
        RetreatToSafety
    }

    // Objective evaluation scores for each hex
    private Dictionary<Hex, float> hexObjectiveScores = new ();
    private Dictionary<Hex, HexObjectiveType> hexObjectiveTypes = new ();

    const int OBJECTIVE_TYPE_BRANCH = 0;    // What type of objective to pursue
    const int TARGET_HEX_BRANCH = 1;        // Which specific hex to target
    const int MOVEMENT_BRANCH = 2;          // How to move toward the target
    const int ACTION_BRANCH = 3;            // What action to take once there

    // Number of objective types (must match HexObjectiveType enum count - 1)
    const int OBJECTIVE_TYPE_COUNT = 6;
    // Number of priority levels for target selection
    const int TARGET_PRIORITY_LEVELS = 3;   // High, Medium, Low priority targets
    // Number of movement strategies
    const int MOVEMENT_STRATEGY_COUNT = 3;  // Direct, Cautious, Aggressive

    void Awaken()
    {
        gameState = FindFirstObjectByType<GameState>();
        controlledCharacter = transform.parent.GetComponent<Character>();
        allPossibleActions = FindFirstObjectByType<ActionsManager>().characterActions.ToList();
        behaviorParams = GetComponent<BehaviorParameters>();
        DEFAULT_ACTION = FindFirstObjectByType<ActionsManager>().DEFAULT;
        board = FindFirstObjectByType<Board>();
        hexPathRenderer = FindAnyObjectByType<HexPathRenderer>();

        // Pre-calculate alignment type count
        alignmentTypeCount = Enum.GetValues(typeof(AlignmentEnum)).Length - 1;

        // Reset chosen action
        chosenAction = null;

        awaken = true;
    }

    public override void OnEpisodeBegin()
    {
        if (!awaken) Awaken();

        strategicTargetHex = null;
        lastDistanceToTarget = float.MaxValue;
        phase = AgentPhase.EvaluateObjectives;
        currentObjective = HexObjectiveType.None;

        behaviorParams.BehaviorName = "Character";
        // Set the correct action space size
        behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(new int[] {
            OBJECTIVE_TYPE_COUNT,          // branch 0 - what kind of objective to pursue
            TARGET_PRIORITY_LEVELS,        // branch 1 - target priority level
            MOVEMENT_STRATEGY_COUNT,       // branch 2 - how to move toward the target
            allPossibleActions.Count       // branch 3 - what action to take once there
        });

        base.OnEpisodeBegin();

        hasEpisodeBegun = true;
    }

    /**
     * This method is called at the end to modify the action taken by the agent with custom heuristics
     * And it's important when Pytorch Training is not running
     */
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!awaken) Awaken();

        var discreteActions = actionsOut.DiscreteActions;

        // Skip processing if game hasn't started
        if (!hasEpisodeBegun)
        {
            // Just set default actions to avoid errors
            discreteActions[OBJECTIVE_TYPE_BRANCH] = (int)HexObjectiveType.None;
            discreteActions[TARGET_HEX_BRANCH] = 0;
            discreteActions[MOVEMENT_BRANCH] = 0;
            discreteActions[ACTION_BRANCH] = FindFirstObjectByType<ActionsManager>().GetDefault();
        }
        else
        {
            // Set reasonable defaults based on the current state
            // discreteActions[OBJECTIVE_TYPE_BRANCH] = DetermineDefaultObjective();
            // discreteActions[TARGET_HEX_BRANCH] = 0; // Medium priority
            // discreteActions[MOVEMENT_BRANCH] = 0; // Direct movement
            // discreteActions[ACTION_BRANCH] = allPossibleActions.FindIndex(0, x => x.actionId == DEFAULT_ACTION.actionId);
        }
    }

    // Determine a reasonable default objective based on the situation
    private int DetermineDefaultObjective()
    {
        if (controlledCharacter == null) return 0;

        // Check if we're in danger
        bool inDanger = IsInDanger();
        if (inDanger) return (int)HexObjectiveType.RetreatToSafety;

        // Check if there are nearby artifacts
        bool nearbyArtifacts = HasNearbyArtifacts();
        if (nearbyArtifacts) return (int)HexObjectiveType.RetrieveArtifact;

        // Check if there are vulnerable enemies
        bool vulnerableEnemies = HasVulnerableEnemiesNearby();
        if (vulnerableEnemies) return (int)HexObjectiveType.AttackEnemy;

        // Check if allies need help
        bool alliesNeedHelp = AlliesNeedDefense();
        if (alliesNeedHelp) return (int)HexObjectiveType.DefendAlly;

        // Default to resource gathering
        return (int)HexObjectiveType.GatherResource;
    }

    // Helper methods for determining default objectives
    private bool IsInDanger()
    {
        if (controlledCharacter == null || controlledCharacter.hex == null) return false;

        // Check for nearby enemies that might pose a threat
        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex == null) continue;

            int enemyCount = hex.characters.Count(x =>
                x != null && !x.killed &&
                (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()));

            // If there are multiple enemies or we're low on health
            if ((enemyCount > 1 || (enemyCount > 0 && controlledCharacter.health < 30)))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasNearbyArtifacts()
    {
        if (controlledCharacter == null) return false;

        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex != null && hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasVulnerableEnemiesNearby()
    {
        if (controlledCharacter == null) return false;

        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex == null) continue;

            var enemies = hex.characters.Where(x =>
                x != null && !x.killed &&
                (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()))
                .ToList();

            if (enemies.Any(e => e.health < 50)) // Vulnerable enemy
            {
                return true;
            }
        }
        return false;
    }

    private bool AlliesNeedDefense()
    {
        if (controlledCharacter == null) return false;

        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex == null) continue;

            var allies = hex.characters.Where(x =>
                x != null && !x.killed &&
                x.GetAlignment() == controlledCharacter.GetAlignment() &&
                x != controlledCharacter)
                .ToList();

            if (allies.Any(a => a.health < 50)) // Ally needs help
            {
                return true;
            }
        }
        return false;
    }

    public void NewTurn(bool isPlayerControlled, bool autoplay, bool isTrainingMode)
    {
        if (!awaken) Awaken();
        this.isPlayerControlled = isPlayerControlled;
        this.autoplay = autoplay;
        this.isTrainingMode = isTrainingMode;

        hexObjectiveScores.Clear();
        hexObjectiveTypes.Clear();

        if (!hasEpisodeBegun)
        {
            OnEpisodeBegin();
        }

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
            // Debug.Log($"- [{controlledCharacter.characterName}] AI controlled character making decision...");
            RequestDecision();
        }
    }

    private void EvaluateHexObjectives()
    {
        hexObjectiveScores.Clear();
        hexObjectiveTypes.Clear();

        if (controlledCharacter == null || controlledCharacter.relevantHexes == null) return;

        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex == null) continue;

            // Score each hex based on various objectives
            float attackScore = EvaluateAttackScore(hex);
            float defenseScore = EvaluateDefenseScore(hex);
            float resourceScore = EvaluateResourceScore(hex);
            float territoryScore = EvaluateTerritoryScore(hex);
            float artifactScore = EvaluateArtifactScore(hex);
            float safetyScore = EvaluateSafetyScore(hex);

            // Determine the primary objective for this hex
            float maxScore = Mathf.Max(attackScore, defenseScore, resourceScore, territoryScore, artifactScore, safetyScore);
            HexObjectiveType objectiveType = HexObjectiveType.None;

            if (maxScore == attackScore && attackScore > 0) objectiveType = HexObjectiveType.AttackEnemy;
            else if (maxScore == defenseScore && defenseScore > 0) objectiveType = HexObjectiveType.DefendAlly;
            else if (maxScore == resourceScore && resourceScore > 0) objectiveType = HexObjectiveType.GatherResource;
            else if (maxScore == territoryScore && territoryScore > 0) objectiveType = HexObjectiveType.SecureTerritory;
            else if (maxScore == artifactScore && artifactScore > 0) objectiveType = HexObjectiveType.RetrieveArtifact;
            else if (maxScore == safetyScore && safetyScore > 0) objectiveType = HexObjectiveType.RetreatToSafety;

            hexObjectiveScores[hex] = maxScore;
            hexObjectiveTypes[hex] = objectiveType;

            // Debug.Log($"Hex {hex.v2}: {objectiveType} with score {maxScore}");
        }
    }

    // Evaluation functions for different objective types
    private float EvaluateAttackScore(Hex hex)
    {
        float score = 0;

        // Check for enemy units
        foreach (var enemy in hex.characters.Where(x =>
            x != null && !x.killed &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment())))
        {
            // Stronger score for weaker enemies (better targets)
            score += enemyWeight * (100 - enemy.health) / 100f;
        }

        // Check for enemy armies
        foreach (var army in hex.armies.Where(x =>
            x != null && !x.killed && x.commander != null && !x.commander.killed &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment())))
        {
            score += enemyWeight * 0.5f; // Armies are secondary targets
        }

        // Check for enemy PCs
        var pc = hex.GetPC();
        if (pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral ||
                          pc.owner.GetAlignment() != controlledCharacter.GetAlignment()))
        {
            // Scale by inverse of defense - weaker PCs are better targets
            score += enemyWeight * (1 - Mathf.Min(pc.GetDefense() / 10000f, 1.0f));
        }

        return score;
    }

    private float EvaluateDefenseScore(Hex hex)
    {
        float score = 0;

        // Check for allied units that need defense
        foreach (var ally in hex.characters.Where(x =>
            x != null && !x.killed &&
            x.GetAlignment() == controlledCharacter.GetAlignment() &&
            x != controlledCharacter))
        {
            // Higher score for allies with lower health
            score += allyWeight * (1 - ally.health / 100f);
        }

        // Check for allied armies
        foreach (var army in hex.armies.Where(x =>
            x != null && !x.killed && x.commander != null && !x.commander.killed &&
            x.GetAlignment() == controlledCharacter.GetAlignment()))
        {
            score += allyWeight * 0.3f;
        }

        // Check for allied PCs
        var pc = hex.GetPC();
        if (pc != null && pc.owner.GetAlignment() == controlledCharacter.GetAlignment())
        {
            // Scale by inverse of defense - weaker allied PCs need more defense
            score += allyWeight * (1 - Mathf.Min(pc.GetDefense() / 10000f, 1.0f));
        }

        return score;
    }

    private float EvaluateResourceScore(Hex hex)
    {
        float score = 0;
        var owner = controlledCharacter.GetOwner();
        if (owner == null) return 0;

        // Check for resource value of the hex
        // This would depend on your game's economy system
        // For example:
        if (hex.terrainType == TerrainEnum.forest)
        {
            score += resourceWeight * (1 - Mathf.Min(owner.timberAmount / 2000f, 1.0f));
        }
        else if (hex.terrainType == TerrainEnum.mountains)
        {
            score += resourceWeight * (1 - Mathf.Min(owner.ironAmount / 2000f, 1.0f));
        }
        else if (hex.terrainType == TerrainEnum.grasslands)
        {
            score += resourceWeight * (1 - Mathf.Min(owner.leatherAmount / 2000f, 1.0f));
        }

        return score;
    }

    private float EvaluateTerritoryScore(Hex hex)
    {
        float score = 0;

        // Strategic value of the territory
        bool isStrategicLocation = IsStrategicLocation(hex);
        if (isStrategicLocation)
        {
            score += territoryWeight;
        }

        // Value increases if it's a point of contention
        bool isContested = IsContestedLocation(hex);
        if (isContested)
        {
            score += territoryWeight * 0.5f;
        }

        return score;
    }

    private bool IsStrategicLocation(Hex hex)
    {
        // Define what makes a location strategic in your game
        // Examples:
        if (hex.GetPC() != null) return true; // Cities/settlements are strategic
        if (hex.hiddenArtifacts.Count > 0) return true; // Artifacts are strategic
        if (hex.terrainType == TerrainEnum.shore) return true; // Shores are strategic
        return false;
    }

    private bool IsContestedLocation(Hex hex)
    {
        // A location is contested if there are both allies and enemies nearby
        bool hasAllies = false;
        bool hasEnemies = false;

        foreach (var nearbyHex in GetNearbyHexes(hex))
        {
            if (nearbyHex == null) continue;

            // Check for allied presence
            if (nearbyHex.characters.Any(x =>
                x != null && !x.killed &&
                x.GetAlignment() == controlledCharacter.GetAlignment()))
            {
                hasAllies = true;
            }

            // Check for enemy presence
            if (nearbyHex.characters.Any(x =>
                x != null && !x.killed &&
                (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment())))
            {
                hasEnemies = true;
            }

            // If we found both, we can return
            if (hasAllies && hasEnemies) return true;
        }

        return hasAllies && hasEnemies;
    }

    private List<Hex> GetNearbyHexes(Hex center)
    {
        // Get hexes in the vicinity
        // This is a simplified version - you'd need to adapt for your board implementation
        var nearbyHexes = new List<Hex>();
        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex != null && Vector2Int.Distance(hex.v2, center.v2) <= 2)
            {
                nearbyHexes.Add(hex);
            }
        }
        return nearbyHexes;
    }

    private float EvaluateArtifactScore(Hex hex)
    {
        float score = 0;

        // Check for artifacts
        if (hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0)
        {
            score += artifactWeight * hex.hiddenArtifacts.Count;
        }

        return score;
    }

    private float EvaluateSafetyScore(Hex hex)
    {
        float score = 0;

        // Safety increases with distance from enemies
        float minEnemyDistance = float.MaxValue;
        bool enemiesPresent = false;

        foreach (var relevantHex in controlledCharacter.relevantHexes)
        {
            if (relevantHex == null) continue;

            // Check for enemy presence
            bool hasEnemies = relevantHex.characters.Any(x =>
                x != null && !x.killed &&
                (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()));

            if (hasEnemies)
            {
                enemiesPresent = true;
                float distance = Vector2Int.Distance(hex.v2, relevantHex.v2);
                minEnemyDistance = Mathf.Min(minEnemyDistance, distance);
            }
        }

        // If no enemies are present, no need for safety
        if (!enemiesPresent) return 0;

        // Score increases with distance from closest enemy
        // and decreases with proximity to the map edge
        score = Mathf.Min(minEnemyDistance / 5f, 1.0f); // Normalize to 0-1

        // Bonus for being near friendly units (protection)
        foreach (var relevantHex in controlledCharacter.relevantHexes)
        {
            if (relevantHex == null) continue;

            int allyCount = relevantHex.characters.Count(x =>
                x != null && !x.killed &&
                x.GetAlignment() == controlledCharacter.GetAlignment() &&
                x != controlledCharacter);

            if (allyCount > 0 && Vector2Int.Distance(hex.v2, relevantHex.v2) <= 2)
            {
                score += 0.2f * allyCount; // Bonus for each nearby ally
            }
        }

        return score;
    }

    private void VisualizeAgentDecisions()
    {
        // DEBUG: Log available actions count
        foreach (var actionId in availableActionsIds)
        {
            var action = allPossibleActions.Find(a => a.actionId == actionId);
            // Debug.Log($"- [{controlledCharacter.characterName}] Available action: {action?.name} {action?.actionName} (ID: {actionId})");
        }

        // Log objective evaluation
        foreach (var hexScore in hexObjectiveScores.OrderByDescending(x => x.Value).Take(5))
        {
            // Debug.Log($"- [{controlledCharacter.characterName}] Objective: {hexObjectiveTypes[hexScore.Key]} at {hexScore.Key.v2} with score {hexScore.Value}");
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
            int[] discreteActionsArray = new int[4]; // We have 4 branches now

            // Set reasonable defaults for other branches
            discreteActionsArray[OBJECTIVE_TYPE_BRANCH] = (int)currentObjective;
            discreteActionsArray[TARGET_HEX_BRANCH] = 0; // Medium priority
            discreteActionsArray[MOVEMENT_BRANCH] = 0; // Direct path
            discreteActionsArray[ACTION_BRANCH] = allPossibleActions.FindIndex(0, x => x.actionId == action.actionId);

            // Create ActionBuffers with discrete actions
            ActionBuffers buffers = new ActionBuffers(
                new float[] { }, // No continuous actions
                discreteActionsArray
            );

            phase = AgentPhase.ChooseAction;
            // Process the action for training purposes
            OnActionReceived(buffers);
        }
    }


    private void ExecuteChosenAction()
    {
        if (chosenAction == null)
        {
            // Debug.LogError($"- [{controlledCharacter.characterName}] No action chosen to execute!");
            return;
        }

        // Debug.Log($"- [{controlledCharacter.characterName}] Executing action: {chosenAction.actionName}");

        // Get leader state before action
        var leader = controlledCharacter.GetOwner();

        if (leader == null)
        {
            // Debug.LogError($"- [{controlledCharacter.characterName}] Leader is null! Cannot execute action properly.");
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

        // Additional metrics for more detailed rewards
        int territoryControlBefore = CountControlledHexes(leader);
        int resourceProductionBefore = leader.GetResourceProductionPoints();
        float averageCharHealthBefore = GetAverageCharacterHealth(leader);
        int strategicLocationsBefore = CountStrategicLocations(leader);
        int artifactsBefore = CountArtifacts(leader);

        // In AI mode, execute immediately. If it's character, it will be executed separately (in the game interfacE)
        bool applyRewards = isTrainingMode;
        if (!isPlayerControlled || autoplay)
        {
            // If the order fails, that is an issue of skill and randomness, we will not check the consequences and get rewards as nothing happened
            applyRewards &= chosenAction.ExecuteAI();
        }

        if (applyRewards)
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

            // Additional post-action metrics
            int territoryControlAfter = CountControlledHexes(leader);
            int resourceProductionAfter = leader.GetResourceProductionPoints();
            float averageCharHealthAfter = GetAverageCharacterHealth(leader);
            int strategicLocationsAfter = CountStrategicLocations(leader);
            int artifactsAfter = CountArtifacts(leader);

            // Base action reward
            float actionReward = chosenAction.reward / 10f; // Normalize reward
            AddReward(actionReward);

            // STRATEGIC REWARDS
            if (wasBankrupted && !isBankrupted) AddReward(5f / 10f);
            if (wasNegative && !isNegative) AddReward(5f / 10f);
            if (!wasBankrupted && isBankrupted) AddReward(-5f / 10f);
            if (!wasNegative && isNegative) AddReward(-5f / 10f);

            // Core game metrics rewards
            AddReward((storesAfter - storesBefore) / 100f);
            AddReward((friendlyStrengthAfter - friendlyStrengthBefore) / 100f);
            AddReward((enemiesStrengthBefore - enemiesStrengthAfter) / 100f);
            AddReward((charactersPointsAfter - characterPointsBefore) / 100f);
            AddReward((pcsStrengthAfter - pcsStrengthBefore) / 100f);
            AddReward((armiesStrengthAfter - armiesStrengthBefore) / 100f);

            // Additional refined rewards
            AddReward((territoryControlAfter - territoryControlBefore) / 50f);
            AddReward((resourceProductionAfter - resourceProductionBefore) / 50f);
            AddReward((averageCharHealthAfter - averageCharHealthBefore) / 25f);
            AddReward((strategicLocationsAfter - strategicLocationsBefore) / 5f);
            AddReward((artifactsAfter - artifactsBefore) * 2f);

            // Objective-based rewards
            if (currentObjective != HexObjectiveType.None && strategicTargetHex != null)
            {
                bool objectiveCompleted = false;

                switch (currentObjective)
                {
                    case HexObjectiveType.AttackEnemy:
                        // Check if we defeated enemies
                        objectiveCompleted = enemiesStrengthAfter < enemiesStrengthBefore;
                        break;
                    case HexObjectiveType.DefendAlly:
                        // Check if ally condition improved
                        objectiveCompleted = averageCharHealthAfter > averageCharHealthBefore;
                        break;
                    case HexObjectiveType.GatherResource:
                        // Check if resources increased
                        objectiveCompleted = storesAfter > storesBefore || resourceProductionAfter > resourceProductionBefore;
                        break;
                    case HexObjectiveType.SecureTerritory:
                        // Check if territory expanded
                        objectiveCompleted = territoryControlAfter > territoryControlBefore ||
                                           strategicLocationsAfter > strategicLocationsBefore;
                        break;
                    case HexObjectiveType.RetrieveArtifact:
                        // Check if we got artifacts
                        objectiveCompleted = artifactsAfter > artifactsBefore;
                        break;
                    case HexObjectiveType.RetreatToSafety:
                        // Check if we're safer (further from enemies)
                        float currentSafetyScore = EvaluateSafetyScore(controlledCharacter.hex);
                        objectiveCompleted = currentSafetyScore > 0.6f; // Threshold for "safe"
                        break;
                }

                if (objectiveCompleted)
                {
                    AddReward(3.0f); // Strong reward for completing the selected objective
                    // Debug.Log($"- [{controlledCharacter.characterName}] Completed objective: {currentObjective}");
                }
            }

            // Check for game over condition
            if (IsGameOver(leader))
            {
                AddReward(IsWinner(leader) ? 25f : -25f);
                EndEpisode();
            }
        }
    }

    // Helper methods for refined reward calculation
    private int CountControlledHexes(Leader leader)
    {
        // Count hexes controlled by this leader
        // This is a simplified placeholder - implement according to your game logic
        int count = 0;
        if (board != null)
        {
            // Example: Count hexes with units belonging to this leader
            foreach (var hex in board.GetHexes())
            {
                if (hex.characters.Any(c => c != null && !c.killed && c.GetOwner() == leader))
                {
                    count++;
                }
            }
        }
        return count;
    }


    private float GetAverageCharacterHealth(Leader leader)
    {
        // Calculate average health of all characters
        var characters = leader.controlledCharacters;
        if (characters == null || characters.Count == 0) return 0;

        float totalHealth = 0;
        int count = 0;

        foreach (var character in characters)
        {
            if (character != null && !character.killed)
            {
                totalHealth += character.health;
                count++;
            }
        }

        return count > 0 ? totalHealth / count : 0;
    }

    private int CountStrategicLocations(Leader leader)
    {
        // Count strategic locations controlled by this leader
        int count = 0;

        // Count PCs owned by this leader
        count += leader.controlledPcs.Count(pc => pc != null);

        // Add other strategic locations as defined by your game
        return count;
    }

    private int CountArtifacts(Leader leader)
    {
        // Count artifacts owned by this leader
        return leader.controlledCharacters.Sum(c => c != null ? c.artifacts.Count : 0);
    }

    private void AddDummyActionObservations(VectorSensor sensor)
    {
        // Add dummy character state observations (expanded for new features)
        // Leader info
        sensor.AddObservation(-1f); // Leader index
        sensor.AddObservation(-1f); // Is NPC
        sensor.AddObservation(-1f); // Is joined NPC

        // Character stats
        sensor.AddObservation(1f);  // Health
        sensor.AddObservation(0f);  // Is free agent

        // Alignment (one-hot)
        sensor.AddOneHotObservation((int)controlledCharacter.GetAlignment(), alignmentTypeCount);

        // Leader resources
        sensor.AddObservation(0f);  // Gold
        sensor.AddObservation(0f);  // Leather
        sensor.AddObservation(0f);  // Timber
        sensor.AddObservation(0f);  // Mounts
        sensor.AddObservation(0f);  // Iron
        sensor.AddObservation(0f);  // Mithril

        // Leader stats
        sensor.AddObservation(0f);  // Character count
        sensor.AddObservation(0f);  // PC count

        // Game state
        sensor.AddObservation(0f);  // Current turn

        // New feature observations
        sensor.AddObservation(0f);  // Territory control
        sensor.AddObservation(0f);  // Resource production
        sensor.AddObservation(0f);  // Average character health
        sensor.AddObservation(0f);  // Strategic locations
        sensor.AddObservation(0f);  // Artifacts

        // Current objective
        sensor.AddOneHotObservation(0, OBJECTIVE_TYPE_COUNT);

        // FEATURE-BASED HEX OBSERVATIONS
        // For each relevant hex (we'll use 10 as a placeholder)
        for (int i = 0; i < 10; i++)
        {
            // Distance from current position
            sensor.AddObservation(-1f);

            // Objective scores
            sensor.AddObservation(0f); // Attack score
            sensor.AddObservation(0f); // Defense score
            sensor.AddObservation(0f); // Resource score
            sensor.AddObservation(0f); // Territory score
            sensor.AddObservation(0f); // Artifact score
            sensor.AddObservation(0f); // Safety score

            // Path cost
            sensor.AddObservation(1f);

            // Feature-based presence indicators
            sensor.AddObservation(0f); // Enemy threat level
            sensor.AddObservation(0f); // Ally support level
            sensor.AddObservation(0f); // Resource value
            sensor.AddObservation(0f); // Strategic value
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!awaken) Awaken();

        if (!hasEpisodeBegun)
        {
            AddDummyActionObservations(sensor);
            return;
        }

        // Feature-based representation of the game state
        var owner = controlledCharacter.GetOwner();

        // LEADER AND CHARACTER INFO (15 observations)
        sensor.AddObservation(owner != null ? gameState.GetIndexOfLeader(owner) / (float)gameState.GetMaxLeaders() : -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader ? 1f : -1f);
        sensor.AddObservation(owner != null && owner is NonPlayableLeader && (owner as NonPlayableLeader).joined ? 1f : -1f);
        sensor.AddObservation(controlledCharacter.health / 100f);
        sensor.AddObservation(owner != null || controlledCharacter.killed ? 0f : 1f);
        sensor.AddOneHotObservation((int)controlledCharacter.GetAlignment(), alignmentTypeCount);

        // RESOURCES (6 observations)
        sensor.AddObservation(owner != null ? owner.goldAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.leatherAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.timberAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.mountsAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.ironAmount / 10000f : 0f);
        sensor.AddObservation(owner != null ? owner.mithrilAmount / 10000f : 0f);

        // STATUS METRICS (2 observations)
        sensor.AddObservation(owner != null ? owner.controlledCharacters.Count / (float)gameState.GetMaxCharacters() : 0f);
        sensor.AddObservation(owner != null ? owner.controlledPcs.Count / (float)gameState.GetMaxCharacters() : 0f);

        // GAME PROGRESS (1 observation)
        sensor.AddObservation(gameState.GetTurn() / (float)gameState.GetMaxTurns());

        // NEW STRATEGIC METRICS (5 observations)
        sensor.AddObservation(owner != null ? CountControlledHexes(owner) / 100f : 0f);
        sensor.AddObservation(owner != null ? owner.GetResourceProductionPoints() / 500f : 0f);
        sensor.AddObservation(owner != null ? GetAverageCharacterHealth(owner) / 100f : 0f);
        sensor.AddObservation(owner != null ? CountStrategicLocations(owner) / 20f : 0f);
        sensor.AddObservation(owner != null ? CountArtifacts(owner) / 10f : 0f);

        // CURRENT OBJECTIVE (6 observations - one-hot)
        sensor.AddOneHotObservation((int)currentObjective, OBJECTIVE_TYPE_COUNT);

        // FEATURE-BASED HEX OBSERVATIONS
        // Sort hexes by their objective scores for more consistent representation
        var sortedHexes = controlledCharacter.relevantHexes
            .Where(h => h != null)
            .OrderByDescending(h => hexObjectiveScores.ContainsKey(h) ? hexObjectiveScores[h] : 0)
            .Take(10) // Limit to top 10 most promising hexes
            .ToList();

        // Ensure we always have 10 hex observations (padding with nulls if needed)
        while (sortedHexes.Count < 10)
        {
            sortedHexes.Add(null);
        }

        foreach (var hex in sortedHexes)
        {
            if (hex == null)
            {
                // Dummy values for null hexes
                sensor.AddObservation(-1f); // Distance
                sensor.AddObservation(0f);  // Attack score
                sensor.AddObservation(0f);  // Defense score
                sensor.AddObservation(0f);  // Resource score
                sensor.AddObservation(0f);  // Territory score
                sensor.AddObservation(0f);  // Artifact score
                sensor.AddObservation(0f);  // Safety score
                sensor.AddObservation(1f);  // Path cost
                sensor.AddObservation(0f);  // Enemy threat
                sensor.AddObservation(0f);  // Ally support
                sensor.AddObservation(0f);  // Resource value
                sensor.AddObservation(0f);  // Strategic value
                continue;
            }

            // Distance from current position (normalized)
            float distance = Vector2Int.Distance(controlledCharacter.hex.v2, hex.v2);
            sensor.AddObservation(distance / 20f); // Normalize by assumed max distance

            // Individual objective scores for this hex (normalized)
            sensor.AddObservation(EvaluateAttackScore(hex));
            sensor.AddObservation(EvaluateDefenseScore(hex));
            sensor.AddObservation(EvaluateResourceScore(hex));
            sensor.AddObservation(EvaluateTerritoryScore(hex));
            sensor.AddObservation(EvaluateArtifactScore(hex));
            sensor.AddObservation(EvaluateSafetyScore(hex));

            // Path cost (normalized)
            float pathCost = hexPathRenderer.GetPathCost(controlledCharacter.hex.v2, hex.v2, controlledCharacter);
            sensor.AddObservation(pathCost / gameState.GetMaxMovement());

            // Feature-based presence indicators
            float enemyThreat = CalculateEnemyThreat(hex);
            float allySupport = CalculateAllySupport(hex);
            float resourceValue = CalculateResourceValue(hex);
            float strategicValue = IsStrategicLocation(hex) ? 1f : 0f;

            sensor.AddObservation(enemyThreat);
            sensor.AddObservation(allySupport);
            sensor.AddObservation(resourceValue);
            sensor.AddObservation(strategicValue);
        }
    }

    // Helper methods for feature-based observations
    private float CalculateEnemyThreat(Hex hex)
    {
        float threat = 0;

        // Count enemy characters and their strength
        foreach (var enemy in hex.characters.Where(c =>
            c != null && !c.killed &&
            (c.GetAlignment() == AlignmentEnum.neutral || c.GetAlignment() != controlledCharacter.GetAlignment())))
        {
            threat += enemy.health / 100f;
        }

        // Count enemy armies
        foreach (var army in hex.armies.Where(a =>
            a != null && !a.killed && a.commander != null && !a.commander.killed &&
            (a.GetAlignment() == AlignmentEnum.neutral || a.GetAlignment() != controlledCharacter.GetAlignment())))
        {
            threat += 0.5f;
        }

        // Enemy PC threat
        var pc = hex.GetPC();
        if (pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral ||
                        pc.owner.GetAlignment() != controlledCharacter.GetAlignment()))
        {
            threat += pc.GetDefense() / 10000f;
        }

        return Mathf.Min(threat, 1f); // Normalize to 0-1
    }

    private float CalculateAllySupport(Hex hex)
    {
        float support = 0;

        // Count allied characters and their strength
        foreach (var ally in hex.characters.Where(c =>
            c != null && !c.killed &&
            c.GetAlignment() == controlledCharacter.GetAlignment() &&
            c != controlledCharacter))
        {
            support += ally.health / 100f;
        }

        // Count allied armies
        foreach (var army in hex.armies.Where(a =>
            a != null && !a.killed && a.commander != null && !a.commander.killed &&
            a.GetAlignment() == controlledCharacter.GetAlignment()))
        {
            support += 0.5f;
        }

        // Allied PC support
        var pc = hex.GetPC();
        if (pc != null && pc.owner.GetAlignment() == controlledCharacter.GetAlignment())
        {
            support += pc.GetDefense() / 10000f;
        }

        return Mathf.Min(support, 1f); // Normalize to 0-1
    }

    private float CalculateResourceValue(Hex hex)
    {
        // Calculate resource value based on terrain
        switch (hex.terrainType)
        {
            case TerrainEnum.forest:
                return 0.8f;
            case TerrainEnum.mountains:
                return 0.9f;
            case TerrainEnum.hills:
                return 0.5f;
            default:
                return 0.2f;
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (!awaken) Awaken();
        if (!hasEpisodeBegun) return;
        if (hexObjectiveScores.Count < 1 || hexObjectiveTypes.Count < 1)
        {
            // Reset objective evaluation for the new turn
            EvaluateHexObjectives();
        }

        switch (phase)
        {
            case AgentPhase.EvaluateObjectives:
                // Step 1: Determine what type of objective to pursue
                int objectiveTypeIndex = actionBuffers.DiscreteActions[OBJECTIVE_TYPE_BRANCH];
                currentObjective = (HexObjectiveType)(objectiveTypeIndex + 1); // +1 because enums start at 1, None = 0

                // Step 2: Find hexes that match this objective type
                var candidateHexes = controlledCharacter.relevantHexes
                    .Where(h => h != null && hexObjectiveTypes.ContainsKey(h) && hexObjectiveTypes[h] == currentObjective)
                    .OrderByDescending(h => hexObjectiveScores[h])
                    .ToList();

                // If no hexes match our objective, look for any scored hexes
                if (candidateHexes.Count == 0)
                {
                    candidateHexes = controlledCharacter.relevantHexes
                        .Where(h => h != null && hexObjectiveScores.ContainsKey(h))
                        .OrderByDescending(h => hexObjectiveScores[h])
                        .ToList();
                }

                // Step 3: Select a target based on priority level
                int priorityLevel = actionBuffers.DiscreteActions[TARGET_HEX_BRANCH];
                int targetIndex = 0;

                // Priority level determines which part of the sorted list to choose from
                switch (priorityLevel)
                {
                    case 0: // High priority - top of the list
                        targetIndex = 0;
                        break;
                    case 1: // Medium priority - middle of the list
                        targetIndex = candidateHexes.Count > 2 ? candidateHexes.Count / 2 : 0;
                        break;
                    case 2: // Low priority - lower part of the list
                        targetIndex = candidateHexes.Count > 3 ? candidateHexes.Count * 2 / 3 : 0;
                        break;
                }

                // Make sure we have a valid index
                targetIndex = Mathf.Clamp(targetIndex, 0, candidateHexes.Count - 1);

                // Set our strategic target
                if (candidateHexes.Count > 0)
                {
                    strategicTargetHex = candidateHexes[targetIndex];
                    lastDistanceToTarget = Vector2Int.Distance(controlledCharacter.hex.v2, strategicTargetHex.v2);
                    // Debug.Log($"- [{controlledCharacter.characterName}] Objective: {currentObjective}, Target: {strategicTargetHex.v2}");
                }
                else if (controlledCharacter.relevantHexes.Count > 0)
                {
                    // Default to first relevant hex if no scored hexes
                    strategicTargetHex = controlledCharacter.relevantHexes[0];
                    lastDistanceToTarget = Vector2Int.Distance(controlledCharacter.hex.v2, strategicTargetHex.v2);
                    // Debug.Log($"- [{controlledCharacter.characterName}] No scored hexes, using default target: {strategicTargetHex.v2}");
                }
                else
                {
                    // Debug.Log($"- [{controlledCharacter.characterName}] No relevant hexes available!");
                }

                phase = AgentPhase.MoveTowardsObjective;
                RequestDecision();
                break;

            case AgentPhase.MoveTowardsObjective:
                // Step 4: Determine how to move toward the target
                int movementStrategy = actionBuffers.DiscreteActions[MOVEMENT_BRANCH];

                if (strategicTargetHex == null || controlledCharacter.reachableHexes.Count == 0)
                {
                    // Debug.Log($"- [{controlledCharacter.characterName}] No target or no reachable hexes, skipping movement");
                    phase = AgentPhase.ChooseAction;
                    RequestDecision();
                    break;
                }

                // Find the best hex to move to based on strategy
                Hex destinationHex = null;

                switch (movementStrategy)
                {
                    case 0: // Direct path - minimize distance to target
                        destinationHex = FindDirectPathHex();
                        break;
                    case 1: // Cautious path - balance safety and progress
                        destinationHex = FindCautiousPathHex();
                        break;
                    case 2: // Aggressive path - focus on enemy engagement
                        destinationHex = FindAggressivePathHex();
                        break;
                }

                if (destinationHex != null)
                {
                    float previousDistance = Vector2Int.Distance(controlledCharacter.hex.v2, strategicTargetHex.v2);
                    if(!isPlayerControlled || autoplay)
                    {
                        board.MoveCharacterOneHex(controlledCharacter, controlledCharacter.hex, destinationHex, true);
                    }   
                    float newDistance = Vector2Int.Distance(destinationHex.v2, strategicTargetHex.v2);

                    // Debug.Log($"- [{controlledCharacter.characterName}] Moved to {destinationHex.v2} " +
                    //          $"(Strategy: {(MovementStrategy)movementStrategy})");

                    if (newDistance < previousDistance)
                    {
                        AddReward(0.05f); // Progress reward
                    }

                    lastDistanceToTarget = newDistance;
                }
                else
                {
                    // Debug.Log($"- [{controlledCharacter.characterName}] Could not find a valid movement destination");
                }

                phase = AgentPhase.ChooseAction;
                RequestDecision();
                break;

            case AgentPhase.ChooseAction:
                // Step 5: Choose an action to perform
                int selectedActionIndex = actionBuffers.DiscreteActions[ACTION_BRANCH];
                
                if (selectedActionIndex < allPossibleActions.Count)
                {
                    chosenAction = allPossibleActions[selectedActionIndex];

                    // Check if this action is available
                    if (!availableActionsIds.Contains(chosenAction.actionId))
                    {
                        // Debug.LogWarning($"- [{controlledCharacter.characterName}] Action {chosenAction.actionName} " +
                        //               $"not available, falling back to default");
                        chosenAction = DEFAULT_ACTION;
                    }
                }
                else
                {
                    // Debug.LogError($"- [{controlledCharacter.characterName}] Invalid action index {selectedActionIndex}");
                    chosenAction = DEFAULT_ACTION;
                }

                // Debug.Log($"- [{controlledCharacter.characterName}] Choosing action: {chosenAction.actionName}");

                if (!isPlayerControlled || autoplay) ExecuteChosenAction();

                phase = AgentPhase.EvaluateObjectives;
                break;
        }
    }

    // Movement strategy enums for better readability
    private enum MovementStrategy
    {
        Direct = 0,
        Cautious = 1,
        Aggressive = 2
    }

    // Helper methods for different movement strategies
    private Hex FindDirectPathHex()
    {
        // Find the hex that minimizes distance to target
        Hex bestHex = null;
        float bestCost = float.MaxValue;

        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;

            float costToTarget = hexPathRenderer.GetPathCost(
                hex.v2,
                strategicTargetHex.v2,
                controlledCharacter
            );

            if (costToTarget < bestCost)
            {
                bestCost = costToTarget;
                bestHex = hex;
            }
        }

        return bestHex;
    }

    private Hex FindCautiousPathHex()
    {
        // Balance progress and safety
        Hex bestHex = null;
        float bestScore = float.MinValue;

        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;

            // Get cost to target (lower is better)
            float costToTarget = hexPathRenderer.GetPathCost(
                hex.v2,
                strategicTargetHex.v2,
                controlledCharacter
            );

            // Get safety score (higher is better)
            float safetyScore = EvaluateSafetyScore(hex);

            // Combined score: negative cost (so lower costs are better) plus safety
            float score = -costToTarget / gameState.GetMaxMovement() + safetyScore * 2;

            if (score > bestScore)
            {
                bestScore = score;
                bestHex = hex;
            }
        }

        return bestHex;
    }

    private Hex FindAggressivePathHex()
    {
        // Prioritize hexes that engage enemies while making progress
        Hex bestHex = null;
        float bestScore = float.MinValue;

        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;

            // Get cost to target (lower is better)
            float costToTarget = hexPathRenderer.GetPathCost(
                hex.v2,
                strategicTargetHex.v2,
                controlledCharacter
            );

            // Get enemy engagement score (higher is better)
            float attackScore = EvaluateAttackScore(hex);

            // Combined score: negative cost plus attack value
            float score = -costToTarget / gameState.GetMaxMovement() + attackScore * 3;

            if (score > bestScore)
            {
                bestScore = score;
                bestHex = hex;
            }
        }

        return bestHex;
    }

    // Ensure the action mask is correctly set up
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!awaken) Awaken();
        if (!hasEpisodeBegun) return;

        // Get the behavior parameters to validate action space
        var behaviorParams = GetComponent<BehaviorParameters>();
        if (behaviorParams == null)
        {
            // Debug.LogError($"- [{controlledCharacter.characterName}] Behavior Parameters missing!");
            return;
        }

        // Clear everything first (enable all actions by default)
        for (int branch = 0; branch < behaviorParams.BrainParameters.ActionSpec.BranchSizes.Length; branch++)
        {
            for (int i = 0; i < behaviorParams.BrainParameters.ActionSpec.BranchSizes[branch]; i++)
            {
                actionMask.SetActionEnabled(branch, i, true);
            }
        }

        switch (phase)
        {
            case AgentPhase.EvaluateObjectives:
                EvaluateHexObjectives();
                break;

            case AgentPhase.MoveTowardsObjective:
                break;

            case AgentPhase.ChooseAction:

                // Initialize all possible actions
                allPossibleActions.ForEach(x => x.Initialize(controlledCharacter));
                availableActionsIds = GetAvailableActionIds(controlledCharacter);
                if(availableActionsIds.Count < 1)
                {
                    // Debug.Log($"- [{controlledCharacter.characterName}] No available actions! Why? Is it killed? {controlledCharacter.killed}");
                    availableActionsIds.Add(DEFAULT_ACTION.actionId);
                }
                // Only enable actions that are actually available
                for (int actionIndex = 0; actionIndex < allPossibleActions.Count; actionIndex++)
                {
                    int actionId = allPossibleActions[actionIndex].actionId;
                    bool isAvailable = availableActionsIds.Contains(actionId);
                    actionMask.SetActionEnabled(ACTION_BRANCH, actionIndex, isAvailable);
                }

                break;
        }
    }

    private List<int> GetAvailableActionIds(Character character)
    {
        if (controlledCharacter == null)
        {
            // Debug.LogError("Controlled character is null!");
            return new List<int>();
        }

        if (allPossibleActions == null)
        {
            // Debug.LogError($"- [{controlledCharacter.characterName}] Possible actions are null!");
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