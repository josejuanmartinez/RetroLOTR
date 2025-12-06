using System;
using System.IO;
using UnityEngine;

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
    public int goldBuffer;
    public int goldPerTurn;
    public string economyStatus;
    public bool needsIndirect;
    public float nationArtifactsShare;
    public float nearestNpcDistance;
    public float nearestEnemyCharacterDistance;
    public float nearestEnemyStrength;
    public float nearestNonNeutralStrength;
    public string preferredTargetType;
    public Vector2Int preferredTarget;
    public string actionName;
    public string advisorType;
    public int actionDifficulty;
    public int actionGoldCost;
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
