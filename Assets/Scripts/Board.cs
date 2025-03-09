using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoardGenerator), typeof(NationSpawner))]
public class Board : MonoBehaviour
{
    [Header("Board Size")]
    public int width = 25;
    public int height = 75;

    [Header("Hex Configuration")]
    public GameObject hexPrefab;
    public Vector2 hexSize;

    [Header("Generation progress")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;

    // Chance parameters
    [Header("Generation Parameters")]
    [Range(1, 5)] public int minIslands = 1;
    [Range(1, 5)] public int maxIslands = 5;
    [Range(0.1f, 0.3f)] public float waterPercentage = 0.2f;
    [Range(0.1f, 1f)] public float deepWaterProportion = 0.7f;
    [Range(0.1f, 0.3f)] public float desertPercentage = 0.1f;
    [Range(0.1f, 0.9f)] public float grasslandsProbability = 0.5f; // Chance of plains becoming grasslands
    [Range(0.1f, 0.5f)] public float coastDepth = 0.15f;

    [Header("Mountain Parameters")]
    [Range(1, 5)] public int mountainChainCount = 3;
    [Range(0.5f, 2.0f)] public float mountainChainLengthMultiplier = 1.2f;

    [Header("Forest Parameters")]
    [Range(1, 3)] public int majorForestCount = 1;
    [Range(2, 6)] public int minorForestCount = 4;

    [Header("Swamp Parameters")]
    [Range(1, 3)] public int swampCount = 1;
    [Range(4, 7)] public int swampSize = 5;

    [Header("Wastelands Parameters")]
    [Range(1, 2)] public int majorWastelandCount = 1;
    [Range(0, 3)] public int minorWastelandCount = 2;

    [Header("Selection")]
    public Vector2 selectedHex = Vector2.one * -1;
    public Character selectedCharacter = null;

    [Header("Start button")]
    public Button startButton;

    [Header("Debug")]
    public bool redraw = false;
    public bool regenerate = false;

    [Header("On the fly")]
    // Colors object
    public Colors colors;
    // Textures object
    public Textures textures;
    // Board Generator
    public BoardGenerator boardGenerator;
    // Nation Spawner
    public NationSpawner nationSpawner;

    // Array to store the terrain types
    public TerrainEnum[,] terrainGrid;
    // Dictionary to store all generated hexes
    public Dictionary<Vector2, Hex> hexes;

    // Direction vectors for hex neighbors (flat-top)
    public readonly Vector2Int[] evenRowNeighbors = new[] {
        new Vector2Int(1, 0),   // Northeast
        new Vector2Int(-1, 0),   // Southeast
        new Vector2Int(-2, 0),   // South
        new Vector2Int(-1, -1),  // Southwest
        new Vector2Int(1, -1),  // NorthWest
        new Vector2Int(+2, 0)   // Top
    };

    public readonly Vector2Int[] oddRowNeighbors = new[] {
        new Vector2Int(1,1),   // Northeast
        new Vector2Int(-1, 1),   // Southeast
        new Vector2Int(-2, 0),   // South
        new Vector2Int(-1, 0),  // Southwest
        new Vector2Int(1, 0),  // NorthWest
        new Vector2Int(2, 0)   // Top
    };

    private bool initialized = false;

    void Start()
    {
        startButton.interactable = false;
        colors = FindFirstObjectByType<Colors>();
        textures = FindFirstObjectByType<Textures>();
        boardGenerator = GetComponent<BoardGenerator>();
        boardGenerator.Initialize(this);
        nationSpawner = GetComponent<NationSpawner>();
        nationSpawner.Initialize(this);

        // Subscribe to generation progress events
        boardGenerator.OnGenerationProgress += UpdateGenerationProgress;

        StartCoroutine(DrawCoroutine());
    }

    private void Update()
    {
        if (redraw)
        {
            redraw = false;
            StartCoroutine(DrawCoroutine());
        }
    }

    public void ForceDraw()
    {
        StartCoroutine(DrawCoroutine(true));
    }

    private IEnumerator DrawCoroutine(bool forced = false)
    {
        if (!initialized || forced)
        {
            // Generate terrain first
            if (terrainGrid == null || regenerate)  yield return StartCoroutine(boardGenerator.GenerateTerrainCoroutine(OnTerrainGenerated));

            // Then instantiate hexes
            yield return StartCoroutine(boardGenerator.InstantiateHexesCoroutine(OnHexesInstantiated));
        }
    }

    private void OnTerrainGenerated(TerrainEnum[,] terrainGrid)
    {
        // Debug.Log("Terrain generation completed");
        this.terrainGrid = terrainGrid;
    }

    private void OnHexesInstantiated(Dictionary<Vector2, GameObject> hexesGameObjects)
    {
        // Debug.Log($"Hex instantiation completed. {hexesGameObjects.Count} hexes created");
        hexes = hexesGameObjects
        .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetComponent<Hex>()
        );

        nationSpawner.Spawn();
        initialized = true;
        startButton.interactable = true;
        hexes.Values.ToList().ForEach(x => { x.GetComponent<OnHoverTile>().enabled = true; x.GetComponent<OnClickTile>().enabled = true; });
    }

