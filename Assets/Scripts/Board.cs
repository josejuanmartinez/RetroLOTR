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
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = FindFirstObjectByType<TextsEN>().start;
        hexes.Values.ToList().ForEach(x => { x.GetComponent<OnHoverTile>().enabled = true; x.GetComponent<OnClickTile>().enabled = true; });
    }

    public void SelectHex(Hex hex)
    {
        SelectHex(hex.v2);
    }

    public void SelectHex(Vector2 selection)
    {
        try
        {
            Leader player = FindFirstObjectByType<Game>().player;
            if (!hexes[selection].IsHidden() && (hexes[selection].HasArmyOfLeader(player) || hexes[selection].HasCharacterOfLeader(player)))
            {
                // If different hex, I unselect character
                if (selection != selectedHex)
                {
                    UnselectHex();
                    selectedHex = selection;
                    hexes[selection].Select();
                }

                // If same hex, I loop through characters
                List<Character> myCharacters = hexes[selection].characters.FindAll(x => x.GetOwner() == player.GetOwner());

                if (myCharacters.Count < 1)
                {
                    selectedCharacter = null;
                    FindFirstObjectByType<SelectedCharacterIcon>().Hide();
                    return;
                }

                if (myCharacters.Count == 1)
                {
                    UnselectHex();
                    selectedHex = selection;
                    hexes[selection].Select();

                    selectedCharacter = myCharacters[0];
                    FindFirstObjectByType<SelectedCharacterIcon>().Refresh(selectedCharacter);
                }
                else
                {
                    var currentIndex = myCharacters.IndexOf(selectedCharacter);
                    if(currentIndex == -1)
                    {
                        selectedCharacter = myCharacters[0];
                    } else
                    {
                        var nextIndex = (currentIndex + 1) % myCharacters.Count;
                        selectedCharacter = myCharacters[nextIndex];
                    }

                    FindFirstObjectByType<SelectedCharacterIcon>().Refresh(selectedCharacter);
                }

                FindFirstObjectByType<ActionsManager>().Refresh(selectedCharacter);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            selectedHex = Vector2.one * -1;
            selectedCharacter = null;
            FindFirstObjectByType<SelectedCharacterIcon>().Hide();
            FindFirstObjectByType<ActionsManager>().Hide();
            return;
        }
    }

    public void UnselectHex()
    {
        if (selectedHex != Vector2.one * -1) hexes[selectedHex].Unselect();
        selectedHex = Vector2.one * -1;

        // Execute these actions after the delay
        FindFirstObjectByType<ActionsManager>().Hide();
        FindFirstObjectByType<SelectedCharacterIcon>().Hide();
        selectedCharacter = null;
    }

    public void UnselectCharacter()
    {

    }

    public List<Hex> GetHexes()
    {
        return hexes.Values.ToList();
    }

    public void Move(Character character, Vector2 targetHexCoordinates)
    {
        if (!character) return;
        if (targetHexCoordinates == Vector2.one * -1) return;
        if (character.moved >= character.GetMaxMovement()) return;

        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        List<Vector2> path = pathRenderer.FindPath(character.hex.v2, targetHexCoordinates, character);

        StartCoroutine(MoveCoroutine(character, path));
    }

    IEnumerator MoveCoroutine(Character character, List<Vector2> path)
    {
        SelectedCharacterIcon selected = null;
        ActionsManager actionsManager = null;
        Hex currentHex = character.hex; // Store initial hex

        try
        {
            actionsManager = FindFirstObjectByType<ActionsManager>();
            actionsManager.Hide();
            selected = FindFirstObjectByType<SelectedCharacterIcon>();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing movement: {e.Message}\n{e.StackTrace}");
            HandleMovementFailure(character, currentHex, path, -1);
            yield break;
        }

        // Main movement loop
        for (int i = 0; i < path.Count; i++)
        {
            if (i + 1 < path.Count)
            {
                try
                {
                    // Store previous hex to revert to in case of failure
                    Hex previousHex = hexes[path[i]];
                    currentHex = previousHex;

                    if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
                    if (character.IsArmyCommander())
                    {
                        Debug.Log($"{character} is an army commander when moving from {path[i]}. Army is {character.GetArmy()}");
                        if (previousHex.armies.Contains(character.GetArmy())) { 
                            Debug.Log($"Current hex {path[i]} contained the army. Removing...");
                            previousHex.armies.Remove(character.GetArmy());
                        }
                    }
                    previousHex.RedrawCharacters();
                    previousHex.RedrawArmies();

                    Hex newHex = hexes[path[i + 1]];
                    if (!newHex.characters.Contains(character)) newHex.characters.Add(character);
                    if (character.IsArmyCommander())
                    {
                        Debug.Log($"{character} is an army commander when moving to {path[i + 1]}. Army is {character.GetArmy()}");
                        if (!newHex.armies.Contains(character.GetArmy()))
                        {
                            Debug.Log($"Nex hex {path[i + 1]} did not contained the army. Adding...");
                            newHex.armies.Add(character.GetArmy());
                        }
                    }
                    character.hex = newHex;
                    currentHex = newHex; // Update current hex

                    newHex.RedrawCharacters();
                    newHex.RedrawArmies();
                    character.hex.RevealArea();
                    character.hasMovedThisTurn = true;
                    newHex.LookAt();
                    UnselectHex();
                    SelectHex(path[i + 1]);

                    if(!character.GetOwner().LeaderSeesHex(previousHex)) character.GetOwner().visibleHexes.Remove(previousHex);
                    character.GetOwner().visibleHexes.Add(newHex);
                    character.moved += newHex.GetTerrainCost(character);

                    bool wasWater = previousHex.terrainType == TerrainEnum.shallowWater || previousHex.terrainType == TerrainEnum.deepWater;
                    bool isWater = newHex.terrainType == TerrainEnum.shallowWater || newHex.terrainType == TerrainEnum.deepWater;

                    if (!wasWater && isWater)
                    {
                        character.moved = character.GetMaxMovement();
                        break;
                    }
                    if (wasWater && !isWater)
                    {
                        character.moved = character.GetMaxMovement();
                        break;
                    }

                    selected.RefreshMovementLeft(character);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during character movement at step {i}: {e.Message}\n{e.StackTrace}");
                    HandleMovementFailure(character, currentHex, path, i);
                    yield break;
                }

                // Yield outside the try block
                yield return new WaitForSeconds(0.5f);
            }
        }

        try
        {
            selected.RefreshMovementLeft(character);
            actionsManager.Refresh(character);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error finalizing movement: {e.Message}\n{e.StackTrace}");
            HandleMovementFailure(character, currentHex, path, path.Count - 1);
        }

        // Final delay outside try block
        yield return new WaitForSeconds(0.5f);
    }

    // Helper method to handle movement failure and ensure consistent state
    private void HandleMovementFailure(Character character, Hex currentHex, List<Vector2> path = null, int currentIndex = -1)
    {
        if (currentHex != null && character != null)
        {
            // Only check hexes in the path rather than the entire map
            if (path != null && currentIndex >= 0)
            {
                // Only need to check hexes that we've already passed through or started to enter
                for (int i = 0; i <= currentIndex + 1 && i < path.Count; i++)
                {
                    if (hexes.TryGetValue(path[i], out Hex pathHex) && pathHex != currentHex)
                    {
                        if (pathHex.characters.Contains(character))
                        {
                            pathHex.characters.Remove(character);
                            pathHex.RedrawCharacters();
                        }

                        if (character.IsArmyCommander() && pathHex.armies.Contains(character.GetArmy()))
                        {
                            pathHex.armies.Remove(character.GetArmy());
                            pathHex.RedrawArmies();
                        }
                    }
                }
            }

            // Make sure character is in current hex
            if (!currentHex.characters.Contains(character)) currentHex.characters.Add(character);

            // Make sure army is in current hex
            if (character.IsArmyCommander() && !currentHex.armies.Contains(character.GetArmy())) currentHex.armies.Add(character.GetArmy());

            // Set character's hex reference properly
            character.hex = currentHex;

            // Redraw
            currentHex.RedrawCharacters();
            currentHex.RedrawArmies();
            currentHex.LookAt();
            SelectHex(currentHex.v2);
        }

        // Always refresh the actions manager and selected icon if available
        var actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager != null)
        {
            actionsManager.Refresh(character);
        }

        var selected = FindFirstObjectByType<SelectedCharacterIcon>();
        if (selected != null)
        {
            selected.RefreshMovementLeft(character);
        }
    }
    private void UpdateGenerationProgress(float progress, string stage)
    {
        // Update the progress bar
        if (progressBar != null) progressBar.value = progress;

        // Update the status text
        if (statusText != null) statusText.text = $"{stage} - {progress * 100:F0}%";
    }

    public Hex GetHex(Vector2 v2)
    {
        if (hexes == null) return null;
        hexes.TryGetValue(v2, out Hex hex);
        return hex;
    }
}
