using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;
using UnityEngine.Pool;

#region Small helper to time-slice work per frame

/// <summary>
/// Simple per-frame time budget helper. Call Spent() inside hot loops and yield when true.
/// </summary>
public struct FrameBudget
{
    private float _start;
    private readonly float _maxSeconds; // e.g., 0.002f = 2ms

    public FrameBudget(float maxSeconds)
    {
        _start = Time.realtimeSinceStartup;
        _maxSeconds = maxSeconds;
    }

    public bool Spent()
    {
        return Time.realtimeSinceStartup - _start >= _maxSeconds;
    }

    public void Reset()
    {
        _start = Time.realtimeSinceStartup;
    }
}

#endregion

[RequireComponent(typeof(Board))]
public class BoardGenerator : MonoBehaviour
{
    [Header("Terrain Grid Batch configuration")]
    public int cellsPerBatch = 1000; // kept; we also time-slice by frame budget

    [Header("Game Object Hex Batch configuration")]
    public int hexesPerBatch = 20;

    [Header("Time-slicing (seconds)")]
    [Tooltip("Max main-thread time per frame that generation may use. Tighten this while video plays (e.g., 0.0015–0.003).")]
    [SerializeField] private float generationFrameBudgetSeconds = 0.002f;

    public Board board;

    // Chance parameters
    [Header("Generation Parameters")]
    [Range(1, 5)] public int minIslands = 1;
    [Range(1, 5)] public int maxIslands = 5;
    [Range(0.1f, 0.3f)] public float waterPercentage = 0.15f;
    [Range(0.1f, 1f)] public float deepWaterProportion = 0.85f;
    [Range(0.1f, 0.3f)] public float desertPercentage = 0.1395f;
    [Range(0.1f, 0.9f)] public float grasslandsProbability = 0.5f; // Chance of plains becoming grasslands
    [Range(0.1f, 0.5f)] public float coastDepth = 0.15f;

    [Header("Mountain Parameters")]
    [Range(1, 5)] public int mountainChainCount = 3;
    [Range(0.5f, 2.0f)] public float mountainChainLengthMultiplier = 1.5f;

    [Header("Forest Parameters")]
    [Range(1, 3)] public int majorForestCount = 2;
    [Range(2, 6)] public int minorForestCount = 5;

    [Header("Swamp Parameters")]
    [Range(1, 3)] public int swampCount = 2;
    [Range(4, 7)] public int swampSize = 4;

    [Header("Wastelands Parameters")]
    [Range(1, 2)] public int majorWastelandCount = 1;
    [Range(0, 3)] public int minorWastelandCount = 2;

    [Header("River and Lakes Parameters")]
    [Range(2, 5)] public int rivers = 2;
    [Range(2, 5)] public int lakes = 2;

    // Array to store the terrain types
    private TerrainEnum[,] terrainGrid;
    private int seaBorder;
    private Dictionary<Vector2Int, GameObject> hexes;
    private ObjectPool<GameObject> hexPool;
    private Dictionary<TerrainEnum, Color> terrainColors;
    private Dictionary<TerrainEnum, Sprite> terrainTextures;

    // Hexes adjacent to rivers (shallowWater cells that are part of rivers)
    public HashSet<Vector2Int> riverCoastHexes = new HashSet<Vector2Int>();

    // Hexes adjacent to lakes (shallowWater clusters generated as lakes)
    public HashSet<Vector2Int> lakeCoastHexes = new HashSet<Vector2Int>();

    // Internally used to mark which shallowWater are rivers vs lakes
    private HashSet<Vector2Int> riverCells = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> lakeCells = new HashSet<Vector2Int>();

    // Delegate for progress reporting
    public delegate void GenerationProgressDelegate(float progress, string stage);
    public event GenerationProgressDelegate OnGenerationProgress;

    // For tracking progress
    private float totalSteps = 10; // Total generation steps
    private float currentStep = 0;

    private float GetStepProgress(float stepProgress)
    {
        // If we're at the last step or beyond, just return 1.0f
        if (currentStep >= totalSteps) return 1.0f; 
        
        float totalWeight = currentStep * 0.1f;

        return Mathf.Clamp01(totalWeight);
    }

