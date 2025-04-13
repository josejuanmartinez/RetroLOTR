using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;
using System.Linq;
using UnityEngine.Pool;

[RequireComponent(typeof(Board))]
public class BoardGenerator : MonoBehaviour
{
    [Header("Terrain Grid Batch configuration")]
    public int cellsPerBatch = 1000; // Increased batch size for better performance
    [Header("Game Object Hex Batch configuration")]
    public int hexesPerBatch = 20; // Increased batch size for better performance

    public Board board;
    // Array to store the terrain types
    private TerrainEnum[,] terrainGrid;
    private Dictionary<Vector2, GameObject> hexes;
    private ObjectPool<GameObject> hexPool;
    private Dictionary<TerrainEnum, Color> terrainColors;
    private Dictionary<TerrainEnum, Sprite> terrainTextures;

    // Delegate for progress reporting
    public delegate void GenerationProgressDelegate(float progress, string stage);
    public event GenerationProgressDelegate OnGenerationProgress;

    // For tracking progress
    private float totalSteps = 8; // Total generation steps
    private float currentStep = 0;

    private void Awake()
    {
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
        // Debug.Log("Starting terrain generation...");
        currentStep = 0;

        // Initialize the terrain grid with plains as default
        terrainGrid = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        yield return StartCoroutine(InitializeGridCoroutine());

        // Convert some plains to grasslands
        yield return StartCoroutine(ConvertPlainsToGrasslandsCoroutine());

        // Determine the sea border (0=North, 1=East, 2=South, 3=West)
        int seaBorder = Random.Range(0, 4);

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

        // Generate forests
        yield return StartCoroutine(GenerateForestsCoroutine());

        // Generate swamps
        yield return StartCoroutine(GenerateSwampsCoroutine());

        // Generate wastelands
        yield return StartCoroutine(GenerateWastelandsCoroutine());

        // Apply shore terrain to areas adjacent to shallow water
        yield return StartCoroutine(ApplyShoresCoroutine());

        // Debug.Log("Terrain generation complete!");
        OnGenerationProgress?.Invoke(1.0f, "Complete");

        // Return the completed terrain grid
        onComplete?.Invoke(terrainGrid);
    }

