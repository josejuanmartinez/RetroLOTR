using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class AITurnController
{
    public static IEnumerator ExecuteLeaderTurn(PlayableLeader leader)
    {
        if (leader == null) yield break;

        ActionsManager actionsManager = Object.FindFirstObjectByType<ActionsManager>();
        if (actionsManager == null)
        {
            Debug.LogWarning("AI could not find ActionsManager. Skipping AI turn.");
            yield break;
        }

        foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed))
        {
            Task task = ExecuteCharacterAsync(leader, character, actionsManager);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted && task.Exception != null)
            {
                Debug.LogException(task.Exception);
            }
        }

        actionsManager.Hide();
    }

    private static async Task ExecuteCharacterAsync(PlayableLeader leader, Character character, ActionsManager actionsManager)
    {
        List<CharacterAction> availableActions = GetAvailableActions(character, actionsManager);
        AIContext.AIContextPrecomputedData? precomputed = AIContextCacheManager.Instance != null ? AIContextCacheManager.Instance.GetCached(leader, character) : null;
        AIContext context = new AIContext(leader, character, availableActions, precomputed);
        IBehaviourNode behaviour = AIBehaviourTreeBuilder.BuildDefault();

        BehaviourTreeStatus status = await behaviour.Tick(context);
        if (status == BehaviourTreeStatus.Failure)
        {
            await context.PassAsync();
        }

        AIActionLogger.Log(context.BuildLogEntry());
        await MoveTowardsTargetAsync(context);

        actionsManager.Hide();
    }

    private static List<CharacterAction> GetAvailableActions(Character character, ActionsManager actionsManager)
    {
        List<CharacterAction> available = new();
        if (character == null || actionsManager == null) return available;

        if (actionsManager.characterActions == null || actionsManager.characterActions.Length == 0)
        {
            actionsManager.characterActions = actionsManager.GetComponentsInChildren<CharacterAction>();
        }

        actionsManager.Refresh(character);

        foreach (CharacterAction action in actionsManager.characterActions)
        {
            if (action == null) continue;
            if (action.button == null) continue;

            bool isEnabled = action.button.gameObject.activeSelf && action.FulfillsConditions();
            if (isEnabled) available.Add(action);
        }

        return available;
    }

    private static async Task MoveTowardsTargetAsync(AIContext context)
    {
        if (context == null || context.Character == null) return;
        Character character = context.Character;
        if (character.moved >= character.GetMaxMovement()) return;

        Hex target = context.GetPreferredMovementTarget();
        if (target == null || target == character.hex) return;

        Board board = Object.FindFirstObjectByType<Board>();
        if (board == null) return;

        HexPathRenderer pathRenderer = Object.FindFirstObjectByType<HexPathRenderer>();
        if (pathRenderer != null)
        {
            pathRenderer.DrawPathBetweenHexes(character.hex.v2, target.v2, character);
        }

        board.Move(character, target.v2);

        // Wait until movement finishes
        int safety = 0;
        while (board.moving && safety < 200)
        {
            await Task.Delay(50);
            safety++;
        }
    }
}
