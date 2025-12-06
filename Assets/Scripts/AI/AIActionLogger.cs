using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class AIActionLogEntry
{
    public string timestamp;
    public int turn;
    public string leaderName;
    public string leaderAlignment;
    public string characterName;
    public string characterAlignment;
    public bool armyCommander;
    public int commander;
    public int agent;
    public int emmissary;
    public int mage;
    public int armyOffence;
    public int armyDefence;
    public int health;
    public int preCommander;
    public int preAgent;
    public int preEmmissary;
    public int preMage;
    public int preArmyOffence;
    public int preArmyDefence;
    public int preHealth;
    public int commanderDelta;
    public int agentDelta;
    public int emmissaryDelta;
    public int mageDelta;
    public int armyOffenceDelta;
    public int armyDefenceDelta;
    public int healthDelta;
    public int goldBuffer;
    public int goldPerTurn;
    public int leather;
    public int timber;
    public int iron;
    public int mounts;
    public int mithril;
    public int leatherPerTurn;
    public int timberPerTurn;
    public int ironPerTurn;
    public int mountsPerTurn;
    public int mithrilPerTurn;
    public int preGoldBuffer;
    public int preGoldPerTurn;
    public int preLeather;
    public int preTimber;
    public int preIron;
    public int preMounts;
    public int preMithril;
    public int preLeatherPerTurn;
    public int preTimberPerTurn;
    public int preIronPerTurn;
    public int preMountsPerTurn;
    public int preMithrilPerTurn;
    public int goldDelta;
    public int leatherDelta;
    public int timberDelta;
    public int ironDelta;
    public int mountsDelta;
    public int mithrilDelta;
    public int goldPerTurnDelta;
    public int leatherPerTurnDelta;
    public int timberPerTurnDelta;
    public int ironPerTurnDelta;
    public int mountsPerTurnDelta;
    public int mithrilPerTurnDelta;
    public string economyStatus;
    public bool needsIndirect;
    public float nationArtifactsShare;
    public float nearestNpcDistance;
    public float nearestEnemyCharacterDistance;
    public float nearestEnemyStrength;
    public float nearestNonNeutralStrength;
    public float preNearestEnemyStrength;
    public float preNearestNonNeutralStrength;
    public float nearestEnemyStrengthDelta;
    public float nearestNonNeutralStrengthDelta;
    public string targetOwnerName;
    public string targetOwnerAlignment;
    public string targetOwnerType;
    public string preferredTargetType;
    public Vector2Int preferredTarget;
    public float preferredTargetDistance;
    public string actionName;
    public string advisorType;
    public int actionDifficulty;
    public int actionGoldCost;
    public List<string> scoredActions;
    public List<string> artifactTransferCandidates;
}

public static class AIActionLogger
{
    private static readonly string LogFilePath = Path.Combine(Application.persistentDataPath, "ai_actions.jsonl");

    public static void Log(AIActionLogEntry entry)
    {
        try
        {
            string json = JsonUtility.ToJson(entry);
            File.AppendAllText(LogFilePath, json + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AIActionLogger failed to write log: {e.Message}");
        }
    }
}
