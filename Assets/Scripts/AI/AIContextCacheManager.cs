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

    [SerializeField] private float playerFrameBudgetMs = 3f;
    [SerializeField] private float aiFrameBudgetMs = 6f;
    [SerializeField] private int minimumPerFrame = 1;

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
            while (workQueue.Count > 0 && (processedThisFrame < minimumPerFrame || stopwatch.Elapsed.TotalMilliseconds < budgetMs))
            {
                (PlayableLeader leader, Character character) item = workQueue.Dequeue();
                if (item.leader == null || item.character == null || item.leader.killed || item.character.killed) continue;
                cache[BuildKey(item.leader, item.character)] = AIContextDataBuilder.Build(item.leader, item.character);
                processedThisFrame++;
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

        foreach (PlayableLeader leader in game.competitors.Where(c => c != null && !c.killed))
        {
            foreach (Character character in leader.controlledCharacters.Where(c => c != null && !c.killed))
            {
                workQueue.Enqueue((leader, character));
            }
        }
    }

    private int BuildKey(PlayableLeader leader, Character character)
    {
        unchecked
        {
            return (leader.GetInstanceID() * 397) ^ character.GetInstanceID();
        }
    }
}
