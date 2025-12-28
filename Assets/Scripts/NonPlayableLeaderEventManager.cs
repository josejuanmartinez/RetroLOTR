using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class EventDefinitionCollection
{
    public List<NonPlayableLeaderEventDefinition> events = new();
}

[Serializable]
public class NonPlayableLeaderEventDefinition
{
    public string eventId;
    public string leaderName;
    public string title;
    public string narration;
    public string alignmentRule;
    public EventSpawn spawn;
    public EventMovement movement;
    public EventActions actions;

    public string GetActionClassName(out string role)
    {
        role = null;
        if (actions == null) return null;
        if (!string.IsNullOrWhiteSpace(actions.commander))
        {
            role = "commander";
            return actions.commander;
        }
        if (!string.IsNullOrWhiteSpace(actions.agent))
        {
            role = "agent";
            return actions.agent;
        }
        if (!string.IsNullOrWhiteSpace(actions.mage))
        {
            role = "mage";
            return actions.mage;
        }
        if (!string.IsNullOrWhiteSpace(actions.emmissary))
        {
            role = "emmissary";
            return actions.emmissary;
        }
        return null;
    }
}

[Serializable]
public class EventSpawn
{
    public string character;
    public string location;
    public EventSpawnArmy army;
}

[Serializable]
public class EventSpawnArmy
{
    public string type;
    public int size;
}

[Serializable]
public class EventMovement
{
    public string target;
    public int maxTries = 6;
    public bool giveUpIfBlocked = true;
}

[Serializable]
public class EventActions
{
    public string commander;
    public string agent;
    public string mage;
    public string emmissary;
}

