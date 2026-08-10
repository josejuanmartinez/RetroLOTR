using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class AIContextCacheManager : MonoBehaviour
{
    public static AIContextCacheManager Instance { get; private set; }

    private readonly Dictionary<int, AIContext.AIContextPrecomputedData> cache = new();
    private readonly Queue<(PlayableLeader leader, Character character)> workQueue = new();
    private Coroutine precomputeRoutine;
    private Game game;
    private bool rebuildRequested = false;
    private int currentQueueTotal = 0;
    private int currentQueueProcessed = 0;
    private bool queueCompletionLogged = true;
    private Stopwatch queueStopwatch = new();
    private string lastQueueDetail = string.Empty;

    [SerializeField] private float playerFrameBudgetMs = 3f;
    [SerializeField] private float aiFrameBudgetMs = 6f;
    [SerializeField] private int minimumPerFrame = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginPlayerTurnPrecompute(Game contextGame)
    {
        game = contextGame != null ? contextGame : FindFirstObjectByType<Game>();
        cache.Clear();
        rebuildRequested = true;
        EnsureRoutine();
    }

    public AIContext.AIContextPrecomputedData? GetCached(PlayableLeader leader, Character character)
    {
        if (leader == null || character == null) return null;
        int key = BuildKey(leader, character);
        if (cache.TryGetValue(key, out AIContext.AIContextPrecomputedData data)) return data;
        return null;
    }

    public void ClearCache()
    {
        cache.Clear();
    }

    private void EnsureRoutine()
    {
        if (precomputeRoutine == null)
        {
            precomputeRoutine = StartCoroutine(PrecomputeLoop());
        }
    }

    private IEnumerator PrecomputeLoop()
    {
        Stopwatch stopwatch = new Stopwatch();

        while (true)
        {
            if (game == null) game = FindFirstObjectByType<Game>();
            if (game == null || !game.started)
            {
                yield return null;
                continue;
            }

            if (rebuildRequested)
            {
                BuildWorkQueue();
                rebuildRequested = false;
            }

            float budgetMs = game.currentlyPlaying == game.player ? playerFrameBudgetMs : aiFrameBudgetMs;

            stopwatch.Restart();
            int processedThisFrame = 0;
            while (workQueue.Count > 0)
            {
                if (processedThisFrame >= minimumPerFrame && stopwatch.Elapsed.TotalMilliseconds >= budgetMs)
                {
                    break;
                }

                (PlayableLeader leader, Character character) item = workQueue.Dequeue();
                if (item.leader == null || item.character == null || item.leader.killed || item.character.killed) continue;
                // Clamp per-item build time so a single heavy build cannot stall the frame.
                float perItemBudget = Mathf.Max(0.5f, budgetMs);
                cache[BuildKey(item.leader, item.character)] = AIContextDataBuilder.Build(item.leader, item.character, perItemBudget);
                processedThisFrame++;
                currentQueueProcessed++;
            }

            if (workQueue.Count == 0 && !queueCompletionLogged && currentQueueTotal > 0)
            {
                queueCompletionLogged = true;
                queueStopwatch.Stop();
                UnityEngine.Debug.Log($"[AIContextCache] Completed caching {currentQueueProcessed}/{currentQueueTotal} items in {queueStopwatch.Elapsed.TotalMilliseconds:F1} ms (turn {game?.turn}, active={game?.currentlyPlaying?.characterName ?? "?"}); items: {lastQueueDetail}");
            }

            yield return null;
        }
    }

    private void BuildWorkQueue()
    {
        workQueue.Clear();
        if (game == null) game = FindFirstObjectByType<Game>();
        if (game == null) return;

        if (game.competitors == null) return;

        List<string> detailItems = new();

        foreach (PlayableLeader leader in game.competitors.Where(c => c != null && !c.killed))
        {
            foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed))
            {
                workQueue.Enqueue((leader, character));
                string leaderName = !string.IsNullOrEmpty(leader.characterName) ? leader.characterName : leader.name;
                string charName = !string.IsNullOrEmpty(character.characterName) ? character.characterName : character.name;
                detailItems.Add($"{leaderName}/{charName}");
            }
        }

        currentQueueTotal = workQueue.Count;
        currentQueueProcessed = 0;
        queueCompletionLogged = false;
        queueStopwatch.Restart();
        lastQueueDetail = detailItems.Count > 0 ? string.Join(", ", detailItems) : "none";
        UnityEngine.Debug.Log($"[AIContextCache] Queued {currentQueueTotal} items for caching (turn {game?.turn}, active={game?.currentlyPlaying?.characterName ?? "?"}); items: {lastQueueDetail}");
    }

    private int BuildKey(PlayableLeader leader, Character character)
    {
        unchecked
        {
            return (leader.GetInstanceID() * 397) ^ character.GetInstanceID();
        }
    }
}
