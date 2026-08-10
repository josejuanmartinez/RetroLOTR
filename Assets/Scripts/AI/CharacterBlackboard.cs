using System.Collections.Generic;

public struct HTNStackFrame
{
    public string MethodTaskId;
    public int SubtaskIndex;
}

public class CharacterBlackboard
{
    public List<HTNStackFrame> ActiveStack { get; set; } = new();
    public int TurnsOnCurrentTask { get; set; }
    public Dictionary<string, float> Facts { get; } = new();

    // The specific hex behind whichever situational parameter the active primitive task
    // prefers (see UtilityAIContext.GetTargetHexForParameter) — set once per turn in
    // AITurnController.AdvanceHtnStrategy. This is what lets a character actually travel to
    // and act on the location that triggered its current strategy, instead of only ever
    // acting on whatever hex it happens to already be standing on. Null when the active task
    // has no specific location (e.g. Economic recovery, or the generic fallback).
    public Hex TargetHex { get; set; }

    // The leader's deck-required material distribution (share of leather/mounts/timber/iron/
    // steel/mithril each sum to across every card in the leader's deck, normalized to 0..1),
    // copied in from NationBlackboard at blackboard-creation time — see
    // UtilityAIContext.GetResourceInsufficientScore/GetResourceSurplusScore, which compare this
    // target against the leader's actual current stockpile share to decide what to buy/sell.
    // Null only if no nation-level share has been computed yet for this leader.
    public IReadOnlyDictionary<ProducesEnum, float> DeckResourceShare { get; set; }
}

// Per-leader (not per-character) cache of each nation's deck-required material distribution —
// computed once, right after a leader's deck is built (DeckManager.BuildDeckStateForLeader),
// from the full composed deck before any card is drawn/discarded. Every character under that
// leader gets a copy of the same dictionary reference at blackboard-creation time (see
// CharacterBlackboardStore.GetOrCreate) — cheap, since the dictionary is treated as immutable
// once set.
public static class NationBlackboard
{
    private static readonly Dictionary<Leader, IReadOnlyDictionary<ProducesEnum, float>> deckResourceShares = new();

    public static void SetDeckResourceShare(Leader leader, IReadOnlyDictionary<ProducesEnum, float> share)
    {
        if (leader == null || share == null) return;
        deckResourceShares[leader] = share;
    }

    // Falls back to an even 1/6 split across the six tradeable materials (gold excluded — it's
    // the trade currency, not a card cost) when no deck has been registered yet for this
    // leader, so callers never have to special-case "not computed yet" themselves.
    public static IReadOnlyDictionary<ProducesEnum, float> GetDeckResourceShare(Leader leader)
    {
        if (leader != null && deckResourceShares.TryGetValue(leader, out var share)) return share;
        return EvenSplit;
    }

    private static readonly IReadOnlyDictionary<ProducesEnum, float> EvenSplit = new Dictionary<ProducesEnum, float>
    {
        [ProducesEnum.leather] = 1f / 6f,
        [ProducesEnum.mounts] = 1f / 6f,
        [ProducesEnum.timber] = 1f / 6f,
        [ProducesEnum.iron] = 1f / 6f,
        [ProducesEnum.steel] = 1f / 6f,
        [ProducesEnum.mithril] = 1f / 6f,
    };
}

public static class CharacterBlackboardStore
{
    // Keyed per (leader, character) rather than per leader: HTN strategy is decided by each
    // character's own local situation now (see AITurnController.AdvanceHtnStrategy), so each
    // character needs its own persistent stack/task-continuity state, not one shared per
    // leader. Keying on the pair (not Character alone) means a character that changes
    // allegiance (e.g. via conversion) starts fresh under its new leader rather than carrying
    // over stale commitment state from its old one.
    private static readonly Dictionary<(Leader, Character), CharacterBlackboard> blackboards = new();

    public static CharacterBlackboard GetOrCreate(Leader leader, Character character)
    {
        if (leader == null || character == null) return new CharacterBlackboard();
        var key = (leader, character);
        if (!blackboards.TryGetValue(key, out CharacterBlackboard blackboard))
        {
            blackboard = new CharacterBlackboard { DeckResourceShare = NationBlackboard.GetDeckResourceShare(leader) };
            blackboards[key] = blackboard;
        }
        return blackboard;
    }

    // Read-only lookup for debug/inspection tools (e.g. AIBlackboardDebugPanel) — unlike
    // GetOrCreate, never creates an entry, so querying a human-controlled character (one the AI
    // turn controller has never processed) correctly reports "no blackboard yet" instead of
    // silently planting a permanent empty one.
    public static bool TryGet(Leader leader, Character character, out CharacterBlackboard blackboard)
    {
        blackboard = null;
        if (leader == null || character == null) return false;
        return blackboards.TryGetValue((leader, character), out blackboard);
    }
}