public class NonPlayableLeaderEventManager : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float maxEventChancePerLeader = 0.05f;
    [SerializeField] private int spawnSearchRadius = 3;
    [SerializeField] private bool debugEvents = false;

    private Game game;
    private Board board;
    private ActionsManager actionsManager;
    private Illustrations illustrations;
    private EventDefinitionCollection eventDefinitions;
    private readonly Dictionary<string, List<NonPlayableLeaderEventDefinition>> eventsByLeader = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ActiveEvent> activeEvents = new();
    private bool processingTurn;

    private class ActiveEvent
    {
        public NonPlayableLeaderEventDefinition definition;
        public NonPlayableLeader leader;
        public Character actor;
        public int turnsRemaining;
        public Hex originalHex;
        public Character.StatusSnapshot originalStatus;
        public bool createdArmy;
        public Army spawnedArmy;
    }

    private void OnEnable()
    {
        CacheReferences();
        LoadEvents();
        if (game != null) game.NewTurnStarted += HandleNewTurn;
    }

    private void OnDisable()
    {
        if (game != null) game.NewTurnStarted -= HandleNewTurn;
    }

    private void CacheReferences()
    {
        if (game == null) game = FindFirstObjectByType<Game>();
        if (board == null) board = FindFirstObjectByType<Board>();
        if (actionsManager == null) actionsManager = FindFirstObjectByType<ActionsManager>();
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
    }

    private void LoadEvents()
    {
        if (eventDefinitions != null && eventDefinitions.events.Count > 0) return;
        TextAsset json = Resources.Load<TextAsset>("Events");
        if (json == null) return;

        eventDefinitions = JsonUtility.FromJson<EventDefinitionCollection>(json.text);
        if (eventDefinitions == null || eventDefinitions.events == null) return;

        eventsByLeader.Clear();
        foreach (NonPlayableLeaderEventDefinition definition in eventDefinitions.events)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.leaderName)) continue;
            if (!eventsByLeader.TryGetValue(definition.leaderName, out List<NonPlayableLeaderEventDefinition> list))
            {
                list = new List<NonPlayableLeaderEventDefinition>();
                eventsByLeader[definition.leaderName] = list;
            }
            list.Add(definition);
        }
    }

    private void HandleNewTurn(int turn)
    {
        if (processingTurn) return;
        if (game == null || !game.started) return;
        StartCoroutine(ProcessTurnEvents());
    }

    private IEnumerator ProcessTurnEvents()
    {
        processingTurn = true;
        CacheReferences();
        LoadEvents();

        if (game == null || board == null)
        {
            processingTurn = false;
            yield break;
        }

        CleanupInactiveEvents();

        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            ActiveEvent active = activeEvents[i];
            if (active == null)
            {
                activeEvents.RemoveAt(i);
                continue;
            }

            yield return ProcessActiveEvent(active);

            if (active.turnsRemaining <= 0 || active.actor == null || active.actor.killed)
            {
                activeEvents.RemoveAt(i);
            }
        }

        TrySpawnEvents();
        processingTurn = false;
    }

    private void CleanupInactiveEvents()
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            ActiveEvent active = activeEvents[i];
            if (active == null)
            {
                activeEvents.RemoveAt(i);
                continue;
            }

            if (active.leader == null || active.leader.killed || active.leader.joined)
            {
                activeEvents.RemoveAt(i);
                continue;
            }

            if (active.actor == null || active.actor.killed)
            {
                activeEvents.RemoveAt(i);
            }
        }
    }

    private IEnumerator ProcessActiveEvent(ActiveEvent active)
    {
        if (active == null || active.definition == null) yield break;
        if (active.turnsRemaining <= 0) yield break;
        if (active.leader == null || active.leader.killed || active.leader.joined) { active.turnsRemaining = 0; yield break; }
        if (active.actor == null || active.actor.killed) { active.turnsRemaining = 0; yield break; }

        active.actor.NewTurn();

        Hex target = SelectTargetHex(active);
        if (target == null)
        {
            LogDebug($"Event {active.definition.eventId} has no target.");
            active.turnsRemaining--;
            if (active.turnsRemaining <= 0) EndActiveEvent(active);
            yield break;
        }

        bool moved = MoveActorToward(active.actor, target);
        if (!moved && active.definition.movement != null && active.definition.movement.giveUpIfBlocked)
        {
            LogDebug($"Event {active.definition.eventId} could not move toward target.");
            active.turnsRemaining--;
            if (active.turnsRemaining <= 0) EndActiveEvent(active);
            yield break;
        }

        if (active.actor.hex == target)
        {
            bool succeeded = false;
            yield return TryExecuteAction(active, result => succeeded = result);
            if (succeeded)
            {
                LogDebug($"Event {active.definition.eventId} succeeded.");
                ShowSuccessPopup(active);
                EndActiveEvent(active);
                yield break;
            }
        }

        active.turnsRemaining--;
        if (active.turnsRemaining <= 0) EndActiveEvent(active);
    }

    private void TrySpawnEvents()
    {
        if (game == null || game.player == null || game.npcs == null) return;
        if (eventDefinitions == null || eventDefinitions.events == null || eventDefinitions.events.Count == 0) return;

        foreach (NonPlayableLeader npl in game.npcs)
        {
            if (npl == null || npl.killed || npl.joined) continue;
            if (!IsEnemyOrNeutral(npl, game.player)) continue;
            if (HasActiveEvent(npl)) continue;
            if (UnityEngine.Random.value > maxEventChancePerLeader) continue;

            if (!eventsByLeader.TryGetValue(npl.characterName, out List<NonPlayableLeaderEventDefinition> options)) continue;
            List<NonPlayableLeaderEventDefinition> viable = options
                .Where(def => def != null && CanSpawnEventForLeader(npl, def))
                .ToList();

            if (viable.Count == 0) continue;

            NonPlayableLeaderEventDefinition picked = viable[UnityEngine.Random.Range(0, viable.Count)];
            if (picked == null) continue;

            Character actor = FindCharacterByName(npl, picked.spawn?.character);
            if (actor == null || actor.killed)
            {
                LogDebug($"Event {picked?.eventId} skipped for {npl.characterName}: actor missing or killed.");
                continue;
            }

            Hex target = SelectTargetHex(npl, picked);
            if (target == null)
            {
                LogDebug($"Event {picked.eventId} skipped for {npl.characterName}: no valid target.");
                continue;
            }

            Hex spawnHex = ChooseSpawnHex(target);
            if (spawnHex == null)
            {
                LogDebug($"Event {picked.eventId} skipped for {npl.characterName}: no hidden spawn.");
                continue;
            }

            if (!EnsureArmyForEvent(actor, picked, out bool createdArmy))
            {
                LogDebug($"Event {picked.eventId} skipped for {npl.characterName}: army could not be created.");
                continue;
            }

            Army spawnedArmy = createdArmy ? actor.GetArmy() : null;
            Hex originalHex = actor.hex;
            Character.StatusSnapshot originalStatus = actor.CaptureStatusSnapshot();

            TeleportCharacter(actor, spawnHex);

            ActiveEvent active = new()
            {
                definition = picked,
                leader = npl,
                actor = actor,
                turnsRemaining = Mathf.Max(1, picked.movement != null ? picked.movement.maxTries : 1),
                originalHex = originalHex,
                originalStatus = originalStatus,
                createdArmy = createdArmy,
                spawnedArmy = spawnedArmy
            };
            activeEvents.Add(active);
            LogDebug($"Spawned event {picked.eventId} for {npl.characterName} at {spawnHex.v2} targeting {target.v2}.");
        }
    }

    private bool HasActiveEvent(NonPlayableLeader leader)
    {
        return activeEvents.Any(ev => ev != null && ev.leader == leader);
    }

    private bool CanSpawnEventForLeader(NonPlayableLeader leader, NonPlayableLeaderEventDefinition definition)
    {
        if (leader == null || definition == null) return false;
        if (definition.spawn == null || string.IsNullOrWhiteSpace(definition.spawn.character)) return false;

        Character actor = FindCharacterByName(leader, definition.spawn.character);
        if (actor == null || actor.killed) return false;

        string actionName = definition.GetActionClassName(out string role);
        if (string.IsNullOrWhiteSpace(actionName)) return false;

        if (string.Equals(role, "commander", StringComparison.OrdinalIgnoreCase))
        {
            if (actor.GetCommander() <= 0) return false;
        }
        return true;
    }

    private bool EnsureArmyForEvent(Character actor, NonPlayableLeaderEventDefinition definition, out bool createdArmy)
    {
        createdArmy = false;
        if (actor == null || definition == null) return false;
        string actionName = definition.GetActionClassName(out string role);
        if (!string.Equals(role, "commander", StringComparison.OrdinalIgnoreCase)) return true;

        if (actor.IsArmyCommander()) return true;

        if (definition.spawn?.army == null || definition.spawn.army.size <= 0) return false;
        if (actor.GetCommander() <= 0) return false;

        TroopsTypeEnum troopType = ParseTroopType(definition.spawn.army.type);
        actor.CreateArmy(troopType, definition.spawn.army.size, false);
        createdArmy = actor.IsArmyCommander();
        return actor.IsArmyCommander();
    }

    private static TroopsTypeEnum ParseTroopType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return TroopsTypeEnum.ma;
        string cleaned = type.Trim().ToLowerInvariant();
        return cleaned switch
        {
            "ma" => TroopsTypeEnum.ma,
            "ar" => TroopsTypeEnum.ar,
            "li" => TroopsTypeEnum.li,
            "hi" => TroopsTypeEnum.hi,
            "lc" => TroopsTypeEnum.lc,
            "hc" => TroopsTypeEnum.hc,
            "ca" => TroopsTypeEnum.ca,
            "ws" => TroopsTypeEnum.ws,
            _ => TroopsTypeEnum.ma
        };
    }

    private bool IsEnemyOrNeutral(NonPlayableLeader npl, PlayableLeader player)
    {
        if (npl == null || player == null) return false;
        AlignmentEnum nplAlignment = npl.GetAlignment();
        AlignmentEnum playerAlignment = player.GetAlignment();
        if (nplAlignment == AlignmentEnum.neutral) return true;
        return nplAlignment != playerAlignment;
    }

    private Character FindCharacterByName(NonPlayableLeader leader, string characterName)
    {
        if (leader == null || string.IsNullOrWhiteSpace(characterName)) return null;
        return leader.controlledCharacters.FirstOrDefault(ch => ch != null && ch.characterName == characterName);
    }

    private Hex SelectTargetHex(ActiveEvent active)
    {
        if (active == null || active.definition == null || active.leader == null) return null;
        return SelectTargetHex(active.leader, active.definition);
    }

    private Hex SelectTargetHex(NonPlayableLeader leader, NonPlayableLeaderEventDefinition definition)
    {
        if (game == null || board == null || game.player == null) return null;

        string actionName = definition.GetActionClassName(out _);
        return SelectTargetHexForAction(leader, actionName);
    }

    private Hex SelectTargetHexForAction(NonPlayableLeader leader, string actionName)
    {
        if (game == null || game.player == null) return null;
        Hex reference = GetCapitalHex(leader) ?? leader.hex;
        if (reference == null) return null;

        List<Character> playerCharacters = game.player.controlledCharacters
            .Where(ch => ch != null && !ch.killed)
            .ToList();
        List<PC> playerPcs = game.player.controlledPcs
            .Where(pc => pc != null && pc.owner == game.player)
            .ToList();
        List<Character> playerArmyCommanders = playerCharacters.Where(ch => ch.IsArmyCommander()).ToList();

        if (IsActionInSet(actionName, EnemyPcActions))
        {
            IEnumerable<PC> pcs = playerPcs;
            if (string.Equals(actionName, "DestroyFortifications", StringComparison.OrdinalIgnoreCase))
            {
                pcs = pcs.Where(pc => pc.fortSize > FortSizeEnum.NONE);
            }
            Func<PC, int> distance = pc => HexDistance(reference, pc.hex);
            IOrderedEnumerable<PC> ordered = pcs.OrderBy(distance);
            if (string.Equals(actionName, "SiegePC", StringComparison.OrdinalIgnoreCase))
            {
                ordered = ordered.ThenByDescending(pc => pc.citySize);
            }
            else if (string.Equals(actionName, "InfluenceDownPC", StringComparison.OrdinalIgnoreCase))
            {
                ordered = ordered.ThenByDescending(pc => pc.loyalty);
            }

            PC bestPc = ordered.FirstOrDefault();
            return bestPc?.hex;
        }

        if (IsActionInSet(actionName, EnemyArmyActions))
        {
            if (playerArmyCommanders.Count == 0) return null;
            Character bestArmy = playerArmyCommanders
                .OrderBy(ch => HexDistance(reference, ch.hex))
                .ThenByDescending(ch => ch.GetArmy() != null ? ch.GetArmy().GetStrength() : 0)
                .FirstOrDefault();
            return bestArmy?.hex;
        }

        if (string.Equals(actionName, "Attack", StringComparison.OrdinalIgnoreCase))
        {
            PC targetPc = playerPcs.OrderBy(pc => HexDistance(reference, pc.hex)).FirstOrDefault();
            if (targetPc != null) return targetPc.hex;
            Character targetArmy = playerArmyCommanders.OrderBy(ch => HexDistance(reference, ch.hex)).FirstOrDefault();
            return targetArmy?.hex;
        }

        if (IsActionInSet(actionName, EnemyCharacterActions))
        {
            IEnumerable<Character> candidates = playerCharacters;
            if (string.Equals(actionName, "Halt", StringComparison.OrdinalIgnoreCase))
            {
                candidates = candidates.Where(ch => !ch.IsArmyCommander());
            }
            if (string.Equals(actionName, "StealArtifact", StringComparison.OrdinalIgnoreCase))
            {
                candidates = candidates.Where(ch => ch.GetTransferableArtifacts().Count > 0);
            }
            Character bestChar = candidates
                .OrderBy(ch => HexDistance(reference, ch.hex))
                .FirstOrDefault();
            return bestChar?.hex;
        }

        if (playerPcs.Count > 0)
        {
            return playerPcs.OrderBy(pc => HexDistance(reference, pc.hex)).FirstOrDefault()?.hex;
        }

        return playerCharacters.OrderBy(ch => HexDistance(reference, ch.hex)).FirstOrDefault()?.hex;
    }

    private static readonly HashSet<string> EnemyCharacterActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "WoundCharacter",
        "AssassinateCharacter",
        "StealArtifact",
        "Halt"
    };

    private static readonly HashSet<string> EnemyPcActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "StealGold",
        "SabotageStorage",
        "InfluenceDownPC",
        "DestroyFortifications",
        "SiegePC"
    };

    private static readonly HashSet<string> EnemyArmyActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Block",
        "WizardsFire",
        "WordsOfDispair",
        "IceStorm"
    };

    private static bool IsActionInSet(string actionName, HashSet<string> set)
    {
        if (string.IsNullOrWhiteSpace(actionName) || set == null) return false;
        return set.Contains(actionName);
    }

    private Hex GetCapitalHex(NonPlayableLeader leader)
    {
        if (leader == null) return null;
        PC capital = leader.controlledPcs.FirstOrDefault(pc => pc != null && pc.isCapital);
        return capital?.hex;
    }

    private Hex ChooseSpawnHex(Hex target)
    {
        if (target == null) return null;
        List<Hex> candidates = target.GetHexesInRadius(spawnSearchRadius)
            .Where(h => h != null && IsHiddenOrUnseenForPlayer(h) && !h.HasAnyPC())
            .ToList();

        if (candidates.Count == 0 && board != null)
        {
            candidates = board.GetHexes()
                .Where(h => h != null && IsHiddenOrUnseenForPlayer(h) && !h.HasAnyPC())
                .ToList();
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private bool IsHiddenOrUnseenForPlayer(Hex hex)
    {
        if (hex == null) return false;
        return !hex.IsHexSeen();
    }

    private void TeleportCharacter(Character character, Hex destination)
    {
        if (character == null || destination == null) return;
        Hex previous = character.hex;
        if (previous == destination) return;

        if (previous != null)
        {
            previous.characters.Remove(character);
            if (character.IsArmyCommander() && character.GetArmy() != null)
            {
                previous.armies.Remove(character.GetArmy());
            }
            previous.RedrawCharacters();
            previous.RedrawArmies();
        }

        if (!destination.characters.Contains(character))
        {
            destination.characters.Add(character);
        }
        if (character.IsArmyCommander() && character.GetArmy() != null && !destination.armies.Contains(character.GetArmy()))
        {
            destination.armies.Add(character.GetArmy());
        }
        character.hex = destination;
        destination.RedrawCharacters();
        destination.RedrawArmies();

        Leader owner = character.GetOwner();
        if (owner != null)
        {
            if (previous != null && !owner.LeaderSeesHex(previous))
            {
                owner.visibleHexes.Remove(previous);
            }
            if (!owner.visibleHexes.Contains(destination))
            {
                owner.visibleHexes.Add(destination);
            }
        }
    }

    private bool MoveActorToward(Character actor, Hex target)
    {
        if (actor == null || target == null || board == null) return false;
        if (actor.hex == target) return true;

        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        if (pathRenderer == null) return false;

        List<Vector2Int> path = pathRenderer.FindPath(actor.hex.v2, target.v2, actor);
        if (path == null || path.Count < 2) return false;

        int movementLeft = actor.GetMovementLeft();
        for (int i = 0; i < path.Count - 1; i++)
        {
            Hex from = board.GetHex(path[i]);
            Hex to = board.GetHex(path[i + 1]);
            if (from == null || to == null) break;

            int cost = to.GetTerrainCost(actor);
            if (movementLeft < cost) break;

            board.MoveCharacterOneHex(actor, from, to, false, false);
            movementLeft = actor.GetMovementLeft();
            if (actor.hex == target) break;
        }

        return true;
    }

    private IEnumerator TryExecuteAction(ActiveEvent active, Action<bool> onFinished)
    {
        bool succeeded = false;
        bool actionExecuted = false;
        if (active != null && active.actor != null && active.definition != null)
        {
            string actionName = active.definition.GetActionClassName(out _);
            CharacterAction action = FindActionByClassName(actionName);
            if (action != null)
            {
                ActionRequirementSnapshot snapshot = OverrideSkillRequirements(action);
                try
                {
                    action.Initialize(active.actor);
                    if (action.FulfillsConditions())
                    {
                        Task task = action.Execute();
                        while (!task.IsCompleted) yield return null;
                        actionExecuted = true;
                        succeeded = action.LastExecutionSucceeded;
                    }
                    else
                    {
                        LogDebug($"Event {active.definition.eventId} action {actionName} not valid at target.");
                    }
                }
                finally
                {
                    RestoreSkillRequirements(action, snapshot);
                }
            }
            else
            {
                LogDebug($"Event {active.definition.eventId} action {actionName} not found.");
            }
        }

        if (actionExecuted)
        {
            RefreshHexAfterAction(active?.actor);
        }

        onFinished?.Invoke(succeeded);
    }

    private CharacterAction FindActionByClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className) || actionsManager == null) return null;
        if (actionsManager.characterActions == null || actionsManager.characterActions.Length == 0)
        {
            actionsManager.characterActions = actionsManager.GetComponentsInChildren<CharacterAction>(true);
        }

        return actionsManager.characterActions
            .FirstOrDefault(action => action != null && string.Equals(action.GetType().Name, className, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowSuccessPopup(ActiveEvent active)
    {
        if (active == null || game == null || game.player == null) return;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        Sprite actor1 = illustrations != null ? illustrations.GetIllustrationByName(active.definition.leaderName) : null;
        Sprite actor2 = illustrations != null ? illustrations.GetIllustrationByName(game.player.characterName) : null;
        PopupManager.Show(active.definition.title, actor1, actor2, active.definition.narration, true);
    }

    private void RefreshHexAfterAction(Character actor)
    {
        if (actor == null || actor.killed || actor.hex == null) return;

        Hex hex = actor.hex;
        if (hex.HasAnyPC())
        {
            hex.RedrawPC();
        }
        hex.RedrawArmies();
        hex.RedrawCharacters();
    }

    private void EndActiveEvent(ActiveEvent active)
    {
        if (active == null) return;
        ReturnActorToOrigin(active);
        DisbandEventArmy(active);
        active.turnsRemaining = 0;
    }

    private void ReturnActorToOrigin(ActiveEvent active)
    {
        if (active.actor == null || active.actor.killed) return;

        if (active.originalHex != null && active.actor.hex != active.originalHex)
        {
            TeleportCharacter(active.actor, active.originalHex);
        }

        active.actor.RestoreStatusSnapshot(active.originalStatus);
    }

    private void DisbandEventArmy(ActiveEvent active)
    {
        if (active.actor == null || active.actor.killed) return;
        if (!active.createdArmy) return;
        if (active.spawnedArmy != null && active.actor.GetArmy() == active.spawnedArmy)
        {
            active.actor.DisbandArmy();
        }
    }

    private static int HexDistance(Hex a, Hex b)
    {
        if (a == null || b == null) return int.MaxValue;
        Vector3Int cubeA = OffsetToCube(a.v2);
        Vector3Int cubeB = OffsetToCube(b.v2);
        return (Mathf.Abs(cubeA.x - cubeB.x) + Mathf.Abs(cubeA.y - cubeB.y) + Mathf.Abs(cubeA.z - cubeB.z)) / 2;
    }

    private static Vector3Int OffsetToCube(Vector2Int offset)
    {
        int x = offset.x;
        int z = offset.y - (offset.x - (offset.x & 1)) / 2;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    private void LogDebug(string message)
    {
        if (!debugEvents) return;
        Debug.Log($"[NPL Events] {message}");
    }

    private struct ActionRequirementSnapshot
    {
        public int commanderSkillRequired;
        public int agentSkillRequired;
        public int emissarySkillRequired;
        public int mageSkillRequired;
    }

    private ActionRequirementSnapshot OverrideSkillRequirements(CharacterAction action)
    {
        ActionRequirementSnapshot snapshot = new()
        {
            commanderSkillRequired = action.commanderSkillRequired,
            agentSkillRequired = action.agentSkillRequired,
            emissarySkillRequired = action.emissarySkillRequired,
            mageSkillRequired = action.mageSkillRequired
        };
        action.commanderSkillRequired = 0;
        action.agentSkillRequired = 0;
        action.emissarySkillRequired = 0;
        action.mageSkillRequired = 0;
        return snapshot;
    }

    private void RestoreSkillRequirements(CharacterAction action, ActionRequirementSnapshot snapshot)
    {
        action.commanderSkillRequired = snapshot.commanderSkillRequired;
        action.agentSkillRequired = snapshot.agentSkillRequired;
        action.emissarySkillRequired = snapshot.emissarySkillRequired;
        action.mageSkillRequired = snapshot.mageSkillRequired;
    }
}
