using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class NonPlayableLeaderEventDefinition
{
    public string eventId;
    public string leaderName;
    public string title;
    public string description;
    public string actionClassName;
    public string targetType; // "self", "closestEnemy", "nearestPc", etc.
    public float chanceWeight = 1.0f;
    public List<string> requiredTags = new();
}

[Serializable]
public class NonPlayableLeaderEventsCollection
{
    public List<NonPlayableLeaderEventDefinition> events = new();
}

public class NonPlayableLeaderEventManager : MonoBehaviour
{
    public static NonPlayableLeaderEventManager Instance { get; private set; }

    [Header("Config")]
    public string eventsResourcePath = "Events";
    public float maxEventChancePerLeader = 0.05f;
    public bool debugEvents = true;
    public int minTurnToEnableEvents = 5;

    [Header("Execution Tuning")]
    public float eventFollowDuration = 0.6f;
    public float eventFollowPause = 0.3f;
    public float eventStepDelay = 0.25f;

    private readonly Dictionary<string, List<NonPlayableLeaderEventDefinition>> eventsByLeader = new(StringComparer.OrdinalIgnoreCase);
    private bool loaded;
    public bool IsProcessingTurn { get; private set; }
    private Game game;
    private Board board;
    private DeckManager deckManager;
    private ActionsManager actionsManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        game = FindFirstObjectByType<Game>();
        board = FindFirstObjectByType<Board>();
        deckManager = FindFirstObjectByType<DeckManager>();
        actionsManager = FindFirstObjectByType<ActionsManager>();
        LoadEvents();
    }

    private void LoadEvents()
    {
        TextAsset json = Resources.Load<TextAsset>(eventsResourcePath);
        if (json == null)
        {
            Debug.LogWarning($"NonPlayableLeaderEventManager: Could not load events from Resources/{eventsResourcePath}.json");
            return;
        }

        NonPlayableLeaderEventsCollection collection = JsonUtility.FromJson<NonPlayableLeaderEventsCollection>(json.text);
        if (collection?.events == null) return;

        foreach (var definition in collection.events)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.leaderName)) continue;
            if (!eventsByLeader.TryGetValue(definition.leaderName, out List<NonPlayableLeaderEventDefinition> list))
            {
                list = new List<NonPlayableLeaderEventDefinition>();
                eventsByLeader[definition.leaderName] = list;
            }
            list.Add(definition);
        }
        loaded = true;
    }

    public async Task RunEventsForLeader(NonPlayableLeader leader)
    {
        if (!loaded || leader == null || leader.killed) return;
        if (game.turn < minTurnToEnableEvents) return;

        if (!eventsByLeader.TryGetValue(leader.characterName, out List<NonPlayableLeaderEventDefinition> possibleEvents))
        {
            return;
        }

        IsProcessingTurn = true;
        try
        {
            foreach (var evt in possibleEvents)
            {
                if (UnityEngine.Random.value > maxEventChancePerLeader) continue;

                await TryExecuteEvent(leader, evt);
            }
        }
        finally
        {
            IsProcessingTurn = false;
        }
    }

    private async Task TryExecuteEvent(NonPlayableLeader leader, NonPlayableLeaderEventDefinition evt)
    {
        Character actor = leader; // For now, the leader is always the actor for these events
        if (actor == null || actor.killed) return;

        CharacterAction action = actionsManager.ResolveActionByRef(evt.actionClassName);
        if (action == null) return;

        // In the new system, we'd find a card that matches this action if we wanted to use card data
        CardData card = deckManager.cards.FirstOrDefault(c => string.Equals(c.GetActionRef(), evt.actionClassName, StringComparison.OrdinalIgnoreCase));
        action.Initialize(actor, card);

        if (!action.FulfillsConditions()) return;

        if (debugEvents) Debug.Log($"NonPlayableLeaderEventManager: {leader.characterName} executing event {evt.eventId} ({evt.actionClassName})");

        // Focus the camera if player can see
        if (PlayerCanSee(actor.hex))
        {
            BoardNavigator.Instance?.EnqueueFocus(actor.hex, eventFollowDuration, eventFollowPause);
            await Task.Delay((int)(eventFollowDuration * 1000 + eventFollowPause * 1000));
        }

        await action.Execute();
    }

    private bool PlayerCanSee(Hex hex)
    {
        if (hex == null || game == null || game.player == null) return false;
        return game.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