    public void SelectHex(Vector2 selection)
    {
        try
        {
            Leader player = FindFirstObjectByType<Game>().player;
            if (!hexes[selection].IsHidden() && (hexes[selection].HasArmyOfLeader(player) || hexes[selection].HasCharacterOfLeader(player)))
            {
                selectedHex = selection;
                hexes[selection].Select();

                if (hexes[selection].characters.Count < 1)
                {
                    selectedCharacter = null;
                    FindFirstObjectByType<SelectedCharacterIcon>().Hide();
                    return;
                }

                if (hexes[selection].characters.Count == 1)
                {
                    selectedCharacter = hexes[selection].characters[0];
                    FindFirstObjectByType<SelectedCharacterIcon>().Refresh(selectedCharacter);
                }
                else
                {
                    // Using LINQ to find next character with single line lambda expression
                    selectedCharacter = hexes[selection].characters
                        .SkipWhile(c => c == selectedCharacter)
                        .Skip(1)
                        .FirstOrDefault() ?? hexes[selection].characters.FirstOrDefault();

                    FindFirstObjectByType<SelectedCharacterIcon>().Refresh(selectedCharacter);
                }

                FindFirstObjectByType<ActionsManager>().Refresh(selectedCharacter);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            selectedHex = Vector2.one * -1;
        }
    }

    public void UnselectHex()
    {
        if (selectedHex != Vector2.one * -1) hexes[selectedHex].Unselect();
        selectedHex = Vector2.one * -1;

        // Start an inline coroutine
        StartCoroutine(DelayedUnselectHexActions());
    }
    IEnumerator DelayedUnselectHexActions()
    {
        // Wait for 2 seconds
        yield return new WaitForSeconds(1f);

        // Execute these actions after the delay
        FindFirstObjectByType<ActionsManager>().Hide();
        FindFirstObjectByType<SelectedCharacterIcon>().Hide();
        selectedCharacter = null;
    }

    public List<Hex> GetHexes()
    {
        return hexes.Values.ToList();
    }

    public void Move(Character character, Vector2 targetHexCoordinates)
    {
        if (!character) return;
        if (targetHexCoordinates == Vector2.one * -1) return;
        
        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        List<Vector2> path = pathRenderer.FindPath(character.hex.v2, targetHexCoordinates, character.army != null);

        StartCoroutine(MoveCoroutine(character, path));
    }

    IEnumerator MoveCoroutine(Character character, List<Vector2> path)
    {
        FindFirstObjectByType<ActionsManager>().Hide();
        for (int i=0;i<path.Count;i++)
        {
            if (i + 1 < path.Count)
            {
                Hex previousHex = hexes[path[i]];

                if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
                if (character.army != null && previousHex.armies.Contains(character.army)) previousHex.armies.Remove(character.army);
                previousHex.RedrawCharacters();
                previousHex.RedrawArmies();

                Hex newHex = hexes[path[i + 1]];

                if (!newHex.characters.Contains(character)) newHex.characters.Add(character);
                if (character.army != null && !newHex.armies.Contains(character.army)) newHex.armies.Add(character.army);
                character.hex = newHex;

                newHex.RedrawCharacters();
                newHex.RedrawArmies();

                character.hex.RevealArea();

                character.hasMovedThisTurn = true;
                newHex.LookAt();

                character.GetOwner().visibleHexes.Remove(previousHex);
                character.GetOwner().visibleHexes.Add(newHex);

                yield return new WaitForSeconds(0.5f);
            }
        }
        yield return new WaitForSeconds(0.5f);
        FindFirstObjectByType<ActionsManager>().Refresh(character);
    }
    private void UpdateGenerationProgress(float progress, string stage)
    {
        // Update the progress bar
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        // Update the status text
        if (statusText != null)
        {
            statusText.text = $"{stage} - {progress * 100:F0}%";
        }

        // Log to console
        // Debug.Log($"Board Generation: {stage} - {progress * 100:F0}%");
    }
}
