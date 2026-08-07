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

        AIBlackboard blackboard = AIBlackboardStore.GetOrCreate(leader);
        (string biasedAdvisorName, string activeHtnTaskId) = AdvanceHtnStrategy(leader, blackboard);

        Task economyCardsTask = ConsumeAiResourceCardsAsync(leader, actionsManager);
        while (!economyCardsTask.IsCompleted) yield return null;
        if (economyCardsTask.IsFaulted && economyCardsTask.Exception != null)
        {
            Debug.LogException(economyCardsTask.Exception);
        }

        foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed))
        {
            Task task = ExecuteCharacterAsync(leader, character, actionsManager, biasedAdvisorName, activeHtnTaskId);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted && task.Exception != null)
            {
                Debug.LogException(task.Exception);
            }
        }

        actionsManager.Hide();
    }

    // Once per leader-turn: re-evaluate the persistent HTN strategy (priority-interrupt
    // scan, failure check, completion-driven advancement — see HTNPlanner) and return the
    // advisor name + taskId its currently-active primitive task biases scoring toward
    // (null for a neutral, no-bias fallback task).
    private static (string advisorName, string taskId) AdvanceHtnStrategy(PlayableLeader leader, AIBlackboard blackboard)
    {
        Character sensingAnchor = leader.controlledCharacters.FirstOrDefault(c => c != null && !c.killed);
        if (sensingAnchor == null) return (null, null);

        AIContext.AIContextPrecomputedData? precomputed = AIContextCacheManager.Instance != null
            ? AIContextCacheManager.Instance.GetCached(leader, sensingAnchor)
            : null;
        AIContext sensingContext = new(leader, sensingAnchor, new List<CharacterAction>(), null, precomputed);

        HTNCompoundTask strategyRoot = AIStrategyLibrary.GetStrategyFor(leader);
        List<HTNStackFrame> previousStack = blackboard.ActiveStack;
        List<HTNStackFrame> newStack = HTNPlanner.AdvanceStack(previousStack, strategyRoot, sensingContext, blackboard);

        bool changed = !StacksEqual(previousStack, newStack);
        blackboard.ActiveStack = newStack;
        blackboard.TurnsOnCurrentTask = changed ? 0 : blackboard.TurnsOnCurrentTask + 1;

        HTNPrimitiveTask activePrimitive = HTNPlanner.ResolveActivePrimitive(newStack, strategyRoot);
        string stackDescription = string.Join(">", newStack.Select(f => $"{f.MethodTaskId}[{f.SubtaskIndex}]"));
        Debug.Log($"[HTN] {leader.characterName} difficulty={AIDifficultySettings.CurrentDifficulty} turnsOnTask={blackboard.TurnsOnCurrentTask} stack={stackDescription} advisor={activePrimitive?.AdvisorName}");

        return (activePrimitive?.AdvisorName, activePrimitive?.TaskId);
    }

    private static bool StacksEqual(List<HTNStackFrame> a, List<HTNStackFrame> b)
    {
        if (a == null || b == null) return a == b;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].MethodTaskId != b[i].MethodTaskId || a[i].SubtaskIndex != b[i].SubtaskIndex) return false;
        }
        return true;
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
            if (!deckManager.TryConsumeCard(leader, card.name, drawReplacement: false, out CardData consumedCard)) continue;
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

        action.Initialize(actor, card, condition: null, effect: null, asyncEffect: null);
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

        return actionsManager != null ? actionsManager.ResolveActionByRef(normalizedActionRef) : null;
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

    // Plays up to AIDifficulty cards for this character this turn, each the single best-scoring
    // card across the leader's entire deck (never just the hand — see DeckManager.GetFullDeck).
    // Re-scores from scratch each pick since executing a card changes affordability/available
    // cards for the next one. Stops early on an empty candidate pool or a failed execution;
    // falls back to a single Pass if literally nothing was played.
    private static async Task ExecuteCharacterAsync(PlayableLeader leader, Character character, ActionsManager actionsManager, string biasedAdvisorName, string activeHtnTaskId)
    {
        DeckManager deckManager = DeckManager.Instance != null
            ? DeckManager.Instance
            : UnityEngine.Object.FindFirstObjectByType<DeckManager>();
        AIContext.AIContextPrecomputedData? precomputed = AIContextCacheManager.Instance != null
            ? AIContextCacheManager.Instance.GetCached(leader, character)
            : null;
        float advisorBiasBonus = AIAdvisorConfig.GetWeight(AIAdvisorConfig.Keys.HTNBiasBonus);

        int picksRemaining = (int)AIDifficultySettings.CurrentDifficulty;
        HashSet<CardData> playedThisCharacter = new();
        AIContext lastContext = null;

        while (picksRemaining > 0 && deckManager != null && deckManager.HasDeckFor(leader))
        {
            CardData chosenCard = ScoreFullDeck(leader, character, actionsManager, deckManager, precomputed, playedThisCharacter, advisorBiasBonus, biasedAdvisorName)
                .OrderByDescending(scored => scored.score)
                .Select(scored => scored.card)
                .FirstOrDefault();
            if (chosenCard == null) break;

            AIContext context = await ExecuteChosenCardAsync(leader, character, actionsManager, precomputed, chosenCard, activeHtnTaskId);
            lastContext = context;

            bool shouldLog = context.LastChosenAction == null || context.LastChosenAction.LastExecutionSucceeded;
            if (shouldLog) AIActionLogger.Log(context.BuildLogEntry());

            if (context.LastChosenAction == null || !context.LastChosenAction.LastExecutionSucceeded) break;

            playedThisCharacter.Add(chosenCard);
            picksRemaining--;
        }

        if (lastContext == null)
        {
            lastContext = new AIContext(leader, character, new List<CharacterAction>(), null, precomputed) { ActiveHtnTaskId = activeHtnTaskId };
            await lastContext.PassAsync();
            AIActionLogger.Log(lastContext.BuildLogEntry());
        }

        await MoveTowardsTargetAsync(lastContext);
        actionsManager.Hide();
    }

    // Re-resolves and re-Initializes the action fresh right before executing — the action
    // instance returned by ResolveActionByRef is a shared singleton per action *type*
    // (ActionsManager caches one CharacterAction component per class), so it must never be
    // reused across candidates without a fresh Initialize immediately before each use.
    private static async Task<AIContext> ExecuteChosenCardAsync(PlayableLeader leader, Character character, ActionsManager actionsManager, AIContext.AIContextPrecomputedData? precomputed, CardData chosenCard, string activeHtnTaskId)
    {
        if (chosenCard != null)
        {
            string actionRef = NormalizeActionRef(chosenCard.GetActionRef());
            CharacterAction action = ResolveActionByRef(actionRef, actionsManager);
            if (action != null)
            {
                action.Initialize(character, chosenCard);
                AdvisorType advisor = AIAdvisorConfig.ResolveAdvisor(action);
                Dictionary<CharacterAction, CardData> actionCards = new() { [action] = chosenCard };
                AIContext context = new(leader, character, new List<CharacterAction> { action }, actionCards, precomputed) { ActiveHtnTaskId = activeHtnTaskId };
                bool executed = await context.TryExecuteChosenActionAsync(action, advisor);
                if (!executed) await context.PassAsync();
                return context;
            }
        }

        AIContext passContext = new(leader, character, new List<CharacterAction>(), null, precomputed) { ActiveHtnTaskId = activeHtnTaskId };
        await passContext.PassAsync();
        return passContext;
    }

    // Scores every playable card in the leader's full deck (not just the drawn hand) for this
    // character — one candidate at a time, each freshly Initialized immediately before it's
    // scored via a throwaway single-card AIContext, since scoring depends on the shared action
    // singleton's current card/character state. advisorBiasBonus/biasedAdvisorName let the HTN
    // layer tilt the ranking toward its currently-active strategy without restricting which
    // cards are eligible (see AIDifficulty loop in ExecuteLeaderTurn).
    private static List<(CardData card, float score)> ScoreFullDeck(PlayableLeader leader, Character character, ActionsManager actionsManager, DeckManager deckManager, AIContext.AIContextPrecomputedData? precomputed, HashSet<CardData> excluded, float advisorBiasBonus, string biasedAdvisorName)
    {
        List<(CardData card, float score)> scored = new();
        if (leader == null || character == null || actionsManager == null || deckManager == null) return scored;

        foreach (CardData card in deckManager.GetFullDeck(leader))
        {
            if (card == null || card.IsEncounterCard()) continue;
            if (excluded != null && excluded.Contains(card)) continue;

            string actionRef = NormalizeActionRef(card.GetActionRef());
            if (string.IsNullOrWhiteSpace(actionRef)) continue;

            CharacterAction action = ResolveActionByRef(actionRef, actionsManager);
            if (action == null) continue;

            action.Initialize(character, card);

            bool playable = card.EvaluatePlayability(character, null, _ => action.FulfillsConditions());
            if (!playable) continue;

            AdvisorType advisor = AIAdvisorConfig.ResolveAdvisor(action);
            float bias = !string.IsNullOrEmpty(biasedAdvisorName) && advisor.ToString() == biasedAdvisorName ? advisorBiasBonus : 0f;

            Dictionary<CharacterAction, CardData> actionCards = new() { [action] = card };
            AIContext scoringContext = new(leader, character, new List<CharacterAction> { action }, actionCards, precomputed);
            float score = scoringContext.ScoreAction(action, advisor, bias);

            scored.Add((card, score));
        }

        return scored;
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