    public IEnumerator InstantiateHexesCoroutine(Action<Dictionary<Vector2, GameObject>> onComplete)
    {
        hexes = new Dictionary<Vector2, GameObject>(board.GetHeight() * board.GetWidth());
        
        // Clear existing hexes more efficiently
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        int totalHexes = board.GetHeight() * board.GetWidth();
        int hexesProcessed = 0;
        var positions = new Vector3[hexesPerBatch];
        var hexObjects = new GameObject[hexesPerBatch];
        int batchIndex = 0;

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                GameObject hexGo = hexPool.Get();
                hexGo.name = $"{row},{col}";
                positions[batchIndex] = GetPosition(row, col);
                hexObjects[batchIndex] = hexGo;
                batchIndex++;

                if (batchIndex >= hexesPerBatch)
                {
                    // Batch process positions and components
                    for (int i = 0; i < batchIndex; i++)
                    {
                        hexObjects[i].transform.position = positions[i];
                        var hex = hexObjects[i].GetComponent<Hex>();
                        var terrainType = terrainGrid[row - (batchIndex - 1 - i) / board.GetWidth(), col - (batchIndex - 1 - i) % board.GetWidth()];
                        
                        hexObjects[i].GetComponent<SpriteRenderer>().color = terrainColors[terrainType];
                        hex.terrainType = terrainType;
                        hex.terrainTexture.sprite = terrainTextures[terrainType];
                        hex.v2 = new Vector2Int(row - (batchIndex - 1 - i) / board.GetWidth(), col - (batchIndex - 1 - i) % board.GetWidth());
                        hex.RefreshHoverText();
                        hexes[new Vector2(row - (batchIndex - 1 - i) / board.GetWidth(), col - (batchIndex - 1 - i) % board.GetWidth())] = hexObjects[i];
                    }

                    hexesProcessed += batchIndex;
                    float progress = (float)hexesProcessed / totalHexes;
                    OnGenerationProgress?.Invoke(progress, "Configuring Board");
                    yield return null;
                    batchIndex = 0;
                }
            }
        }

        // Process remaining hexes
        if (batchIndex > 0)
        {
            for (int i = 0; i < batchIndex; i++)
            {
                hexObjects[i].transform.position = positions[i];
                var hex = hexObjects[i].GetComponent<Hex>();
                var terrainType = terrainGrid[board.GetHeight() - 1 - (batchIndex - 1 - i) / board.GetWidth(), board.GetWidth() - 1 - (batchIndex - 1 - i) % board.GetWidth()];
                
                hexObjects[i].GetComponent<SpriteRenderer>().color = terrainColors[terrainType];
                hex.terrainType = terrainType;
                hex.terrainTexture.sprite = terrainTextures[terrainType];
                hex.v2 = new Vector2Int(board.GetHeight() - 1 - (batchIndex - 1 - i) / board.GetWidth(), board.GetWidth() - 1 - (batchIndex - 1 - i) % board.GetWidth());
                hex.RefreshHoverText();
                hexes[new Vector2(board.GetHeight() - 1 - (batchIndex - 1 - i) / board.GetWidth(), board.GetWidth() - 1 - (batchIndex - 1 - i) % board.GetWidth())] = hexObjects[i];
            }
        }

        OnGenerationProgress?.Invoke(1.0f, "Game ready!");
        onComplete?.Invoke(hexes);
    }

    private Vector3 GetPosition(int row, int col)
    {
        float hexWidth = board.hexSize.x * board.hexPrefab.transform.localScale.x;
        float hexHeight = board.hexSize.y * board.hexPrefab.transform.localScale.y;

        float xOffset = col * hexWidth;  // Default spacing
        float yOffset = row * hexHeight * 0.5f;  // Proper vertical stacking

        // Shift odd rows to interlock with the previous row
        if (row % 2 == 1)
        {
            xOffset += hexWidth * 0.5f; // Half of a hex board.GetWidth()
        }

        return new Vector3(xOffset, yOffset, 0);
    }

    private IEnumerator InitializeGridCoroutine()
    {
        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsProcessed = 0;

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                terrainGrid[row, col] = TerrainEnum.plains;

                cellsProcessed++;

                // Yield every batch to distribute the work
                if (cellsProcessed % cellsPerBatch == 0)
                {
                    float progress = (float)cellsProcessed / totalCells;
                    OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Configuring Terrain");
                    yield return null;
                }
            }
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Terrain ready");
    }

    private IEnumerator ConvertPlainsToGrasslandsCoroutine()
    {
        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatch = 500;
        int cellsProcessed = 0;

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainGrid[row, col] == TerrainEnum.plains && Random.value < board.grasslandsProbability)
                {
                    terrainGrid[row, col] = TerrainEnum.grasslands;
                }

                cellsProcessed++;

                if (cellsProcessed % cellsPerBatch == 0)
                {
                    float progress = (float)cellsProcessed / totalCells;
                    OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Grasslands");
                    yield return null;
                }
            }
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Grasslands Created");
    }

    private IEnumerator GenerateWaterWithNaturalCoastlineCoroutine(int seaBorder)
    {
        int waterWidth = Mathf.FloorToInt(board.GetWidth() * board.waterPercentage);
        int waterHeight = Mathf.FloorToInt(board.GetHeight() * board.waterPercentage);
        int coastlineDepth = Mathf.FloorToInt(Mathf.Min(board.GetWidth(), board.GetHeight()) * board.coastDepth);

        // Create a noise-based coastline
        int seedX = Random.Range(0, 10000);
        int seedY = Random.Range(0, 10000);

        // Process the main coastline generation in batches
        yield return StartCoroutine(GenerateCoastlineCoroutine(seaBorder, waterWidth, waterHeight, coastlineDepth, seedX, seedY));

        // Create water "fingers" extending inland
        yield return StartCoroutine(GenerateWaterFingersCoroutine(seaBorder, waterWidth, waterHeight, coastlineDepth));

        // Create islands
        yield return StartCoroutine(GenerateIslandsCoroutine(seaBorder, waterWidth, waterHeight));

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Water Generation Complete");
    }

    private IEnumerator GenerateCoastlineCoroutine(int seaBorder, int waterWidth, int waterHeight, int coastlineDepth, int seedX, int seedY)
    {
        int totalCells;
        int cellsPerBatch = 300;
        int cellsProcessed = 0;

        switch (seaBorder)
        {
            case 0: // North border
                totalCells = waterHeight * board.GetWidth();

                for (int row = 0; row < waterHeight; row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        // Generate perlin noise for the coastline
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        // Calculate boundary for deep water based on proportion
                        int deepWaterBoundary = Mathf.FloorToInt(waterHeight * board.deepWaterProportion);

                        if (row < deepWaterBoundary - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (row < waterHeight + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress * 0.4f / totalSteps + currentStep / totalSteps, "Creating Coastline");
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
                        // Generate perlin noise for the coastline
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        // Calculate boundary for deep water based on proportion
                        int deepWaterStart = board.GetWidth() - Mathf.FloorToInt(waterWidth * board.deepWaterProportion);

                        if (col > deepWaterStart + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (col > board.GetWidth() - waterWidth - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress * 0.4f / totalSteps + currentStep / totalSteps, "Creating Coastline");
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
                        // Generate perlin noise for the coastline
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        // Calculate boundary for deep water based on proportion
                        int deepWaterStart = board.GetHeight() - Mathf.FloorToInt(waterHeight * board.deepWaterProportion);

                        if (row > deepWaterStart + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (row > board.GetHeight() - waterHeight - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress * 0.4f / totalSteps + currentStep / totalSteps, "Creating Coastline");
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
                        // Generate perlin noise for the coastline
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * coastlineDepth);

                        // Calculate boundary for deep water based on proportion
                        int deepWaterBoundary = Mathf.FloorToInt(waterWidth * board.deepWaterProportion);

                        if (col < deepWaterBoundary - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.deepWater;
                        }
                        else if (col < waterWidth + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.shallowWater;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress * 0.4f / totalSteps + currentStep / totalSteps, "Creating Coastline");
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
                                // Taper the finger as it extends
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

                        // Yield occasionally to distribute work
                        if (row % 3 == 0)
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke((0.4f + overallProgress * 0.3f) / totalSteps + currentStep / totalSteps, "Creating Water Features");
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

                        // Yield occasionally to distribute work
                        if ((board.GetWidth() - waterWidth - 1 - col) % 3 == 0)
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke((0.4f + overallProgress * 0.3f) / totalSteps + currentStep / totalSteps, "Creating Water Features");
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

                        // Yield occasionally to distribute work
                        if ((board.GetHeight() - waterHeight - 1 - row) % 3 == 0)
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke((0.4f + overallProgress * 0.3f) / totalSteps + currentStep / totalSteps, "Creating Water Features");
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

                        // Yield occasionally to distribute work
                        if ((col - waterWidth) % 3 == 0)
                        {
                            float fingerProgress = (float)cellsProcessed / cellsPerFinger;
                            float overallProgress = (fingersProcessed + fingerProgress) / totalFingers;
                            OnGenerationProgress?.Invoke((0.4f + overallProgress * 0.3f) / totalSteps + currentStep / totalSteps, "Creating Water Features");
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
        int numIslands = Random.Range(board.minIslands, board.maxIslands);
        int totalIslands = numIslands;
        int islandsProcessed = 0;

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

            // Only place island if in water (preferably deep water)
            if (terrainGrid[islandRow, islandCol] == TerrainEnum.deepWater ||
                terrainGrid[islandRow, islandCol] == TerrainEnum.shallowWater)
            {
                // Island size
                int islandSize = Random.Range(3, 8);
                Queue<Vector2Int> islandCells = new Queue<Vector2Int>();
                islandCells.Enqueue(new Vector2Int(islandRow, islandCol));

                // Mark center as shore
                terrainGrid[islandRow, islandCol] = TerrainEnum.shore;

                int cellsProcessed = 1;
                int iterations = 0;
                int maxIterations = islandSize * 4; // Prevent infinite loops

                while (islandCells.Count > 0 && cellsProcessed < islandSize && iterations < maxIterations)
                {
                    Vector2Int current = islandCells.Dequeue();

                    // Get neighbors
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

                    // Yield occasionally to distribute work
                    if (iterations % 5 == 0)
                    {
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
            OnGenerationProgress?.Invoke((0.7f + islandProgress * 0.3f) / totalSteps + currentStep / totalSteps, "Creating Islands");

            // Yield after each island
            yield return null;
        }
    }

    private IEnumerator GenerateMountainChainsCoroutine()
    {
        int maxChainLength = Mathf.FloorToInt(Mathf.Max(board.GetWidth(), board.GetHeight()) * 0.4f * board.mountainChainLengthMultiplier);
        int totalChains = board.mountainChainCount;
        int chainsProcessed = 0;

        for (int i = 0; i < board.mountainChainCount; i++)
        {
            // Choose a random starting point for each mountain chain
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

            if (!validStart) continue; // Skip this chain if can't find a valid start

            // Determine a directional preference (x or y dominant)
            bool xDominant = Random.value > 0.5f;

            // Choose chain length with some variety
            int chainLength = Random.Range(maxChainLength / 2, maxChainLength);

            // Create the chain
            Vector2Int current = new Vector2Int(startRow, startCol);
            terrainGrid[current.x, current.y] = TerrainEnum.mountains;

            yield return null; // Yield after setting up each chain

            for (int j = 0; j < chainLength; j++)
            {
                // Get current position's neighbors
                Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                // Choose a direction with preference based on x or y dominance
                Vector2Int nextDir;
                if (xDominant)
                {
                    // Prefer east or west
                    nextDir = neighbors[Random.value > 0.7f ? Random.Range(0, 6) : Random.Range(1, 4) % 6];
                }
                else
                {
                    // Prefer north or south
                    nextDir = neighbors[Random.value > 0.7f ? Random.Range(0, 6) : Random.Range(0, 3) * 2];
                }

                // Calculate next position
                Vector2Int next = current + nextDir;

                // Check if next position is valid and not water
                if (next.x >= 0 && next.x < board.GetHeight() && next.y >= 0 && next.y < board.GetWidth() &&
                    terrainGrid[next.x, next.y] != TerrainEnum.deepWater &&
                    terrainGrid[next.x, next.y] != TerrainEnum.shallowWater)
                {
                    current = next;
                    terrainGrid[current.x, current.y] = TerrainEnum.mountains;
                }

                // Yield occasionally during mountain chain generation
                if (j % 10 == 0)
                {
                    float chainProgress = (float)j / chainLength;
                    float overallProgress = (chainsProcessed + chainProgress) / totalChains;
                    OnGenerationProgress?.Invoke(overallProgress / totalSteps + currentStep / totalSteps, "Creating Mountains");
                    yield return null;
                }
            }

            chainsProcessed++;
        }

        // Add hills around mountains
        yield return StartCoroutine(AddHillsAroundMountainsCoroutine());

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Mountains Created");
    }

    private IEnumerator AddHillsAroundMountainsCoroutine()
    {
        // Create a copy of the current terrain
        TerrainEnum[,] terrainCopy = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        Array.Copy(terrainGrid, terrainCopy, terrainGrid.Length);

        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatch = 300;
        int cellsProcessed = 0;

        // Add hills around mountains
        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                if (terrainCopy[row, col] == TerrainEnum.mountains)
                {
                    // Get neighbors
                    Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                    foreach (Vector2Int dir in neighbors)
                    {
                        int newRow = row + dir.x;
                        int newCol = col + dir.y;

                        // Check bounds
                        if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                        {
                            // Make surrounding plains into hills (80% chance)
                            if (terrainGrid[newRow, newCol] == TerrainEnum.plains && Random.value < 0.8f)
                            {
                                terrainGrid[newRow, newCol] = TerrainEnum.hills;
                            }
                        }
                    }
                }

                cellsProcessed++;
                if (cellsProcessed % cellsPerBatch == 0)
                {
                    float progress = (float)cellsProcessed / totalCells;
                    OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Hills");
                    yield return null;
                }
            }
        }
    }

    private IEnumerator GenerateForestsCoroutine()
    {
        // Generate one major forest
        for (int i = 0; i < board.majorForestCount; i++)
        {
            int forestSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.08f); // 8% of the map
            yield return StartCoroutine(GenerateForestPatchCoroutine(forestSize, "Major Forest"));
        }

        // Generate smaller forests
        for (int i = 0; i < board.minorForestCount; i++)
        {
            int forestSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.02f); // 2% of the map
            yield return StartCoroutine(GenerateForestPatchCoroutine(forestSize, "Minor Forest"));
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Forests Created");
    }

    private IEnumerator GenerateForestPatchCoroutine(int forestSize, string forestType)
    {
        int startRow, startCol;
        bool validStart = false;
        int attempts = 0;

        // Find a suitable starting location (not water or mountains)
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

        if (!validStart) yield break; // Skip this forest if can't find a valid start

        // Use a modified flood fill to create the forest
        Queue<Vector2Int> tilesToProcess = new Queue<Vector2Int>();
        tilesToProcess.Enqueue(new Vector2Int(startRow, startCol));
        terrainGrid[startRow, startCol] = TerrainEnum.forest;

        int tilesProcessed = 1;
        int iterations = 0;
        int maxIterations = forestSize * 3; // Prevent infinite loops

        while (tilesToProcess.Count > 0 && tilesProcessed < forestSize && iterations < maxIterations)
        {
            Vector2Int current = tilesToProcess.Dequeue();

            // Get current tile's neighbors
            Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            // Shuffle neighbors
            for (int i = 0; i < neighbors.Length; i++)
            {
                int j = Random.Range(i, neighbors.Length);
                (neighbors[i], neighbors[j]) = (neighbors[j], neighbors[i]);
            }

            foreach (Vector2Int dir in neighbors)
            {
                int newRow = current.x + dir.x;
                int newCol = current.y + dir.y;

                // Check bounds
                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                {
                    // Only convert plains to forest
                    if (terrainGrid[newRow, newCol] == TerrainEnum.plains)
                    {
                        // Chance to expand reduces as we get further from center
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

            // Yield occasionally during forest generation
            if (iterations % 20 == 0)
            {
                float forestProgress = (float)tilesProcessed / forestSize;
                OnGenerationProgress?.Invoke(forestProgress / totalSteps + currentStep / totalSteps, $"Creating {forestType}");
                yield return null;
            }
        }
    }

    private IEnumerator GenerateSwampsCoroutine()
    {
        int totalSwamps = board.swampCount;
        int swampsProcessed = 0;

        for (int i = 0; i < board.swampCount; i++)
        {
            int startRow, startCol;
            bool validStart = false;
            int attempts = 0;
            bool nearWater = false;

            // Try to place swamps near water when possible
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

                // Check if a water tile is adjacent
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

            // If after 50 attempts we can't find an ideal location, just use a valid one even if not near water
            if (!validStart)
            {
                // Try once more for any valid location
                attempts = 0;
                do
                {
                    startRow = Random.Range(0, board.GetHeight());
                    startCol = Random.Range(0, board.GetWidth());
                    validStart = terrainGrid[startRow, startCol] == TerrainEnum.plains;
                    attempts++;
                } while (!validStart && attempts < 20);

                if (!validStart) continue; // Skip this swamp if we can't find a valid position at all
            }

            // Create the swamp patch
            terrainGrid[startRow, startCol] = TerrainEnum.swamp;

            // Get neighbors
            Vector2Int[] swampNeighbors = (startRow % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            // Shuffle neighbors
            for (int n = 0; n < swampNeighbors.Length; n++)
            {
                int j = Random.Range(n, swampNeighbors.Length);
                (swampNeighbors[n], swampNeighbors[j]) = (swampNeighbors[j], swampNeighbors[n]);
            }

            // Add a few swamp tiles around the initial one
            int swampTiles = 1;
            foreach (Vector2Int dir in swampNeighbors)
            {
                if (swampTiles >= board.swampSize)
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
            OnGenerationProgress?.Invoke(swampProgress / totalSteps + currentStep / totalSteps, "Creating Swamps");

            yield return null; // Yield after each swamp
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Swamps Created");
    }

    private IEnumerator GenerateWastelandsCoroutine()
    {
        // Generate one major wasteland area
        for (int i = 0; i < board.majorWastelandCount; i++)
        {
            int wastelandSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.07f); // Major wasteland covers ~7% of map
            yield return StartCoroutine(GenerateWastelandPatchCoroutine(wastelandSize, true, "Major Wasteland"));
        }

        // Generate smaller wasteland patches
        for (int i = 0; i < board.minorWastelandCount; i++)
        {
            int wastelandSize = Mathf.FloorToInt(board.GetWidth() * board.GetHeight() * 0.02f); // Minor wasteland ~2% of map
            yield return StartCoroutine(GenerateWastelandPatchCoroutine(wastelandSize, false, "Minor Wasteland"));
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Wastelands Created");
    }

    private IEnumerator GenerateWastelandPatchCoroutine(int wastelandSize, bool isMajor, string wastelandType)
    {
        int startRow, startCol;
        bool validStart = false;
        int attempts = 0;

        // Select location based on wasteland type
        if (isMajor)
        {
            // Find a suitable starting location for major wasteland (central area)
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
            // Find a suitable starting location for minor wasteland away from major wastelands
            do
            {
                startRow = Random.Range(0, board.GetHeight());
                startCol = Random.Range(0, board.GetWidth());

                validStart = (terrainGrid[startRow, startCol] == TerrainEnum.plains ||
                             terrainGrid[startRow, startCol] == TerrainEnum.grasslands);

                // If we found a valid terrain type, check if there are any wastelands nearby
                if (validStart)
                {
                    // Check if there's any wasteland nearby already
                    foreach (Vector2Int neighbor in GetNeighborsInRadius(startRow, startCol, 4))
                    {
                        if (terrainGrid[neighbor.x, neighbor.y] == TerrainEnum.wastelands)
                        {
                            // Location is invalid if there's a wasteland nearby
                            validStart = false;
                            break;
                        }
                    }
                }

                attempts++;
            } while (!validStart && attempts < 50);
        }

        if (!validStart) yield break; // Skip this wasteland if can't find a valid start

        // Set initial cell to wasteland
        terrainGrid[startRow, startCol] = TerrainEnum.wastelands;

        // Create irregular wasteland patch
        Queue<Vector2Int> expansionCells = new Queue<Vector2Int>();
        expansionCells.Enqueue(new Vector2Int(startRow, startCol));

        int cellsProcessed = 1;
        float noiseScale = 0.15f;
        int noiseSeed = Random.Range(0, 10000);
        int iterations = 0;
        int maxIterations = wastelandSize * 3; // Prevent infinite loops

        while (expansionCells.Count > 0 && cellsProcessed < wastelandSize && iterations < maxIterations)
        {
            Vector2Int current = expansionCells.Dequeue();

            // Get neighbors
            Vector2Int[] neighbors = (current.x % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

            // Shuffle neighbors
            for (int n = 0; n < neighbors.Length; n++)
            {
                int j = Random.Range(n, neighbors.Length);
                (neighbors[n], neighbors[j]) = (neighbors[j], neighbors[n]);
            }

            foreach (Vector2Int dir in neighbors)
            {
                int newRow = current.x + dir.x;
                int newCol = current.y + dir.y;

                // Check bounds
                if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth())
                {
                    // Only convert plains/grasslands to wasteland
                    if ((terrainGrid[newRow, newCol] == TerrainEnum.plains ||
                        terrainGrid[newRow, newCol] == TerrainEnum.grasslands) &&
                        terrainGrid[newRow, newCol] != TerrainEnum.wastelands)
                    {
                        // Use noise to create more irregular, realistic borders
                        float noise = Mathf.PerlinNoise((newCol + noiseSeed) * noiseScale, (newRow + noiseSeed) * noiseScale);
                        float expandChance = isMajor ?
                            (0.8f - (float)cellsProcessed / wastelandSize * 0.5f) : // Major wastelands spread wider
                            0.7f; // Minor wastelands are more compact

                        expandChance *= (0.5f + noise * 0.5f); // Use noise to vary expansion chance

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

            // Yield occasionally during wasteland generation
            if (iterations % 20 == 0)
            {
                float wastelandProgress = (float)cellsProcessed / wastelandSize;
                OnGenerationProgress?.Invoke(wastelandProgress / totalSteps + currentStep / totalSteps, $"Creating {wastelandType}");
                yield return null;
            }
        }
    }

    private IEnumerator ApplyShoresCoroutine()
    {
        // Create a copy of the terrain to avoid immediate changes affecting the process
        TerrainEnum[,] terrainCopy = new TerrainEnum[board.GetHeight(), board.GetWidth()];
        Array.Copy(terrainGrid, terrainCopy, terrainGrid.Length);

        int totalCells = board.GetHeight() * board.GetWidth();
        int cellsPerBatch = 300;
        int cellsProcessed = 0;

        for (int row = 0; row < board.GetHeight(); row++)
        {
            for (int col = 0; col < board.GetWidth(); col++)
            {
                // Find land tiles adjacent to shallow water
                if (terrainCopy[row, col] != TerrainEnum.deepWater &&
                    terrainCopy[row, col] != TerrainEnum.shallowWater)
                {
                    bool adjacentToWater = false;

                    // Get neighbors
                    Vector2Int[] neighbors = (row % 2 == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                    foreach (Vector2Int dir in neighbors)
                    {
                        int newRow = row + dir.x;
                        int newCol = col + dir.y;

                        if (newRow >= 0 && newRow < board.GetHeight() && newCol >= 0 && newCol < board.GetWidth() &&
                            terrainCopy[newRow, newCol] == TerrainEnum.shallowWater)
                        {
                            adjacentToWater = true;
                            break;
                        }
                    }

                    // If adjacent to water and not mountains, hills or swamp, high chance to become shore
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
                if (cellsProcessed % cellsPerBatch == 0)
                {
                    float progress = (float)cellsProcessed / totalCells;
                    OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Shores");
                    yield return null;
                }
            }
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Shores Created");
    }

    private IEnumerator GenerateDesertCoroutine(int desertBorder)
    {
        int desertWidth = Mathf.FloorToInt(board.GetWidth() * board.desertPercentage);
        int desertHeight = Mathf.FloorToInt(board.GetHeight() * board.desertPercentage);
        int desertDepth = Mathf.FloorToInt(Mathf.Min(board.GetWidth(), board.GetHeight()) * 0.12f);

        // Create noise for the desert border
        int seedX = Random.Range(0, 10000);
        int seedY = Random.Range(0, 10000);

        int cellsPerBatch = 300;
        int totalCells;
        int cellsProcessed = 0;

        switch (desertBorder)
        {
            case 0: // North border
                totalCells = desertHeight * board.GetWidth();

                for (int row = 0; row < desertHeight; row++)
                {
                    for (int col = 0; col < board.GetWidth(); col++)
                    {
                        // Skip if water or mountains already present
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        // Generate perlin noise for the desert edge
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (row < desertHeight - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Desert");
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
                        // Skip if water or mountains already present
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        // Generate perlin noise for the desert edge
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (col > board.GetWidth() - desertWidth + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Desert");
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
                        // Skip if water or mountains already present
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        // Generate perlin noise for the desert edge
                        float noise = Mathf.PerlinNoise((col + seedX) * 0.1f, seedY * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (row > board.GetHeight() - desertHeight + noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Desert");
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
                        // Skip if water or mountains already present
                        if (terrainGrid[row, col] == TerrainEnum.deepWater ||
                            terrainGrid[row, col] == TerrainEnum.shallowWater ||
                            terrainGrid[row, col] == TerrainEnum.mountains ||
                            terrainGrid[row, col] == TerrainEnum.hills)
                            continue;

                        // Generate perlin noise for the desert edge
                        float noise = Mathf.PerlinNoise(seedX * 0.1f, (row + seedY) * 0.1f);
                        int noiseOffset = Mathf.FloorToInt(noise * desertDepth);

                        if (col < desertWidth - noiseOffset)
                        {
                            terrainGrid[row, col] = TerrainEnum.desert;
                        }

                        cellsProcessed++;
                        if (cellsProcessed % cellsPerBatch == 0)
                        {
                            float progress = (float)cellsProcessed / totalCells;
                            OnGenerationProgress?.Invoke(progress / totalSteps + currentStep / totalSteps, "Creating Desert");
                            yield return null;
                        }
                    }
                }
                break;
        }

        currentStep++;
        OnGenerationProgress?.Invoke(currentStep / totalSteps, "Desert Created");
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

            // Skip the center point
            if (current.distance > 0)
            {
                result.Add(current.position);
            }

            // If we've reached the radius limit, don't add more cells to the queue
            if (current.distance >= radius)
                continue;

            // Add neighbors
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