    private void Awake()
    {
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        if (board == null)
        {
            board = GetComponent<Board>();
        }

        if (board != null && board.hexPrefab != null)
        {
            hexPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(board.hexPrefab),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => obj.SetActive(false),
                actionOnDestroy: (obj) => Destroy(obj),
                defaultCapacity: 1000,
                maxSize: 10000
            );
        }
        else
        {
            Debug.LogError("Board or hexPrefab is not assigned in BoardGenerator!");
        }
    }

    /// <summary>
    /// Call this when your video starts/stops to tighten/relax budgets and loader priority.
    /// </summary>
    public void SetVideoPlaying(bool playing)
    {
        if(!playing)
        {
            generationFrameBudgetSeconds *= 100;
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
        }
    }

    public void Initialize(Board board)
    {
        if (board == null)
        {
            Debug.LogError("Board is null in BoardGenerator.Initialize!");
            return;
        }

        this.board = board;

        if (board.colors == null || board.textures == null)
        {
            Debug.LogError("Board colors or textures are not assigned!");
            return;
        }

        CacheTerrainAssets();
    }

    private void CacheTerrainAssets()
    {
        if (board == null || board.colors == null || board.textures == null)
        {
            Debug.LogError("Cannot cache terrain assets: Board, colors, or textures are null!");
            return;
        }

        terrainColors = new Dictionary<TerrainEnum, Color>();
        terrainTextures = new Dictionary<TerrainEnum, Sprite>();

        foreach (TerrainEnum terrain in Enum.GetValues(typeof(TerrainEnum)))
        {
            try
            {
                var colorFieldInfo = typeof(Colors).GetField(terrain.ToString());
                if (colorFieldInfo != null)
                {
                    terrainColors[terrain] = (Color)colorFieldInfo.GetValue(board.colors);
                }
                else
                {
                    Debug.LogWarning($"Color field not found for terrain type: {terrain}");
                }

                var textureFieldInfo = typeof(Textures).GetField(terrain.ToString());
                if (textureFieldInfo != null)
                {
                    terrainTextures[terrain] = (Sprite)textureFieldInfo.GetValue(board.textures);
                }
                else
                {
                    Debug.LogWarning($"Texture field not found for terrain type: {terrain}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error caching assets for terrain {terrain}: {e.Message}");
            }
        }
    }

    // Main entry point - now returns a coroutine
    public IEnumerator GenerateTerrainCoroutine(Action<TerrainEnum[,]> onComplete)
    {
        currentStep = 0;

        // Initialize the terrain grid with plains as default
        terrainGrid = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        yield return StartCoroutine(InitializeGridCoroutine());

        // Convert some plains to grasslands
        yield return StartCoroutine(ConvertPlainsToGrasslandsCoroutine());

        // Determine the sea border (0=North, 1=East, 2=South, 3=West)
        seaBorder = Random.Range(0, 4);

        // Generate water (sea) with more natural coastlines
        yield return StartCoroutine(GenerateWaterWithNaturalCoastlineCoroutine(seaBorder));

        // Determine desert border (different from sea border)
        int desertBorder;
        do
        {
            desertBorder = Random.Range(0, 4);
        } while (desertBorder == seaBorder);

        // Generate desert along the chosen border
        yield return StartCoroutine(GenerateDesertCoroutine(desertBorder));

        // Generate mountain chains
        yield return StartCoroutine(GenerateMountainChainsCoroutine());

        // Generate rivers and lakes (uses shallowWater only, 1-hex wide rivers, 3–6-hex lakes)
        yield return StartCoroutine(GenerateRiversAndLakesCoroutine());

        // Identify which land hexes are coastal to rivers/lakes
        yield return StartCoroutine(ComputeRiverAndLakeCoastsCoroutine());

        // Generate forests
        yield return StartCoroutine(GenerateForestsCoroutine());

        // Generate swamps
        yield return StartCoroutine(GenerateSwampsCoroutine());

        // Generate wastelands
        yield return StartCoroutine(GenerateWastelandsCoroutine());

        // Apply shore terrain to areas adjacent to shallow water
        yield return StartCoroutine(ApplyShoresCoroutine());

        OnGenerationProgress?.Invoke(1.0f, "Complete");
        onComplete?.Invoke(terrainGrid);
    }

    public IEnumerator InstantiateHexesCoroutine(Action<Dictionary<Vector2Int, GameObject>> onComplete)
    {
        hexes = new Dictionary<Vector2Int, GameObject>(board.GetHeight() * board.GetWidth());

        // Clear existing children safely and time-sliced
        yield return StartCoroutine(ClearChildrenCoroutine());

        int totalHexes = board.GetHeight() * board.GetWidth();
        int hexesProcessed = 0;

        // Pre-allocate batch buffers
        var positions = new Vector3[hexesPerBatch];
        var hexObjects = new GameObject[hexesPerBatch];
        var rowsBuf = new int[hexesPerBatch];
        var colsBuf = new int[hexesPerBatch];
        int batchIndex = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        // Safety: make sure terrainGrid exists and matches board dims
        if (terrainGrid == null ||
            terrainGrid.GetLength(0) != board.GetHeight() ||
            terrainGrid.GetLength(1) != board.GetWidth())
        {
            Debug.LogError("InstantiateHexesCoroutine: terrainGrid is null or has wrong size.");
            onComplete?.Invoke(hexes);
            yield break;
        }

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                GameObject hexGo = hexPool.Get();
                hexGo.GetComponent<Hex>().Initialize(row, col);
                hexGo.transform.SetParent(transform, false);
                hexGo.name = $"{row},{col}";

                positions[batchIndex] = GetPosition(row, col);
                hexObjects[batchIndex] = hexGo;
                rowsBuf[batchIndex] = row;   // << store exact row
                colsBuf[batchIndex] = col;   // << store exact col
                batchIndex++;

                // Flush if batch full OR time budget spent
                if (batchIndex >= hexesPerBatch || budget.Spent())
                {
                    for (int i = 0; i < batchIndex; i++)
                    {
                        var go = hexObjects[i];
                        var r = rowsBuf[i];
                        var c = colsBuf[i];

                        // bounds guard (paranoia)
                        if ((uint)r >= (uint)board.GetHeight() || (uint)c >= (uint)board.GetWidth())
                            continue;

                        go.transform.position = positions[i];

                        var hex = go.GetComponent<Hex>();
                        var sr = go.GetComponent<SpriteRenderer>();
                        var terrainType = terrainGrid[r, c];

                        sr.color = terrainColors[terrainType];
                        hex.SetTerrain(terrainType, terrainTextures[terrainType], terrainColors[terrainType]);
                        // hex.RefreshHoverText();

                        hexes[new Vector2Int(r, c)] = go;
                    }

                    hexesProcessed += batchIndex;
                    float progress = (float)hexesProcessed / totalHexes;
                    OnGenerationProgress?.Invoke(Mathf.Min(progress, 1.0f), "Configuring Board");

                    // reset batch and yield
                    batchIndex = 0;
                    budget.Reset();
                    yield return null;
                }
            }
        }

        // Flush remainder
        if (batchIndex > 0)
        {
            for (int i = 0; i < batchIndex; i++)
            {
                var go = hexObjects[i];
                var r = rowsBuf[i];
                var c = colsBuf[i];

                if ((uint)r >= (uint)board.GetHeight() || (uint)c >= (uint)board.GetWidth())
                    continue;

                go.transform.position = positions[i];

                var hex = go.GetComponent<Hex>();
                hex.Initialize(r, c);
                var sr = go.GetComponent<SpriteRenderer>();
                var terrainType = terrainGrid[r, c];

                sr.color = terrainColors[terrainType];
                hex.SetTerrain(terrainType, terrainTextures[terrainType], terrainColors[terrainType]);
                //hex.RefreshHoverText();

                hexes[new Vector2Int(r, c)] = go;
            }
        }

        OnGenerationProgress?.Invoke(1.0f, "Game ready!");
        onComplete?.Invoke(hexes);
    }


    private IEnumerator ClearChildrenCoroutine()
    {
        // Release pooled children if they belong to the pool, otherwise Destroy().
        // Time-sliced to avoid spikes.
        var budget = new FrameBudget(generationFrameBudgetSeconds);

        // Snapshot because we're modifying the hierarchy
        int count = transform.childCount;
        if (count == 0) yield break;

        var toClear = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
            toClear.Add(transform.GetChild(i).gameObject);

        foreach (var go in toClear)
        {
            // If this object came from our pool, release; otherwise, Destroy().
            // (If you're *sure* all children are pooled, you can always Release.)
            hexPool.Release(go);

            if (budget.Spent())
            {
                budget.Reset();
                yield return null;
            }
        }
    }

    private Vector3 GetPosition(int row, int col)
    {
        /*float hexWidth = board.hexSize.x * board.hexPrefab.transform.localScale.x;
        float hexHeight = board.hexSize.y * board.hexPrefab.transform.localScale.y;

        float xOffset = col * hexWidth;  // Default spacing
        float yOffset = row * hexHeight * 0.5f;  // Proper vertical stacking

        // Shift odd rows to interlock with the previous row
        if (row % 2 == 1)
        {
            xOffset += hexWidth * 0.5f; // Half of a hex board.GetWidth()
        }*/

        float xOffset = col * board.hexSize.x;
        if(row % 2 == 1) xOffset += board.hexSize.x * 0.5f; // Half of a hex board.GetWidth()
        float yOffset = row * board.hexSize.y;

        return new Vector3(xOffset, yOffset, 0);
    }

    private IEnumerator InitializeGridCoroutine()
    {
        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                terrainGrid[row, col] = TerrainEnum.plains;

                cellsProcessed++;

                // Yield by batch OR when time budget is spent
                if (cellsProcessed % cellsPerBatch == 0 || budget.Spent())
                {
                    float progress = (float)cellsProcessed / totalCells;
                    float stepProgress = GetStepProgress(progress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Configuring Terrain");
                    budget.Reset();
                    yield return null;
                }
            }
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Terrain ready");
        currentStep++;
    }

    private IEnumerator ConvertPlainsToGrasslandsCoroutine()
    {
        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatchLocal = 500;
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainGrid[row, col] == TerrainEnum.plains && Random.value < grasslandsProbability)
                {
                    terrainGrid[row, col] = TerrainEnum.grasslands;
                }

                cellsProcessed++;

                if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                {
                    float progress = (float)cellsProcessed / totalCells;
                    float stepProgress = GetStepProgress(progress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Grasslands");
                    budget.Reset();
                    yield return null;
                }
            }
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Grasslands Created");
        currentStep++;
    }

    private IEnumerator GenerateWaterWithNaturalCoastlineCoroutine(int seaBorder)
    {
        int waterWidth = Mathf.FloorToInt(board.GetWidth() * waterPercentage);
        int waterHeight = Mathf.FloorToInt(board.GetHeight() * waterPercentage);
        int coastlineDepth = Mathf.FloorToInt(Mathf.Min(board.GetWidth(), board.GetHeight()) * coastDepth);

        // Create a noise-based coastline
        int seedX = Random.Range(0, 10000);
        int seedY = Random.Range(0, 10000);

        // Process the main coastline generation in batches
        yield return StartCoroutine(GenerateCoastlineCoroutine(seaBorder, waterWidth, waterHeight, coastlineDepth, seedX, seedY));

        // Create water "fingers" extending inland
        yield return StartCoroutine(GenerateWaterFingersCoroutine(seaBorder, waterWidth, waterHeight, coastlineDepth));

        // Create islands
        yield return StartCoroutine(GenerateIslandsCoroutine(seaBorder, waterWidth, waterHeight));

        OnGenerationProgress?.Invoke(GetStepProgress(1.0f), "Water Generation Complete");
        currentStep++;
    }

    private IEnumerator GenerateCoastlineCoroutine(int seaBorder, int waterWidth, int waterHeight, int coastlineDepth, int seedX, int seedY)
    {
        int totalCells;
        int cellsPerBatchLocal = 300;
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        switch (seaBorder)
        {
            case 0: // North border
                totalCells = waterHeight * board.GetWidth();

                for (int row = 0; row < waterHeight; row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        int deepWaterBoundary = Mathf.FloorToInt(waterHeight * deepWaterProportion);

                        if (row < deepWaterBoundary - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (row < waterHeight + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(GetStepProgress(progress * 0.4f), "Creating Coastline");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 1: // East border
                totalCells = board.GetHeight() * waterWidth;

                for (int row = 0; row < board.GetHeight(); row++)
                {
                    for (int col = board.GetWidth() - waterWidth; col < board.GetWidth(); col++)
                    {
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        int deepWaterStart = board.GetWidth() - Mathf.FloorToInt(waterWidth * deepWaterProportion);

                        if (col > deepWaterStart + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (col > board.GetWidth() - waterWidth - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(GetStepProgress(progress * 0.4f), "Creating Coastline");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 2: // South border
                totalCells = waterHeight * board.GetWidth();

                for (int row = board.GetHeight() - waterHeight; row < board.GetHeight(); row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        int deepWaterStart = board.GetHeight() - Mathf.FloorToInt(waterHeight * deepWaterProportion);

                        if (row > deepWaterStart + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (row > board.GetHeight() - waterHeight - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(GetStepProgress(progress * 0.4f), "Creating Coastline");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 3: // West border
                totalCells = board.GetHeight() * waterWidth;

                for (int row = 0; row < board.GetHeight(); row++)
                {
                    for (int col = 0; col < waterWidth; col++)
                    {
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        int deepWaterBoundary = Mathf.FloorToInt(waterWidth * deepWaterProportion);

                        if (col < deepWaterBoundary - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (col < waterWidth + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(GetStepProgress(progress * 0.4f), "Creating Coastline");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;
        }
    }

    private IEnumerator GenerateWaterFingersCoroutine(int seaBorder, int waterWidth, int waterHeight, int coastlineDepth)
    {
        int numFingers = Random.Range(2, 5);
        int totalFingers = numFingers;
        int fingersProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        switch (seaBorder)
        {
            case 0: // North border - fingers extend south
                for (int i = 0; i < numFingers; i++)
                {
                    int startCol = Random.Range(0, board.GetWidth());
                    int fingerLength = Random.Range(coastlineDepth, coastlineDepth * 2);
                    int fingerWidth = Random.Range(3, 6);
                    int cellsPerFinger = fingerLength * fingerWidth;
                    int cellsProcessed = 0;

                    for (int row = waterHeight; row < waterHeight + fingerLength && row < board.GetHeight(); row++)
                    {
                        for (int colOffset = -fingerWidth / 2; colOffset <= fingerWidth / 2; colOffset++)
                        {
                            int col = startCol + colOffset;
                            if (col >= 0 && col < board.GetWidth())
                            {
                                float taperChance = (float)(row - waterHeight) / fingerLength;
                                if (Random.value > taperChance * 0.8f)
                                {
                                    if (row < waterHeight + fingerLength / 3)
                                    {
                                        terrainGrid[row, col] = TerrainEnum.shallowWater;
                                    }
                                    else
                                    {
                                        terrainGrid[row, col] = Random.value < 0.7f ?
                                            TerrainEnum.shallowWater : TerrainEnum.shore;
                                    }
                                }
                            }

                            cellsProcessed++;
                        }

                        if ((row % 3 == 0) || budget.Spent())
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke(GetStepProgress(0.4f + overallProgress * 0.3f), "Creating Water Features");
                            budget.Reset();
                            yield return null;
                        }
                    }

                    fingersProcessed++;
                }
                break;

            case 1: // East border - fingers extend west
                for (int i = 0; i < numFingers; i++)
                {
                    int startRow = Random.Range(0, board.GetHeight());
                    int fingerLength = Random.Range(coastlineDepth, coastlineDepth * 2);
                    int fingerWidth = Random.Range(3, 6);
                    int cellsPerFinger = fingerLength * fingerWidth;
                    int cellsProcessed = 0;

                    for (int col = board.GetWidth() - waterWidth - 1; col >= board.GetWidth() - waterWidth - fingerLength && col >= 0; col--)
                    {
                        for (int rowOffset = -fingerWidth / 2; rowOffset <= fingerWidth / 2; rowOffset++)
                        {
                            int row = startRow + rowOffset;
                            if (row >= 0 && row < board.GetHeight())
                            {
                                float taperChance = (float)(board.GetWidth() - waterWidth - col) / fingerLength;
                                if (Random.value > taperChance * 0.8f)
                                {
                                    if (col > board.GetWidth() - waterWidth - fingerLength / 3)
                                    {
                                        terrainGrid[row, col] = TerrainEnum.shallowWater;
                                    }
                                    else
                                    {
                                        terrainGrid[row, col] = Random.value < 0.7f ?
                                            TerrainEnum.shallowWater : TerrainEnum.shore;
                                    }
                                }
                            }

                            cellsProcessed++;
                        }

                        if (((board.GetWidth() - waterWidth - 1 - col) % 3 == 0) || budget.Spent())
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke(GetStepProgress(0.4f + overallProgress * 0.3f), "Creating Water Features");
                            budget.Reset();
                            yield return null;
                        }
                    }

                    fingersProcessed++;
                }
                break;

            case 2: // South border - fingers extend north
                for (int i = 0; i < numFingers; i++)
                {
                    int startCol = Random.Range(0, board.GetWidth());
                    int fingerLength = Random.Range(coastlineDepth, coastlineDepth * 2);
                    int fingerWidth = Random.Range(3, 6);
                    int cellsPerFinger = fingerLength * fingerWidth;
                    int cellsProcessed = 0;

                    for (int row = board.GetHeight() - waterHeight - 1; row >= board.GetHeight() - waterHeight - fingerLength && row >= 0; row--)
                    {
                        for (int colOffset = -fingerWidth / 2; colOffset <= fingerWidth / 2; colOffset++)
                        {
                            int col = startCol + colOffset;
                            if (col >= 0 && col < board.GetWidth())
                            {
                                float taperChance = (float)(board.GetHeight() - waterHeight - row) / fingerLength;
                                if (Random.value > taperChance * 0.8f)
                                {
                                    if (row > board.GetHeight() - waterHeight - fingerLength / 3)
                                    {
                                        terrainGrid[row, col] = TerrainEnum.shallowWater;
                                    }
                                    else
                                    {
                                        terrainGrid[row, col] = Random.value < 0.7f ?
                                            TerrainEnum.shallowWater : TerrainEnum.shore;
                                    }
                                }
                            }

                            cellsProcessed++;
                        }

                        if (((board.GetHeight() - waterHeight - 1 - row) % 3 == 0) || budget.Spent())
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke(GetStepProgress(0.4f + overallProgress * 0.3f), "Creating Water Features");
                            budget.Reset();
                            yield return null;
                        }
                    }

                    fingersProcessed++;
                }
                break;

            case 3: // West border - fingers extend east
                for (int i = 0; i < numFingers; i++)
                {
                    int startRow = Random.Range(0, board.GetHeight());
                    int fingerLength = Random.Range(coastlineDepth, coastlineDepth * 2);
                    int fingerWidth = Random.Range(3, 6);
                    int cellsPerFinger = fingerLength * fingerWidth;
                    int cellsProcessed = 0;

                    for (int col = waterWidth; col < waterWidth + fingerLength && col < board.GetWidth(); col++)
                    {
                        for (int rowOffset = -fingerWidth / 2; rowOffset <= fingerWidth / 2; rowOffset++)
                        {
                            int row = startRow + rowOffset;
                            if (row >= 0 && row < board.GetHeight())
                            {
                                float taperChance = (float)(col - waterWidth) / fingerLength;
                                if (Random.value > taperChance * 0.8f)
                                {
                                    if (col < waterWidth + fingerLength / 3)
                                    {
                                        terrainGrid[row, col] = TerrainEnum.shallowWater;
                                    }
                                    else
                                    {
                                        terrainGrid[row, col] = Random.value < 0.7f ?
                                            TerrainEnum.shallowWater : TerrainEnum.shore;
                                    }
                                }
                            }

                            cellsProcessed++;
                        }

                        if (((col - waterWidth) % 3 == 0) || budget.Spent())
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke(GetStepProgress(0.4f + overallProgress * 0.3f), "Creating Water Features");
                            budget.Reset();
                            yield return null;
                        }
                    }

                    fingersProcessed++;
                }
                break;
        }
    }

    private IEnumerator GenerateIslandsCoroutine(int seaBorder, int waterWidth, int waterHeight)
    {
        int numIslands = Random.Range(minIslands, maxIslands);
        int totalIslands = numIslands;
        int islandsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int i = 0; i < numIslands; i++)
        {
            int islandRow, islandCol;

            // Determine island position based on the sea border
            switch (seaBorder)
            {
                case 0: // North
                    islandRow = Random.Range(0, waterHeight);
                    islandCol = Random.Range(0, board.GetWidth());
                    break;
                case 1: // East
                    islandRow = Random.Range(0, board.GetHeight());
                    islandCol = Random.Range(board.GetWidth() - waterWidth, board.GetWidth());
                    break;
                case 2: // South
                    islandRow = Random.Range(board.GetHeight() - waterHeight, board.GetHeight());
                    islandCol = Random.Range(0, board.GetWidth());
                    break;
                case 3: // West
                    islandRow = Random.Range(0, board.GetHeight());
                    islandCol = Random.Range(0, waterWidth);
                    break;
                default:
                    islandRow = Random.Range(0, board.GetHeight());
                    islandCol = Random.Range(0, board.GetWidth());
                    break;
            }

            if (terrainGrid[islandRow, islandCol] == TerrainEnum.deepWater ||
                terrainGrid[islandRow, islandCol] == TerrainEnum.shallowWater)
            {
                int islandSize = Random.Range(3, 8);
                Queue<Vector2Int> islandCells = new Queue<Vector2Int>();
                islandCells.Enqueue(new Vector2Int(islandRow, islandCol));

                terrainGrid[islandRow, islandCol] = TerrainEnum.shore;

                int cellsProcessed = 1;
                int iterations = 0;
                int maxIterations = islandSize * 4; // Prevent infinite loops

                while (islandCells.Count > 0 && cellsProcessed < islandSize && iterations < maxIterations)
                {
                    Vector2Int current = islandCells.Dequeue();

                    Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                    foreach (Vector2Int dir in neighbors)
                    {
                        int newRow = current.x + dir.x;
                        int newCol = current.y + dir.y;

                        if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth() &&
                            (terrainGrid[newRow, newCol] == TerrainEnum.deepWater ||
                             terrainGrid[newRow, newCol] == TerrainEnum.shallowWater) &&
                            Random.value < 0.6f)
                        {
                            terrainGrid[newRow, newCol] = TerrainEnum.shore;
                            islandCells.Enqueue(new Vector2Int(newRow, newCol));
                            cellsProcessed++;
                            if (cellsProcessed >= islandSize)
                                break;
                        }
                    }

                    iterations++;

                    if ((iterations % 5 == 0) || budget.Spent())
                    {
                        budget.Reset();
                        yield return null;
                    }
                }

                // Add some shallow water around the island
                foreach (Vector2Int cell in GetNeighbors(islandRow, islandCol))
                {
                    if (terrainGrid[cell.x, cell.y] == TerrainEnum.deepWater)
                    {
                        terrainGrid[cell.x, cell.y] = TerrainEnum.shallowWater;
                    }
                }
            }

            islandsProcessed++;
            float islandProgress = (float)islandsProcessed / totalIslands;
            OnGenerationProgress?.Invoke(GetStepProgress(0.7f + islandProgress * 0.3f), "Creating Islands");

            // Yield per island if budget spent
            if (budget.Spent())
            {
                budget.Reset();
                yield return null;
            }
        }
    }

    private IEnumerator GenerateMountainChainsCoroutine()
    {
        int maxChainLength = Mathf.FloorToInt(Mathf.Max(board.GetWidth(), board.GetHeight()) * 0.4f * mountainChainLengthMultiplier);
        int totalChains = mountainChainCount;
        int chainsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int i = 0; i < mountainChainCount; i++)
        {
            int startRow, startCol;
            bool validStart = false;
            int attempts = 0;

            do
            {
                startRow = Random.Range(0, board.GetHeight());
                startCol = Random.Range(0, board.GetWidth());
                if (terrainGrid[startRow, startCol] == TerrainEnum.plains)
                {
                    validStart = true;
                }
                attempts++;
            } while (!validStart && attempts < 50);

            if (!validStart) continue;

            bool xDominant = Random.value > 0.5f;

            int chainLength = Random.Range(maxChainLength / 2, maxChainLength);

            Vector2Int current = new Vector2Int(startRow, startCol);
            terrainGrid[current.x, current.y] = TerrainEnum.mountains;

            yield return null; // small yield after setup

            for (int j = 0; j < chainLength; j++)
            {
                Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                Vector2Int nextDir;
                if (xDominant)
                {
                    nextDir = neighbors[Random.value > 0.7f ? Random.Range(0, 6) : Random.Range(1, 4) % 6];
                }
                else
                {
                    nextDir = neighbors[Random.value > 0.7f ? Random.Range(0, 6) : Random.Range(0, 3) * 2];
                }

                Vector2Int next = current + nextDir;

                if (next.x >= 0 && next.x < board.GetHeight() && next.y >= 0 && next.y < board.GetWidth() &&
                    terrainGrid[next.x, next.y] != TerrainEnum.deepWater &&
                    terrainGrid[next.x, next.y] != TerrainEnum.shallowWater)
                {
                    current = next;
                    terrainGrid[current.x, current.y] = TerrainEnum.mountains;
                }

                if ((j % 10 == 0) || budget.Spent())
                {
                    float chainProgress = (float)j / chainLength;
                    float overallProgress = (chainsProcessed + chainProgress) / totalChains;
                    float stepProgress = GetStepProgress(overallProgress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Mountains");
                    budget.Reset();
                    yield return null;
                }
            }

            chainsProcessed++;
        }

        // Add hills around mountains
        yield return StartCoroutine(AddHillsAroundMountainsCoroutine());

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Mountains Created");
        currentStep++;
    }

    private IEnumerator GenerateLakesCoroutine()
    {
        int lakesCreated = 0;
        int attempts = 0;
        var budget = new FrameBudget(generationFrameBudgetSeconds);

        while (lakesCreated < lakes && attempts < lakes * 20)
        {
            attempts++;

            int row = Random.Range(0, board.GetHeight());
            int col = Random.Range(0, board.GetWidth());

            if (!CanPlaceLakeCenter(row, col))
                continue;

            int targetSize = Random.Range(4, 8); // 5–10 inclusive

            // Collect potential cells in radius 1–2 to get a roundish cluster
            int radius = 1;
            List<Vector2Int> candidates = GetNeighborsInRadius(row, col, radius);
            List<Vector2Int> validNeighbors = new List<Vector2Int>();

            foreach (var c in candidates)
            {
                if (CanPlaceLakeCell(c.x, c.y))
                    validNeighbors.Add(c);
            }

            // Need at least 2 neighbors: center + 2 = 3 tiles minimum
            if (validNeighbors.Count < 2)
                continue;

            ShuffleList(validNeighbors);

            Vector2Int center = new Vector2Int(row, col);
            lakeCells.Add(center);

            int extra = Mathf.Min(targetSize - 1, validNeighbors.Count);
            for (int i = 0; i < extra; i++)
            {
                lakeCells.Add(validNeighbors[i]);
            }

            // Paint all selected cells as shallowWater
            foreach (var c in lakeCells)
            {
                terrainGrid[c.x, c.y] = TerrainEnum.shallowWater;
            }

            lakesCreated++;

            if (budget.Spent())
            {
                float stepProgress = GetStepProgress((float)lakesCreated / Mathf.Max(1, lakes));
                OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Lakes");
                budget.Reset();
                yield return null;
            }
        }
    }

    private IEnumerator GenerateRiversCoroutine()
    {
        var budget = new FrameBudget(generationFrameBudgetSeconds);

        // 1. Collect possible starting cells: land adjacent to mountains
        List<Vector2Int> potentialStarts = new List<Vector2Int>();

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainGrid[row, col] != TerrainEnum.mountains)
                    continue;

                Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                foreach (Vector2Int dir in neighbors)
                {
                    int nr = row + dir.x;
                    int nc = col + dir.y;
                    if (!IsInside(nr, nc)) continue;

                    if (IsLandForRiver(nr, nc))
                        potentialStarts.Add(new Vector2Int(nr, nc));
                }
            }
        }

        // No suitable sources, nothing to do
        if (potentialStarts.Count == 0)
            yield break;

        int riversCreated = 0;
        int safety = 0;

        while (riversCreated < rivers && safety < rivers * 4)
        {
            safety++;

            Vector2Int start = potentialStarts[Random.Range(0, potentialStarts.Count)];
            if (!IsLandForRiver(start.x, start.y))
                continue;

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Vector2Int current = start;

            // Reasonable upper bound: map perimeter-ish
            int maxLength = board.GetWidth() + board.GetHeight();

            for (int step = 0; step < maxLength; step++)
            {
                if (!IsInside(current.x, current.y))
                    break;

                TerrainEnum t = terrainGrid[current.x, current.y];

                if (t == TerrainEnum.deepWater)
                    break; // reached sea
                if (t == TerrainEnum.shallowWater && step > 0)
                    break; // reached another river / lake / ocean

                // 1-wide river: mark current cell as shallowWater
                terrainGrid[current.x, current.y] = TerrainEnum.shallowWater;
                riverCells.Add(current);  // track river water
                visited.Add(current);

                Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                List<Vector2Int> candidates = new List<Vector2Int>();
                foreach (Vector2Int dir in neighbors)
                {
                    int nr = current.x + dir.x;
                    int nc = current.y + dir.y;
                    Vector2Int next = new Vector2Int(nr, nc);

                    if (!IsInside(nr, nc))
                        continue;
                    if (visited.Contains(next))
                        continue;

                    TerrainEnum nt = terrainGrid[nr, nc];

                    // Don't climb mountains; rivers go around them
                    if (nt == TerrainEnum.mountains)
                        continue;

                    candidates.Add(next);
                }

                if (candidates.Count == 0)
                    break;

                // Bias: choose neighbors that move generally toward the seaBorder
                List<Vector2Int> forward = new List<Vector2Int>();
                foreach (var c in candidates)
                {
                    switch (seaBorder)
                    {
                        case 0: // sea to the north – flow northward (row decreasing)
                            if (c.x < current.x) forward.Add(c);
                            break;
                        case 1: // sea to the east – flow eastward (col increasing)
                            if (c.y > current.y) forward.Add(c);
                            break;
                        case 2: // sea to the south – flow southward (row increasing)
                            if (c.x > current.x) forward.Add(c);
                            break;
                        case 3: // sea to the west – flow westward (col decreasing)
                            if (c.y < current.y) forward.Add(c);
                            break;
                    }
                }

                List<Vector2Int> choicePool = forward.Count > 0 ? forward : candidates;
                current = choicePool[Random.Range(0, choicePool.Count)];

                if ((step % 8 == 0) || budget.Spent())
                {
                    float riverProgress = (float)riversCreated / Mathf.Max(1, rivers);
                    float stepProgress = GetStepProgress(riverProgress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Rivers");
                    budget.Reset();
                    yield return null;
                }
            }

            riversCreated++;
        }
    }

    private IEnumerator ComputeRiverAndLakeCoastsCoroutine()
    {
        riverCoastHexes.Clear();
        lakeCoastHexes.Clear();

        int height = board.GetHeight();
        int width = board.GetWidth();

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Vector2Int pos = new Vector2Int(row, col);
                TerrainEnum t = terrainGrid[row, col];

                // Only land can be a coast
                if (t == TerrainEnum.deepWater || t == TerrainEnum.shallowWater)
                    continue;

                Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                bool riverAdj = false;
                bool lakeAdj = false;

                foreach (var dir in neighbors)
                {
                    int nr = row + dir.x;
                    int nc = col + dir.y;
                    Vector2Int nPos = new Vector2Int(nr, nc);

                    if (!IsInside(nr, nc))
                        continue;

                    if (riverCells.Contains(nPos))
                        riverAdj = true;

                    if (lakeCells.Contains(nPos))
                        lakeAdj = true;
                }

                if (riverAdj)
                    riverCoastHexes.Add(pos);

                if (lakeAdj)
                    lakeCoastHexes.Add(pos);

                if (budget.Spent())
                {
                    budget.Reset();
                    yield return null;
                }
            }
        }

        yield break;
    }

    private IEnumerator GenerateRiversAndLakesCoroutine()
    {
        // Lakes first, then rivers
        yield return StartCoroutine(GenerateLakesCoroutine());
        yield return StartCoroutine(GenerateRiversCoroutine());

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Rivers & Lakes Created");
        currentStep++;
    }

    private IEnumerator AddHillsAroundMountainsCoroutine()
    {
        TerrainEnum[,] terrainCopy = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        Array.Copy(terrainGrid, terrainCopy, terrainGrid.Length);

        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatchLocal = 300;
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainCopy[row, col] == TerrainEnum.mountains)
                {
                    Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                    foreach (Vector2Int dir in neighbors)
                    {
                        int newRow = row + dir.x;
                        int newCol = col + dir.y;

                        if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                        {
                            if (terrainGrid[newRow, newCol] == TerrainEnum.plains && Random.value < 0.8f)
                            {
                                terrainGrid[newRow, newCol] = TerrainEnum.hills;
                            }
                        }
                    }
                }

                cellsProcessed++;
                if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                {
                    float progress = (float)cellsProcessed / totalCells;
                    float stepProgress = GetStepProgress(progress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Hills");
                    budget.Reset();
                    yield return null;
                }
            }
        }
    }

    private IEnumerator GenerateForestsCoroutine()
    {
        for (int i = 0; i < majorForestCount; i++)
        {
            int forestSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.08f); // 8%
            yield return StartCoroutine(GenerateForestPatchCoroutine(forestSize, "Major Forest"));
        }

        for (int i = 0; i < minorForestCount; i++)
        {
            int forestSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.02f); // 2%
            yield return StartCoroutine(GenerateForestPatchCoroutine(forestSize, "Minor Forest"));
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Forests Created");
        currentStep++;
    }

    private IEnumerator GenerateForestPatchCoroutine(int forestSize, string forestType)
    {
        int startRow, startCol;
        bool validStart = false;
        int attempts = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        do
        {
            startRow = Random.Range(0, board.GetHeight());
            startCol = Random.Range(0, board.GetWidth());
            if (terrainGrid[startRow, startCol] != TerrainEnum.deepWater &&
                terrainGrid[startRow, startCol] != TerrainEnum.shallowWater &&
                terrainGrid[startRow, startCol] != TerrainEnum.mountains)
            {
                validStart = true;
            }
            attempts++;
        } while (!validStart && attempts < 50);

        if (!validStart) yield break;

        Queue<Vector2Int> tilesToProcess = new Queue<Vector2Int>();
        tilesToProcess.Enqueue(new Vector2Int(startRow, startCol));
        terrainGrid[startRow, startCol] = TerrainEnum.forest;

        int tilesProcessed = 1;
        int iterations = 0;
        int maxIterations = forestSize * 3;

        while (tilesToProcess.Count > 0 && tilesProcessed < forestSize && iterations < maxIterations)
        {
            Vector2Int current = tilesToProcess.Dequeue();

            Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            // shuffle neighbors
            for (int i = 0; i < neighbors.Length; i++)
            {
                int j = Random.Range(i, neighbors.Length);
                (neighbors[i], neighbors[j]) = (neighbors[j], neighbors[i]);
            }

            foreach (Vector2Int dir in neighbors)
            {
                int newRow = current.x + dir.x;
                int newCol = current.y + dir.y;

                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                {
                    if (terrainGrid[newRow, newCol] == TerrainEnum.plains)
                    {
                        float distanceRatio = (float)tilesProcessed / forestSize;
                        if (Random.value > distanceRatio)
                        {
                            terrainGrid[newRow, newCol] = TerrainEnum.forest;
                            tilesToProcess.Enqueue(new Vector2Int(newRow, newCol));
                            tilesProcessed++;
                            if (tilesProcessed >= forestSize)
                                break;
                        }
                    }
                }
            }

            iterations++;

            if ((iterations % 20 == 0) || budget.Spent())
            {
                float forestProgress = (float)tilesProcessed / forestSize;
                float stepProgress = GetStepProgress(forestProgress);
                OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), $"Creating {forestType}");
                budget.Reset();
                yield return null;
            }
        }
    }

    private IEnumerator GenerateSwampsCoroutine()
    {
        int totalSwamps = swampCount;
        int swampsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int i = 0; i < swampCount; i++)
        {
            int startRow, startCol;
            bool validStart = false;
            int attempts = 0;
            bool nearWater = false;

            do
            {
                startRow = Random.Range(0, board.GetHeight());
                startCol = Random.Range(0, board.GetWidth());

                if (terrainGrid[startRow, startCol] != TerrainEnum.plains)
                {
                    attempts++;
                    continue;
                }

                validStart = true;

                Vector2Int[] neighbors = (startRow % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
                foreach (Vector2Int dir in neighbors)
                {
                    int newRow = startRow + dir.x;
                    int newCol = startCol + dir.y;

                    if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth() &&
                        (terrainGrid[newRow, newCol] == TerrainEnum.shallowWater))
                    {
                        nearWater = true;
                        break;
                    }
                }

                attempts++;

            } while ((!validStart || !nearWater) && attempts < 50);

            if (!validStart)
            {
                attempts = 0;
                do
                {
                    startRow = Random.Range(0, board.GetHeight());
                    startCol = Random.Range(0, board.GetWidth());
                    validStart = terrainGrid[startRow, startCol] == TerrainEnum.plains;
                    attempts++;
                } while (!validStart && attempts < 20);

                if (!validStart) continue;
            }

            terrainGrid[startRow, startCol] = TerrainEnum.swamp;

            Vector2Int[] swampNeighbors = (startRow % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            for (int n = 0; n < swampNeighbors.Length; n++)
            {
                int j = Random.Range(n, swampNeighbors.Length);
                (swampNeighbors[n], swampNeighbors[j]) = (swampNeighbors[j], swampNeighbors[n]);
            }

            int swampTiles = 1;
            foreach (Vector2Int dir in swampNeighbors)
            {
                if (swampTiles >= swampSize)
                    break;

                int newRow = startRow + dir.x;
                int newCol = startCol + dir.y;

                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth() &&
                    (terrainGrid[newRow, newCol] == TerrainEnum.plains || terrainGrid[newRow, newCol] == TerrainEnum.forest))
                {
                    terrainGrid[newRow, newCol] = TerrainEnum.swamp;
                    swampTiles++;
                }
            }

            swampsProcessed++;
            float swampProgress = (float)swampsProcessed / totalSwamps;
            float stepProgress = GetStepProgress(swampProgress);
            OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Swamps");

            if (budget.Spent())
            {
                budget.Reset();
                yield return null;
            }
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Swamps Created");
        currentStep++;
    }

    private IEnumerator GenerateWastelandsCoroutine()
    {
        for (int i = 0; i < majorWastelandCount; i++)
        {
            int wastelandSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.07f);
            yield return StartCoroutine(GenerateWastelandPatchCoroutine(wastelandSize, true, "Major Wasteland"));
        }

        for (int i = 0; i < minorWastelandCount; i++)
        {
            int wastelandSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.02f);
            yield return StartCoroutine(GenerateWastelandPatchCoroutine(wastelandSize, false, "Minor Wasteland"));
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Wastelands Created");
        currentStep++;
    }

    private IEnumerator GenerateWastelandPatchCoroutine(int wastelandSize, bool isMajor, string wastelandType)
    {
        int startRow, startCol;
        bool validStart = false;
        int attempts = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        if (isMajor)
        {
            do
            {
                startRow = Random.Range(board.GetHeight() / 4, 3 * board.GetHeight() / 4);
                startCol = Random.Range(board.GetWidth() / 4, 3 * board.GetWidth() / 4);
                validStart = (terrainGrid[startRow, startCol] == TerrainEnum.plains ||
                              terrainGrid[startRow, startCol] == TerrainEnum.grasslands);
                attempts++;
            } while (!validStart && attempts < 50);
        }
        else
        {
            do
            {
                startRow = Random.Range(0, board.GetHeight());
                startCol = Random.Range(0, board.GetWidth());

                validStart = (terrainGrid[startRow, startCol] == TerrainEnum.plains ||
                              terrainGrid[startRow, startCol] == TerrainEnum.grasslands);

                if (validStart)
                {
                    foreach (Vector2Int neighbor in GetNeighborsInRadius(startRow, startCol, 4))
                    {
                        if (terrainGrid[neighbor.x, neighbor.y] == TerrainEnum.wastelands)
                        {
                            validStart = false;
                            break;
                        }
                    }
                }

                attempts++;
            } while (!validStart && attempts < 50);
        }

        if (!validStart) yield break;

        terrainGrid[startRow, startCol] = TerrainEnum.wastelands;

        Queue<Vector2Int> expansionCells = new Queue<Vector2Int>();
        expansionCells.Enqueue(new Vector2Int(startRow, startCol));

        int cellsProcessed = 1;
        float noiseScale = 0.15f;
        int noiseSeed = Random.Range(0, 10000);
        int iterations = 0;
        int maxIterations = wastelandSize * 3;

        while (expansionCells.Count > 0 && cellsProcessed < wastelandSize && iterations < maxIterations)
        {
            Vector2Int current = expansionCells.Dequeue();

            Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            for (int n = 0; n < neighbors.Length; n++)
            {
                int j = Random.Range(n, neighbors.Length);
                (neighbors[n], neighbors[j]) = (neighbors[j], neighbors[n]);
            }

            foreach (Vector2Int dir in neighbors)
            {
                int newRow = current.x + dir.x;
                int newCol = current.y + dir.y;

                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                {
                    if ((terrainGrid[newRow, newCol] == TerrainEnum.plains ||
                         terrainGrid[newRow, newCol] == TerrainEnum.grasslands) &&
                        terrainGrid[newRow, newCol] != TerrainEnum.wastelands)
                    {
                        float noise = Mathf.PerlinNoise((newCol + noiseSeed) * noiseScale, (newRow + noiseSeed) * noiseScale);
                        float expandChance = isMajor ?
                            (0.8f - (float)cellsProcessed / wastelandSize * 0.5f) :
                            0.7f;

                        expandChance *= (0.5f + noise * 0.5f);

                        if (Random.value < expandChance)
                        {
                            terrainGrid[newRow, newCol] = TerrainEnum.wastelands;
                            expansionCells.Enqueue(new Vector2Int(newRow, newCol));
                            cellsProcessed++;
                            if (cellsProcessed >= wastelandSize)
                                break;
                        }
                    }
                }
            }

            iterations++;

            if ((iterations % 20 == 0) || budget.Spent())
            {
                float wastelandProgress = (float)cellsProcessed / wastelandSize;
                float stepProgress = GetStepProgress(wastelandProgress);
                OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), $"Creating {wastelandType}");
                budget.Reset();
                yield return null;
            }
        }
    }

    private IEnumerator ApplyShoresCoroutine()
    {
        TerrainEnum[,] terrainCopy = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        Array.Copy(terrainGrid, terrainCopy, terrainGrid.Length);

        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatchLocal = 300;
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainCopy[row, col] != TerrainEnum.deepWater &&
                    terrainCopy[row, col] != TerrainEnum.shallowWater)
                {
                    bool adjacentToWater = false;

                    Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                    foreach (Vector2Int dir in neighbors)
                    {
                        int newRow = row + dir.x;
                        int newCol = col + dir.y;

                        if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                        {
                            if (terrainCopy[newRow, newCol] == TerrainEnum.shallowWater)
                            {
                                Vector2Int waterPos = new Vector2Int(newRow, newCol);

                                // Only mark as "sea coast" if this shallowWater is NOT river or lake
                                if (!riverCells.Contains(waterPos) && !lakeCells.Contains(waterPos) && !riverCoastHexes.Contains(waterPos) && !lakeCoastHexes.Contains(waterPos))
                                {
                                    adjacentToWater = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (adjacentToWater && 
                        terrainCopy[row, col] != TerrainEnum.mountains &&
                        terrainCopy[row, col] != TerrainEnum.hills &&
                        terrainCopy[row, col] != TerrainEnum.swamp)
                    {
                        if (Random.value < 0.8f)
                        {
                            terrainGrid[row, col] = TerrainEnum.shore;
                        }
                    }
                }

                cellsProcessed++;
                if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                {
                    float progress = (float)cellsProcessed / totalCells;
                    float stepProgress = GetStepProgress(progress);
                    OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Shores");
                    budget.Reset();
                    yield return null;
                }
            }
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Shores Created");
    }

    private IEnumerator GenerateDesertCoroutine(int desertBorder)
    {
        int desertWidth = Mathf.FloorToInt(board.GetWidth() * desertPercentage);
        int desertHeight = Mathf.FloorToInt(board.GetHeight() * desertPercentage);
        int desertDepth = Mathf.FloorToInt(Mathf.Min(board.GetWidth(), board.GetHeight()) * 0.12f);

        int seedX = Random.Range(0, 10000);
        int seedY = Random.Range(0, 10000);

        int cellsPerBatchLocal = 300;
        int totalCells;
        int cellsProcessed = 0;

        var budget = new FrameBudget(generationFrameBudgetSeconds);

        switch (desertBorder)
        {
            case 0: // North border
                totalCells = desertHeight * board.GetWidth();

                for (int row = 0; row < desertHeight; row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (row < desertHeight - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            float stepProgress = GetStepProgress(progress);
                            OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Desert");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 1: // East border
                totalCells = board.GetHeight() * desertWidth;

                for (int row = 0; row < board.GetHeight(); row++)
                {
                    for (int col = board.GetWidth() - desertWidth; col < board.GetWidth(); col++)
                    {
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (col > board.GetWidth() - desertWidth + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            float stepProgress = GetStepProgress(progress);
                            OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Desert");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 2: // South border
                totalCells = desertHeight * board.GetWidth();

                for (int row = board.GetHeight() - desertHeight; row < board.GetHeight(); row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (row > board.GetHeight() - desertHeight + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            float stepProgress = GetStepProgress(progress);
                            OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Desert");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;

            case 3: // West border
                totalCells = board.GetHeight() * desertWidth;

                for (int row = 0; row < board.GetHeight(); row++)
                {
                    for (int col = 0; col < desertWidth; col++)
                    {
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (col < desertWidth - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatchLocal == 0 || budget.Spent())
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            float stepProgress = GetStepProgress(progress);
                            OnGenerationProgress?.Invoke(Mathf.Min(stepProgress, (currentStep + 1) / totalSteps), "Creating Desert");
                            budget.Reset();
                            yield return null;
                        }
                    }
                }
                break;
        }

        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Desert Created");
        currentStep++;
    }

    private bool IsInside(int row, int col)
    {
        return row >= 0 && row < board.GetHeight() && col >= 0 && col < board.GetWidth();
    }

    private bool IsLandForRiver(int row, int col)
    {
        TerrainEnum t = terrainGrid[row, col];

        // Rivers cannot start on or flow over deep water, other shallow water, or mountains
        return t != TerrainEnum.deepWater &&
               t != TerrainEnum.shallowWater &&
               t != TerrainEnum.mountains;
    }

    private bool CanPlaceLakeCenter(int row, int col)
    {
        if (!IsInside(row, col)) return false;

        TerrainEnum t = terrainGrid[row, col];

        // Don’t start lakes on existing water or very steep terrain
        if (t == TerrainEnum.deepWater ||
            t == TerrainEnum.shallowWater ||
            t == TerrainEnum.mountains ||
            t == TerrainEnum.hills)
            return false;

        return true;
    }

    private bool CanPlaceLakeCell(int row, int col)
    {
        if (!CanPlaceLakeCenter(row, col))
            return false;

        // Avoid lakes that directly touch deep ocean
        foreach (var n in GetNeighbors(row, col))
        {
            if (terrainGrid[n.x, n.y] == TerrainEnum.deepWater)
                return false;
        }

        return true;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }


    // A utility method to get all neighbors of a given hex
    private List<Vector2Int> GetNeighbors(int row, int col)
    {
        Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;
        List<Vector2Int> result = new List<Vector2Int>();

        foreach (Vector2Int dir in neighbors)
        {
            int newRow = row + dir.x;
            int newCol = col + dir.y;

            if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
            {
                result.Add(new Vector2Int(newRow, newCol));
            }
        }

        return result;
    }

    // Get all neighbors within a certain radius (measured in hex distance)
    private List<Vector2Int> GetNeighborsInRadius(int row, int col, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2IntWithDistance> queue = new Queue<Vector2IntWithDistance>();

        Vector2Int center = new Vector2Int(row, col);
        queue.Enqueue(new Vector2IntWithDistance(center, 0));
        visited.Add(center);

        while (queue.Count > 0)
        {
            Vector2IntWithDistance current = queue.Dequeue();

            if (current.distance > 0)
            {
                result.Add(current.position);
            }

            if (current.distance >= radius)
                continue;

            Vector2Int[] neighbors = (current.position.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            foreach (Vector2Int dir in neighbors)
            {
                int newRow = current.position.x + dir.x;
                int newCol = current.position.y + dir.y;
                Vector2Int next = new Vector2Int(newRow, newCol);

                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth() && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(new Vector2IntWithDistance(next, current.distance + 1));
                }
            }
        }

        return result;
    }

    // Helper struct for tracking distance in the neighbor radius search
    private struct Vector2IntWithDistance
    {
        public Vector2Int position;
        public int distance;

        public Vector2IntWithDistance(Vector2Int position, int distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }
}
