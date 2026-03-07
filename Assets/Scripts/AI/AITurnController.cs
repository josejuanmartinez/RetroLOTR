using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class AITurnController
{
    public static IEnumerator ExecuteLeaderTurn(PlayableLeader leader)
    {
        if (leader == null) yield break;

        ActionsManager actionsManager = UnityEngine.Object.FindFirstObjectByType<ActionsManager>();
        if (actionsManager == null)
        {
            Debug.LogWarning("AI could not find ActionsManager. Skipping AI turn.");
            yield break;
        }

        Task economyCardsTask = ConsumeAiResourceCardsAsync(leader, actionsManager);
        while (!economyCardsTask.IsCompleted) yield return null;
        if (economyCardsTask.IsFaulted && economyCardsTask.Exception != null)
        {
            Debug.LogException(economyCardsTask.Exception);
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

    private static async Task ConsumeAiResourceCardsAsync(PlayableLeader leader, ActionsManager actionsManager)
    {
        if (leader == null || actionsManager == null) return;

        DeckManager deckManager = DeckManager.Instance != null
            ? DeckManager.Instance
            : UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        if (deckManager == null || !deckManager.HasDeckFor(leader)) return;

        Character actor = leader.controlledCharacters.FirstOrDefault(c => c != null && !c.killed);
        if (actor == null) return;

        List<CardData> resourceCards = deckManager.GetHand(leader)
            .Where(card => card != null && (card.GetCardType() == CardTypeEnum.Land || card.GetCardType() == CardTypeEnum.PC))
            .ToList();

        foreach (CardData card in resourceCards)
        {
            if (card == null) continue;
            if (!card.EvaluatePlayability(actor)) continue;
            if (!deckManager.TryConsumeCard(leader, card.cardId, drawReplacement: false, out CardData consumedCard)) continue;
            bool succeeded = await ExecuteCardEffectForAiAsync(consumedCard, actor, actionsManager);
            if (succeeded)
            {
                deckManager.ApplyMapRevealForPlayedCard(leader, consumedCard);
                leader.RecordPlayedCard(consumedCard);
            }
        }
    }

    private static async Task<bool> ExecuteCardEffectForAiAsync(CardData card, Character actor, ActionsManager actionsManager)
    {
        if (card == null || actor == null || actionsManager == null) return false;

        string actionRef = NormalizeActionRef(card.GetActionRef());
        if (string.IsNullOrWhiteSpace(actionRef)) return false;

        CharacterAction action = ResolveActionByRef(actionRef, actionsManager);
        if (action == null) return false;

        action.Initialize(actor);
        action.difficulty = Mathf.Max(0, card.difficulty);

        bool ok = true;
        if (action.effect != null) ok = action.effect(actor);
        if (ok && action.asyncEffect != null) ok = await action.asyncEffect(actor);
        return ok;
    }

    private static CharacterAction ResolveActionByRef(string actionRef, ActionsManager actionsManager = null)
    {
        string normalizedActionRef = NormalizeActionRef(actionRef);
        if (string.IsNullOrWhiteSpace(normalizedActionRef)) return null;

        if (actionsManager == null)
        {
            actionsManager = UnityEngine.Object.FindFirstObjectByType<ActionsManager>();
        }

        if (actionsManager != null && actionsManager.characterActions != null && actionsManager.characterActions.Length > 0)
        {
            CharacterAction fromManager = actionsManager.characterActions.FirstOrDefault(candidate =>
                candidate != null && ActionTypeMatchesRef(candidate.GetType(), normalizedActionRef));
            if (fromManager != null) return fromManager;
        }

        CharacterAction[] allActions = UnityEngine.Object.FindObjectsByType<CharacterAction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allActions != null && allActions.Length > 0)
        {
            CharacterAction existing = allActions.FirstOrDefault(candidate =>
                candidate != null && ActionTypeMatchesRef(candidate.GetType(), normalizedActionRef));
            if (existing != null) return existing;
        }

        Type resolvedType = ResolveActionType(normalizedActionRef);
        if (resolvedType == null || !typeof(CharacterAction).IsAssignableFrom(resolvedType)) return null;

        GameObject host = actionsManager != null ? actionsManager.gameObject : null;
        if (host == null) return null;

        CharacterAction created = host.GetComponent(resolvedType) as CharacterAction;
        if (created == null)
        {
            created = host.AddComponent(resolvedType) as CharacterAction;
        }

        if (created != null && actionsManager != null)
        {
            CharacterAction[] existingArray = actionsManager.characterActions ?? new CharacterAction[0];
            if (!existingArray.Contains(created))
            {
                actionsManager.characterActions = existingArray.Concat(new[] { created }).ToArray();
            }
        }

        return created;
    }

    private static string NormalizeActionRef(string actionRef)
    {
        if (string.IsNullOrWhiteSpace(actionRef)) return string.Empty;

        string normalized = actionRef.Trim();
        if (normalized.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 3).Trim();
        }

        int lastDotIndex = normalized.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < normalized.Length - 1)
        {
            normalized = normalized.Substring(lastDotIndex + 1).Trim();
        }

        return normalized;
    }

    private static bool ActionTypeMatchesRef(System.Type candidateType, string normalizedActionRef)
    {
        if (candidateType == null || string.IsNullOrWhiteSpace(normalizedActionRef)) return false;

        if (string.Equals(candidateType.Name, normalizedActionRef, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(candidateType.FullName)
            && string.Equals(candidateType.FullName, normalizedActionRef, System.StringComparison.OrdinalIgnoreCase);
    }

    private static Type ResolveActionType(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;

        Type direct = Type.GetType(className, false, true);
        if (direct != null) return direct;

        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            Type candidate = assembly.GetType(className, false, true);
            if (candidate != null) return candidate;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            candidate = types.FirstOrDefault(t =>
                string.Equals(t.Name, className, System.StringComparison.OrdinalIgnoreCase));
            if (candidate != null) return candidate;
        }

        return null;
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

        bool shouldLog = context.LastChosenAction == null || context.LastChosenAction.LastExecutionSucceeded;
        if (shouldLog)
        {
            AIActionLogger.Log(context.BuildLogEntry());
        }
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
            if (!action.IsRoleEligible(character)) continue;
            bool isEnabled = action.FulfillsConditions();
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

        Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
        if (board == null) return;

        HexPathRenderer pathRenderer = UnityEngine.Object.FindFirstObjectByType<HexPathRenderer>();
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
