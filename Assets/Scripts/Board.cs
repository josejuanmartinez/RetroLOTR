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
    [SerializeField] int width = 25;
    [SerializeField] int height = 75;

    [Header("Hex Configuration")]
    public GameObject hexPrefab;
    public Vector2 hexSize;

    [Header("Generation progress")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;
    public bool drawMark = false;

    [Header("Selection")]
    public Vector2Int selectedHex = Vector2Int.one * -1;
    public Character selectedCharacter = null;
    public event Action<Character, Character> SelectedCharacterChanged;

    [Header("Movement over board")]
    public bool moving = false;
    [SerializeField] private SpriteRenderer characterMoverImage;
    [SerializeField] private SpriteRenderer characterMoverBackground;
    [SerializeField] private SpriteRenderer freeArmyMoverImage;
    [SerializeField] private SpriteRenderer darkServantsMoverImage;
    [SerializeField] private SpriteRenderer neutralMoverImage;
    private SpriteRenderer portMoverImage;

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
    public Dictionary<Vector2Int, Hex> hexes;
    public List<Hex> hexesWithCharacters;
    public List<Hex> hexesWithPCs;
    public List<Hex> hexesWithArtifacts;

    // Direction vectors for hex neighbors (flat-top)
    public readonly Vector2Int[] evenRowNeighbors = new[] {
        new Vector2Int(1, 0),   // Northeast
        new Vector2Int(0, 1),   // East
        new Vector2Int(-1, 0),   // Southeast
        new Vector2Int(-1, -1),  // Southwest
        new Vector2Int(0, -1),  // West
        new Vector2Int(1, -1)   // Northwest
    };

    public readonly Vector2Int[] oddRowNeighbors = new[] {
        new Vector2Int(1,1),   // Northeast
        new Vector2Int(0, 1),   // East
        new Vector2Int(-1, 1),   // Southeast
        new Vector2Int(-1, 0),  // Southwest
        new Vector2Int(0, -1),  // West
        new Vector2Int(1, 0)   // Northwest
    };

    private bool initialized = false;

    void Start()
    {
        if (startButton == null)
        {
            Debug.LogError("Start button is not assigned!");
            return;
        }
        startButton.interactable = false;

        colors = FindFirstObjectByType<Colors>();
        if (colors == null)
        {
            Debug.LogError("Colors component not found!");
            return;
        }

        textures = FindFirstObjectByType<Textures>();
        if (textures == null)
        {
            Debug.LogError("Textures component not found!");
            return;
        }

        boardGenerator = GetComponent<BoardGenerator>();
        if (boardGenerator == null)
        {
            Debug.LogError("BoardGenerator component not found!");
            return;
        }
        boardGenerator.Initialize(this);

        nationSpawner = GetComponent<NationSpawner>();
        if (nationSpawner == null)
        {
            Debug.LogError("NationSpawner component not found!");
            return;
        }
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

    public int GetWidth()
    {
        return Math.Min(width, Game.MAX_BOARD_WIDTH);
    }

    public int GetHeight()
    {
        return Math.Min(height, Game.MAX_BOARD_HEIGHT);
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
            if (terrainGrid == null || regenerate)
            {
                yield return StartCoroutine(boardGenerator.GenerateTerrainCoroutine(OnTerrainGenerated));
            }

            // Then instantiate hexes
            yield return StartCoroutine(boardGenerator.InstantiateHexesCoroutine(OnHexesInstantiated));
        }
        // In case the video has not finished yet and we have, we return the download priority to normal
        Application.backgroundLoadingPriority = ThreadPriority.Normal;
    }

    private void OnTerrainGenerated(TerrainEnum[,] terrainGrid)
    {
        this.terrainGrid = terrainGrid;
        // Update the terrain hex cache in NationSpawner
        nationSpawner.BuildTerrainHexCache(terrainGrid);
    }

    private void OnHexesInstantiated(Dictionary<Vector2Int, Hex> spawnedHexes)
    {
        if (spawnedHexes == null)
        {
            Debug.LogError("Hexes instantiation failed!");
            return;
        }

        hexes = spawnedHexes;

        if (hexes == null || hexes.Count == 0)
        {
            Debug.LogError("Failed to create hex dictionary!");
            return;
        }

        nationSpawner.Spawn();
        initialized = true;
        startButton.interactable = true;
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = "◊ Start as this leader ◊";
        
        var hexList = GetHexes();
        if (hexList != null)
        {
            hexList.ForEach(x => {
                if (x != null)
                {
                    var hoverTile = x.GetComponent<OnHoverTile>();
                    var clickTile = x.GetComponent<OnClickTile>();
                    if (hoverTile != null) hoverTile.enabled = true;
                    if (clickTile != null) clickTile.enabled = true;
                }
            });
        }
        
        StartCoroutine(SpawnArtifacts());
    }

    IEnumerator SpawnArtifacts()
    {
        // Get all hexes
        List<Hex> hexes = GetHexes();

        TextAsset jsonFile = Resources.Load<TextAsset>("Artifacts");
        List<Artifact> hiddenArtifacts = JsonUtility.FromJson<ArtifactCollection>(jsonFile.text).artifacts;

        // Shuffle the hexes to randomize artifact placement
        List<Hex> shuffledHexes = hexes.OrderBy(hex => UnityEngine.Random.value).ToList();

        // Ensure we don't try to place more artifacts than we have hexes
        int artifactsToPlace = Mathf.Min(hiddenArtifacts.Count, shuffledHexes.Count);

        // Place artifacts in hexes (one per hex)
        for (int i = 0; i < artifactsToPlace; i++)
        {
            Hex targetHex = shuffledHexes[i];
            Artifact artifact = hiddenArtifacts[i];

            // Add the artifact to the hex's hiddenArtifacts list
            targetHex.hiddenArtifacts.Add(artifact);

            // Debug.Log($"Artifact {artifact.artifactName} placed at {targetHex.v2}");

            // Optional: Set artifact position to hex position
            // artifact.transform.position = targetHex.transform.position;

            // Yield to distribute over frames if needed
            if (i % 10 == 0) yield return null;
        }
    }

    public void StartGame()
    {
        RefreshRelevantHexes();
    }

    public void RefreshRelevantHexes()
    {
        hexesWithArtifacts = GetHexes().FindAll(x => x.hiddenArtifacts.Count > 0);
        hexesWithPCs = GetHexes().FindAll(x => x.GetPC() != null);
        hexesWithCharacters = GetHexes().FindAll(x => x.characters.Count > 0);
    }

    public void ClearAllScouting()
    {
        if (hexes == null) return;
        foreach (var hex in hexes.Values)
        {
            hex?.ClearScouting();
        }
    }

    public void SelectCharacter(Character character, bool lookAt = true, float duration = 1.0f, float delay = 0.0f)
    {
        SelectHex(character.hex, lookAt, duration, delay, character);
    }

    public void SelectHex(Hex hex, bool lookAt = true, float duration = 1.0f, float delay = 0.0f, Character characterToSelect = null)
    {
        SelectHex(hex.v2, lookAt, duration, delay, characterToSelect);
    }

    public void SelectHex(Vector2Int selection, bool lookAt = true, float duration = 1.0f, float delay = 0.0f, Character characterToSelect = null)
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
                    hexes[selection].Select(lookAt, duration, delay);
                }

                // If same hex, I loop through characters
                List<Character> myCharacters = hexes[selection].characters.FindAll(x => x.GetOwner() == player.GetOwner());

                if (myCharacters.Count < 1)
                {
                    SetSelectedCharacter(null);
                    FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Hide();
                    return;
                }

                if (myCharacters.Count == 1)
                {
                    UnselectHex();
                    selectedHex = selection;
                    hexes[selection].Select(lookAt, duration, delay);

                    SetSelectedCharacter(myCharacters[0]);
                    FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(selectedCharacter);
                }
                else
                {
                    var toSelectIndex = 0;
                    if (characterToSelect != null)
                    {
                        toSelectIndex = myCharacters.IndexOf(characterToSelect);
                    } else if (selectedCharacter != null)
                    {
                        toSelectIndex = (myCharacters.IndexOf(selectedCharacter) + 1) % myCharacters.Count;
                    }
                    
                    SetSelectedCharacter(myCharacters[toSelectIndex]);
                    
                    FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(selectedCharacter);
                }

                FindFirstObjectByType<Layout>().GetActionsManager().Refresh(selectedCharacter);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            selectedHex = Vector2Int.one * -1;
            SetSelectedCharacter(null);
            FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Hide();
            FindFirstObjectByType<Layout>().GetActionsManager().Hide();
            return;
        }
        if (hexes != null && hexes.TryGetValue(selection, out var selected))
        {
            Music.Instance?.UpdateForHex(selected);
        }
        if(selectedCharacter) FindFirstObjectByType<ActionsManager>().Refresh(selectedCharacter);
    }

    public void UnselectHex()
    {
        if (selectedHex != Vector2Int.one * -1) hexes[selectedHex].Unselect();
        selectedHex = Vector2Int.one * -1;

        // Execute these actions after the delay
        FindFirstObjectByType<Layout>().GetActionsManager().Hide();
        FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Hide();
        SetSelectedCharacter(null);
        Music.Instance?.UpdateForHex(null);
    }

    public void UnselectCharacter()
    {
        SetSelectedCharacter(null);
    }

    public List<Hex> GetHexes()
    {
        return hexes.Values.ToList();
    }

    public void Move(Character character, Vector2Int targetHexCoordinates)
    {
        if (!character) return;
        if (targetHexCoordinates == Vector2.one * -1) return;
        if (character.moved >= character.GetMaxMovement()) return;

        moving = true;
        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        List<Vector2Int> path = pathRenderer.FindPath(character.hex.v2, targetHexCoordinates, character);

        StartCoroutine(MoveCoroutine(character, path));
    }

    private IEnumerator AnimateSpriteBetween(
    SpriteRenderer fromSR,
    SpriteRenderer toSR,
    SpriteRenderer moverSR,
    SpriteRenderer fromBgSR,
    SpriteRenderer toBgSR,
    SpriteRenderer moverBgSR,
    Vector3 start,
    Vector3 end,
    Vector3 startBg,
    Vector3 endBg,
    float duration,
    Camera followCam = null,
    AnimationCurve ease = null)
    {
        // Copy look
        moverSR.sprite = fromSR.sprite;
        moverSR.color = fromSR.color;
        moverSR.flipX = fromSR.flipX;
        moverSR.flipY = fromSR.flipY;
        moverSR.sharedMaterial = fromSR.sharedMaterial;
        CopyPropertyBlock(fromSR, moverSR);
        if (moverBgSR != null && fromBgSR != null)
        {
            moverBgSR.sprite = fromBgSR.sprite;
            moverBgSR.color = fromBgSR.color;
            moverBgSR.flipX = fromBgSR.flipX;
            moverBgSR.flipY = fromBgSR.flipY;
            moverBgSR.sharedMaterial = fromBgSR.sharedMaterial;
            CopyPropertyBlock(fromBgSR, moverBgSR);
        }

        // Make sure mover appears above board
        moverSR.sortingLayerID = fromSR.sortingLayerID;
        moverSR.sortingOrder = fromSR.sortingOrder + 100;
        if (moverBgSR != null && fromBgSR != null)
        {
            moverBgSR.sortingLayerID = fromBgSR.sortingLayerID;
            moverBgSR.sortingOrder = fromBgSR.sortingOrder + 99;
        }

        moverSR.transform.localScale = GetLocalScaleForWorldScale(moverSR.transform, GetDesiredWorldScale(fromSR, moverSR));
        moverSR.transform.rotation = fromSR.transform.rotation;
        if (moverBgSR != null && fromBgSR != null)
        {
            moverBgSR.transform.localScale = GetLocalScaleForWorldScale(moverBgSR.transform, GetDesiredWorldScale(fromBgSR, moverBgSR));
            moverBgSR.transform.rotation = fromBgSR.transform.rotation;
        }

        // Hide the static icons during tween so you don't see the destination early
        bool fromPrevEnabled = fromSR != null && fromSR.enabled;
        bool toPrevEnabled = toSR != null && toSR.enabled;
        bool fromBgPrevEnabled = fromBgSR != null && fromBgSR.enabled;
        bool toBgPrevEnabled = toBgSR != null && toBgSR.enabled;
        if (fromSR != null) fromSR.enabled = false;
        if (toSR != null) toSR.enabled = false;
        if (fromBgSR != null) fromBgSR.enabled = false;
        if (toBgSR != null) toBgSR.enabled = false;

        moverSR.gameObject.SetActive(true);
        moverSR.transform.position = start;
        if (moverBgSR != null)
        {
            if (fromBgSR != null)
            {
                moverBgSR.gameObject.SetActive(true);
                moverBgSR.transform.position = startBg;
            }
            else
            {
                moverBgSR.gameObject.SetActive(false);
            }
        }

        // Camera follow setup
        Vector3 camOffset = Vector3.zero;
        if (followCam != null)
            camOffset = followCam.transform.position - start;

        float elapsed = 0f;
        ease ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = ease.Evaluate(t);

            Vector3 pos = Vector3.Lerp(start, end, e);
            moverSR.transform.position = pos;
            if (moverBgSR != null && fromBgSR != null)
            {
                Vector3 bgPos = Vector3.Lerp(startBg, endBg, e);
                moverBgSR.transform.position = bgPos;
            }

            if (followCam != null)
            {
                // Keep same offset and preserve camera z (for 2D orthographic)
                Vector3 camPos = pos + camOffset;
                camPos.z = followCam.transform.position.z;
                followCam.transform.position = camPos;
            }

            yield return null;
        }

        moverSR.transform.position = end;
        moverSR.gameObject.SetActive(false);
        if (moverBgSR != null && fromBgSR != null)
        {
            moverBgSR.transform.position = endBg;
            moverBgSR.gameObject.SetActive(false);
        }

        // Restore icon visibility (destination will be redrawn after MoveCharacterOneHex anyway)
        if (fromSR != null) fromSR.enabled = fromPrevEnabled;
        if (toSR != null) toSR.enabled = toPrevEnabled;
        if (fromBgSR != null) fromBgSR.enabled = fromBgPrevEnabled;
        if (toBgSR != null) toBgSR.enabled = toBgPrevEnabled;
    }

    private IEnumerator AnimateSpriteBetweenDual(
    SpriteRenderer fromCharSR,
    SpriteRenderer toCharSR,
    SpriteRenderer moverCharSR,
    SpriteRenderer fromCharBgSR,
    SpriteRenderer toCharBgSR,
    SpriteRenderer moverCharBgSR,
    SpriteRenderer fromArmySR,
    SpriteRenderer toArmySR,
    SpriteRenderer moverArmySR,
    SpriteRenderer fromPortSR,
    SpriteRenderer toPortSR,
    SpriteRenderer moverPortSR,
    Vector3 charStart,
    Vector3 charEnd,
    Vector3 charBgStart,
    Vector3 charBgEnd,
    Vector3 armyStart,
    Vector3 armyEnd,
    Vector3 portStart,
    Vector3 portEnd,
    bool hideStaticPort,
    float duration,
    Camera followCam = null,
    AnimationCurve ease = null)
    {
        bool useChar = moverCharSR != null && fromCharSR != null;
        bool useArmy = moverArmySR != null && fromArmySR != null;
        bool usePort = moverPortSR != null && fromPortSR != null;

        if (useChar)
        {
            moverCharSR.sprite = fromCharSR.sprite;
            moverCharSR.color = fromCharSR.color;
            moverCharSR.flipX = fromCharSR.flipX;
            moverCharSR.flipY = fromCharSR.flipY;
            moverCharSR.sharedMaterial = fromCharSR.sharedMaterial;
            CopyPropertyBlock(fromCharSR, moverCharSR);
            if (moverCharBgSR != null && fromCharBgSR != null)
            {
                moverCharBgSR.sprite = fromCharBgSR.sprite;
                moverCharBgSR.color = fromCharBgSR.color;
                moverCharBgSR.flipX = fromCharBgSR.flipX;
                moverCharBgSR.flipY = fromCharBgSR.flipY;
                moverCharBgSR.sharedMaterial = fromCharBgSR.sharedMaterial;
                CopyPropertyBlock(fromCharBgSR, moverCharBgSR);
            }

            moverCharSR.sortingLayerID = fromCharSR.sortingLayerID;
            moverCharSR.sortingOrder = fromCharSR.sortingOrder + 100;
            if (moverCharBgSR != null && fromCharBgSR != null)
            {
                moverCharBgSR.sortingLayerID = fromCharBgSR.sortingLayerID;
                moverCharBgSR.sortingOrder = fromCharBgSR.sortingOrder + 99;
            }

            moverCharSR.transform.localScale = GetLocalScaleForWorldScale(moverCharSR.transform, GetDesiredWorldScale(fromCharSR, moverCharSR));
            moverCharSR.transform.rotation = fromCharSR.transform.rotation;
            if (moverCharBgSR != null && fromCharBgSR != null)
            {
                moverCharBgSR.transform.localScale = GetLocalScaleForWorldScale(moverCharBgSR.transform, GetDesiredWorldScale(fromCharBgSR, moverCharBgSR));
                moverCharBgSR.transform.rotation = fromCharBgSR.transform.rotation;
            }
        }

        if (useArmy)
        {
            moverArmySR.sprite = fromArmySR.sprite;
            moverArmySR.color = fromArmySR.color;
            moverArmySR.flipX = fromArmySR.flipX;
            moverArmySR.flipY = fromArmySR.flipY;
            moverArmySR.sharedMaterial = fromArmySR.sharedMaterial;
            CopyPropertyBlock(fromArmySR, moverArmySR);

            moverArmySR.sortingLayerID = fromArmySR.sortingLayerID;
            moverArmySR.sortingOrder = fromArmySR.sortingOrder + 100;
            moverArmySR.transform.localScale = GetLocalScaleForWorldScale(moverArmySR.transform, GetDesiredWorldScale(fromArmySR, moverArmySR));
            moverArmySR.transform.rotation = fromArmySR.transform.rotation;
        }

        if (usePort)
        {
            moverPortSR.sprite = fromPortSR.sprite;
            moverPortSR.color = fromPortSR.color;
            moverPortSR.flipX = fromPortSR.flipX;
            moverPortSR.flipY = fromPortSR.flipY;
            moverPortSR.sharedMaterial = fromPortSR.sharedMaterial;
            CopyPropertyBlock(fromPortSR, moverPortSR);

            moverPortSR.sortingLayerID = fromPortSR.sortingLayerID;
            moverPortSR.sortingOrder = fromPortSR.sortingOrder + 100;
            moverPortSR.transform.localScale = GetLocalScaleForWorldScale(moverPortSR.transform, GetDesiredWorldScale(fromPortSR, moverPortSR));
            moverPortSR.transform.rotation = fromPortSR.transform.rotation;
        }

        bool fromCharPrevEnabled = fromCharSR != null && fromCharSR.enabled;
        bool toCharPrevEnabled = toCharSR != null && toCharSR.enabled;
        bool fromCharBgPrevEnabled = fromCharBgSR != null && fromCharBgSR.enabled;
        bool toCharBgPrevEnabled = toCharBgSR != null && toCharBgSR.enabled;
        if (fromCharSR != null) fromCharSR.enabled = false;
        if (toCharSR != null) toCharSR.enabled = false;
        if (fromCharBgSR != null) fromCharBgSR.enabled = false;
        if (toCharBgSR != null) toCharBgSR.enabled = false;

        bool fromArmyPrevEnabled = fromArmySR != null && fromArmySR.enabled;
        bool toArmyPrevEnabled = toArmySR != null && toArmySR.enabled;
        if (fromArmySR != null) fromArmySR.enabled = false;
        if (toArmySR != null) toArmySR.enabled = false;

        bool fromPortPrevEnabled = fromPortSR != null && fromPortSR.enabled;
        bool toPortPrevEnabled = toPortSR != null && toPortSR.enabled;
        if (hideStaticPort)
        {
            if (fromPortSR != null) fromPortSR.enabled = false;
            if (toPortSR != null) toPortSR.enabled = false;
        }

        if (useChar)
        {
            moverCharSR.gameObject.SetActive(true);
            moverCharSR.transform.position = charStart;
            if (moverCharBgSR != null)
            {
                if (fromCharBgSR != null)
                {
                    moverCharBgSR.gameObject.SetActive(true);
                    moverCharBgSR.transform.position = charBgStart;
                }
                else
                {
                    moverCharBgSR.gameObject.SetActive(false);
                }
            }
        }
        else if (moverCharSR != null)
        {
            moverCharSR.gameObject.SetActive(false);
        }

        if (useArmy)
        {
            moverArmySR.gameObject.SetActive(true);
            moverArmySR.transform.position = armyStart;
        }
        else if (moverArmySR != null)
        {
            moverArmySR.gameObject.SetActive(false);
        }

        if (usePort)
        {
            moverPortSR.gameObject.SetActive(true);
            moverPortSR.transform.position = portStart;
        }
        else if (moverPortSR != null)
        {
            moverPortSR.gameObject.SetActive(false);
        }

        Vector3 camOffset = Vector3.zero;
        if (followCam != null)
        {
            Vector3 camAnchor = useChar ? charStart : armyStart;
            camOffset = followCam.transform.position - camAnchor;
        }

        float elapsed = 0f;
        ease ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = ease.Evaluate(t);

            if (useChar)
            {
                Vector3 pos = Vector3.Lerp(charStart, charEnd, e);
                moverCharSR.transform.position = pos;
                if (moverCharBgSR != null && fromCharBgSR != null)
                {
                    Vector3 bgPos = Vector3.Lerp(charBgStart, charBgEnd, e);
                    moverCharBgSR.transform.position = bgPos;
                }
            }

            if (useArmy)
            {
                Vector3 pos = Vector3.Lerp(armyStart, armyEnd, e);
                moverArmySR.transform.position = pos;
            }

            if (usePort)
            {
                Vector3 pos = Vector3.Lerp(portStart, portEnd, e);
                moverPortSR.transform.position = pos;
            }

            if (followCam != null)
            {
                Vector3 anchor = useChar ? moverCharSR.transform.position : moverArmySR.transform.position;
                Vector3 camPos = anchor + camOffset;
                camPos.z = followCam.transform.position.z;
                followCam.transform.position = camPos;
            }

            yield return null;
        }

        if (useChar)
        {
            moverCharSR.transform.position = charEnd;
            moverCharSR.gameObject.SetActive(false);
            if (moverCharBgSR != null && fromCharBgSR != null)
            {
                moverCharBgSR.transform.position = charBgEnd;
                moverCharBgSR.gameObject.SetActive(false);
            }
        }

        if (useArmy)
        {
            moverArmySR.transform.position = armyEnd;
            moverArmySR.gameObject.SetActive(false);
        }

        if (usePort)
        {
            moverPortSR.transform.position = portEnd;
            moverPortSR.gameObject.SetActive(false);
        }

        if (fromCharSR != null) fromCharSR.enabled = fromCharPrevEnabled;
        if (toCharSR != null) toCharSR.enabled = toCharPrevEnabled;
        if (fromCharBgSR != null) fromCharBgSR.enabled = fromCharBgPrevEnabled;
        if (toCharBgSR != null) toCharBgSR.enabled = toCharBgPrevEnabled;

        if (fromArmySR != null) fromArmySR.enabled = fromArmyPrevEnabled;
        if (toArmySR != null) toArmySR.enabled = toArmyPrevEnabled;

        if (hideStaticPort)
        {
            if (fromPortSR != null) fromPortSR.enabled = fromPortPrevEnabled;
            if (toPortSR != null) toPortSR.enabled = toPortPrevEnabled;
        }
    }


    // Centralize how you get a hex's world spot.
    // If your Hex already has a center/world pos property, use it here.
    private Vector3 GetHexWorldPosition(Hex h)
    {
        // Common options—pick the one that matches your project:
        // return h.WorldPosition;
        // return h.CenterWorld;
        return h.transform.position;
    }

    private static Vector3 GetLocalScaleForWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        if (target == null) return desiredWorldScale;
        Transform parent = target.parent;
        if (parent == null) return desiredWorldScale;

        Vector3 parentWorldScale = parent.lossyScale;
        return new Vector3(
            SafeDivide(desiredWorldScale.x, parentWorldScale.x),
            SafeDivide(desiredWorldScale.y, parentWorldScale.y),
            SafeDivide(desiredWorldScale.z, parentWorldScale.z)
        );
    }

    private static Vector3 GetDesiredWorldScale(SpriteRenderer source, SpriteRenderer mover)
    {
        if (source == null || mover == null || source.sprite == null || mover.sprite == null)
        {
            return source != null ? source.transform.lossyScale : Vector3.one;
        }

        Vector3 sourceSize = source.bounds.size;
        Vector3 moverSpriteSize = mover.sprite.bounds.size;
        return new Vector3(
            SafeDivide(sourceSize.x, moverSpriteSize.x),
            SafeDivide(sourceSize.y, moverSpriteSize.y),
            1f
        );
    }

    private static void CopyPropertyBlock(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null) return;
        var block = new MaterialPropertyBlock();
        source.GetPropertyBlock(block);
        target.SetPropertyBlock(block);
    }

    private static SpriteRenderer GetCharacterBackground(SpriteRenderer characterSprite)
    {
        if (characterSprite == null) return null;
        Transform parent = characterSprite.transform.parent;
        if (parent == null) return null;
        return parent.GetComponent<SpriteRenderer>();
    }

    private SpriteRenderer EnsureMoverBackground(SpriteRenderer mover)
    {
        if (mover == null) return null;
        if (characterMoverBackground != null) return characterMoverBackground;

        Transform parent = mover.transform.parent;
        if (parent != null)
        {
            Transform existing = parent.Find("characterBg");
            if (existing != null)
            {
                characterMoverBackground = existing.GetComponent<SpriteRenderer>();
                if (characterMoverBackground != null) return characterMoverBackground;
            }
        }

        GameObject bg = new("characterBg");
        bg.transform.SetParent(mover.transform.parent, false);
        characterMoverBackground = bg.AddComponent<SpriteRenderer>();
        characterMoverBackground.gameObject.SetActive(false);
        return characterMoverBackground;
    }

    private SpriteRenderer EnsurePortMover(SpriteRenderer mover)
    {
        if (mover == null) return null;
        if (portMoverImage != null) return portMoverImage;

        Transform parent = mover.transform.parent;
        if (parent != null)
        {
            Transform existing = parent.Find("portMover");
            if (existing != null)
            {
                portMoverImage = existing.GetComponent<SpriteRenderer>();
                if (portMoverImage != null) return portMoverImage;
            }
        }

        GameObject portMover = new("portMover");
        portMover.transform.SetParent(mover.transform.parent, false);
        portMoverImage = portMover.AddComponent<SpriteRenderer>();
        portMoverImage.gameObject.SetActive(false);
        return portMoverImage;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Abs(denominator) < 0.0001f ? numerator : numerator / denominator;
    }


    IEnumerator MoveCoroutine(Character character, List<Vector2Int> path)
    {
        FindFirstObjectByType<HexPathRenderer>().HidePath();
        SelectedCharacterIcon selected;
        ActionsManager actionsManager;
        Hex currentHex = character.hex; // Store initial hex
        bool showPlayerUi = ShouldShowPlayerUi(character);

        try
        {
            actionsManager = FindFirstObjectByType<Layout>().GetActionsManager();
            actionsManager.Hide();
            selected = FindFirstObjectByType<Layout>().GetSelectedCharacterIcon();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing movement: {e.Message}\n{e.StackTrace}");
            HandleMovementFailure(character, currentHex, path, -1);
            yield break;
        }


        SpriteRenderer characterMoverSR = characterMoverImage;
        SpriteRenderer armyMoverSR = null;
        if (character.IsArmyCommander())
        {
            switch (character.alignment)
            {
                case AlignmentEnum.freePeople:
                    armyMoverSR = freeArmyMoverImage; break;
                case AlignmentEnum.darkServants:
                    armyMoverSR = darkServantsMoverImage; break;
                case AlignmentEnum.neutral:
                    armyMoverSR = neutralMoverImage; break;
            }
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            Hex previousHex = hexes[path[i]];
            Hex newHex = hexes[path[i + 1]];
            currentHex = previousHex;

            // Get the visible sprites on each hex
            SpriteRenderer fromSR = previousHex.GetCharacterSpriteRendererOnHex(character);
            SpriteRenderer toSR = newHex.GetCharacterSpriteRendererOnHex(character);
            SpriteRenderer fromBg = GetCharacterBackground(fromSR);
            SpriteRenderer toBg = GetCharacterBackground(toSR);
            SpriteRenderer moverBg = characterMoverSR == characterMoverImage ? EnsureMoverBackground(characterMoverSR) : null;

            SpriteRenderer fromArmySR = previousHex.GetArmySpriteRendererOnHex(character);
            SpriteRenderer toArmySR = newHex.GetArmySpriteRendererOnHex(character);
            SpriteRenderer fromPortSR = previousHex.GetPortSpriteRenderer();
            SpriteRenderer toPortSR = newHex.GetPortSpriteRenderer();

            // Use the sprite transforms' world positions (NOT RectTransform)
            Vector3 startPos = (fromSR != null) ? fromSR.transform.position : previousHex.transform.position;
            Vector3 endPos = (toSR != null) ? toSR.transform.position : newHex.transform.position;
            Vector3 startBgPos = fromBg != null ? fromBg.transform.position : startPos;
            Vector3 endBgPos = toBg != null ? toBg.transform.position : endPos;
            Vector3 startArmyPos = (fromArmySR != null) ? fromArmySR.transform.position : previousHex.transform.position;
            Vector3 endArmyPos = (toArmySR != null) ? toArmySR.transform.position : newHex.transform.position;
            Vector3 startPortPos = (fromPortSR != null) ? fromPortSR.transform.position : previousHex.transform.position;
            Vector3 endPortPos = (toPortSR != null) ? toPortSR.transform.position : newHex.transform.position;

            IEnumerator tween = null;
            bool canAnimate = false;

            try
            {
                if (character.IsArmyCommander())
                {
                    bool hasAny = (characterMoverSR != null && fromSR != null) || (armyMoverSR != null && fromArmySR != null);
                    if (hasAny)
                    {
                        bool hasWarships = character.GetArmy() != null && character.GetArmy().ws > 0;
                        bool fromWarshipPort = previousHex.ShouldShowWarshipPort();
                        bool toWarshipPort = newHex.ShouldShowWarshipPort();
                        bool hasPcPort = previousHex.HasPcPort() || newHex.HasPcPort();
                        bool usePort = hasWarships && fromWarshipPort && toWarshipPort && !hasPcPort;
                        SpriteRenderer portMover = usePort ? EnsurePortMover(characterMoverSR) : null;

                        tween = AnimateSpriteBetweenDual(
                            fromSR,
                            toSR,
                            characterMoverSR,
                            fromBg,
                            toBg,
                            moverBg,
                            fromArmySR,
                            toArmySR,
                            armyMoverSR,
                            fromPortSR,
                            toPortSR,
                            portMover,
                            startPos,
                            endPos,
                            startBgPos,
                            endBgPos,
                            startArmyPos,
                            endArmyPos,
                            startPortPos,
                            endPortPos,
                            usePort,
                            0.35f);
                        canAnimate = true;
                    }
                }
                else
                {
                    canAnimate = characterMoverSR != null && fromSR != null;
                    if (canAnimate)
                    {
                        tween = AnimateSpriteBetween(fromSR, toSR, characterMoverSR, fromBg, toBg, moverBg, startPos, endPos, startBgPos, endBgPos, 0.35f);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error preparing animation at step {i}: {e.Message}\n{e.StackTrace}");
                HandleMovementFailure(character, currentHex, path, i);
                canAnimate = false;
            }

            if (canAnimate) yield return tween;

            try
            {
                // Commit logic AFTER the tween so Redraw snaps to the new hex cleanly
                bool lastHex = i == path.Count -1;
                MoveCharacterOneHex(character, previousHex, newHex, lastHex, lastHex);
                currentHex = newHex;
                if (showPlayerUi) selected.RefreshMovementLeft(character);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during character movement at step {i}: {e.Message}\n{e.StackTrace}");
                HandleMovementFailure(character, currentHex, path, i);
                yield break;
            }

            // optional: tiny pacing pause
            // yield return new WaitForSeconds(0.02f);
        }

        try
        {
            if (showPlayerUi)
            {
                selected.RefreshMovementLeft(character);
                actionsManager.Refresh(character);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error finalizing movement: {e.Message}\n{e.StackTrace}");
            HandleMovementFailure(character, currentHex, path, path.Count - 1);
        }

        // Final delay outside try block
        yield return new WaitForSeconds(0.5f);
        moving = false;
        if (showPlayerUi)
        {
            SelectCharacter(character);
            actionsManager.Refresh(character);
        }
    }

    public void MoveCharacterOneHex(Character character, Hex previousHex, Hex newHex, bool finishMovement = false, bool lookAt = true) {
        int movedBefore = character.moved;
        bool wasWater = previousHex != null && previousHex.IsWaterTerrain();
        bool isWater = newHex != null && newHex.IsWaterTerrain();
        Game g = FindFirstObjectByType<Game>();
        try
        {
            HandleWarshipAnchoring(character, previousHex, newHex, wasWater, isWater);
            if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
            if (character.IsArmyCommander())
            {
                if (previousHex.armies.Contains(character.GetArmy())) previousHex.armies.Remove(character.GetArmy());
            }
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();

            if (!newHex.characters.Contains(character)) newHex.characters.Add(character);
            if (character.IsArmyCommander())
            {
                if (!newHex.armies.Contains(character.GetArmy())) newHex.armies.Add(character.GetArmy());
            }
            character.hex = newHex;
            if (g != null)
            {
                if (character.GetOwner() == g.player)
                {
                    Sounds.Instance?.PlayMovement(previousHex, newHex);
                }
                else if (g.player != null && g.player.visibleHexes.Contains(newHex) && newHex.IsHexSeen())
                {
                    Sounds.Instance?.PlayMovement(previousHex, newHex);
                }
            }

            newHex.RedrawCharacters();
            newHex.RedrawArmies();
            if (character.GetOwner() == FindFirstObjectByType<Game>().player)
            {
                if (lookAt) newHex.LookAt();
                character.hex.RevealArea(1, lookAt);                
                if(finishMovement) {
                    UnselectHex();
                    SelectHex(newHex, lookAt);
                }
            }            
            else if (g != null && g.player != null && character.doubledBy.Contains(g.player))
            {
                newHex.RevealArea(1, false, g.player);
                g.player.AddTemporarySeenHexes(newHex.GetHexesInRadius(1));
                g.player.AddTemporaryScoutCenters(new[] { newHex });
                g.player.RefreshVisibleHexesImmediate();
            }

            if (!character.GetOwner().LeaderSeesHex(previousHex)) character.GetOwner().visibleHexes.Remove(previousHex);
            character.GetOwner().visibleHexes.Add(newHex);
            if (g != null && g.IsPlayerCurrentlyPlaying() && g.player == character.GetOwner())
            {
                character.GetOwner().RefreshVisibleHexesImmediate();
            }

            if ((!wasWater && isWater) || (wasWater && !isWater) || finishMovement)
            {
                character.moved = character.GetMaxMovement();
                if(!wasWater && isWater) MessageDisplayNoUI.ShowMessage(newHex, character, "Set Sail!", Color.cyan);
                if(wasWater && !isWater) MessageDisplayNoUI.ShowMessage(newHex, character, "Disembarked", Color.cyan);
            }
            else
            {
                character.moved += newHex.GetTerrainCost(character);                 
            }

            if (g != null && g.player != null && character.GetOwner() != g.player)
            {
                bool playerCanSee = g.player.visibleHexes.Contains(newHex) && newHex.IsHexSeen();
                if (playerCanSee)
                {
                    BoardNavigator.Instance?.EnqueueEnemyFocus(newHex, character.GetOwner());
                }
            }

        } catch (Exception e)
        {
            character.moved = movedBefore;
            Debug.LogError($"Error moving character: {e.Message}\n{e.StackTrace}");
            if (hexes.TryGetValue(newHex.v2, out Hex pathHex))
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

            Hex currentHex = previousHex;
            // Make sure character is in current hex
            if (!currentHex.characters.Contains(character)) currentHex.characters.Add(character);

            // Make sure army is in current hex
            if (character.IsArmyCommander() && !currentHex.armies.Contains(character.GetArmy())) currentHex.armies.Add(character.GetArmy());

            // Set character's hex reference properly
            character.hex = currentHex;

            // Redraw
            currentHex.RedrawCharacters();
            currentHex.RedrawArmies();
            if (finishMovement)
            {
              currentHex.LookAt();  
              SelectHex(currentHex.v2, lookAt);
            }
        }
    }

    private static void HandleWarshipAnchoring(Character character, Hex previousHex, Hex newHex, bool wasWater, bool isWater)
    {
        if (character == null || previousHex == null || newHex == null || !character.IsArmyCommander()) return;
        Army army = character.GetArmy();
        if (army == null) return;
        Leader owner = character.GetOwner();
        if (owner == null) return;

        if (!wasWater && isWater)
        {
            int pickedUp = previousHex.TakeAnchoredWarships(owner);
            if (pickedUp > 0) army.ws += pickedUp;
        }

        bool previousIsShore = previousHex.terrainType == TerrainEnum.shore;
        bool previousHasPort = previousHex.HasPcPort();
        bool newIsLandWithoutPortOrShore = !isWater && newHex.terrainType != TerrainEnum.shore && !newHex.HasPcPort();
        if (army.ws > 0 && (previousIsShore || previousHasPort) && newIsLandWithoutPortOrShore)
        {
            int anchored = previousHex.AddAnchoredWarships(owner, army.ws);
            if (anchored > 0) army.ws -= anchored;
        }
    }

    // Helper method to handle movement failure and ensure consistent state
    private void HandleMovementFailure(Character character, Hex currentHex, List<Vector2Int> path = null, int currentIndex = -1)
    {
        if (currentHex != null && character != null)
        {
            // Only check hexes in the path rather than the entire map
            if (path != null && currentIndex >= 0)
            {
                // Only need to check hexes that we've already passed through or started to enter
                for (int i = 0; i <= currentIndex + 1 && i < path.Count; i++)
                {
                    if (hexes.TryGetValue(path[i], out Hex pathHex))
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
            if (ShouldShowPlayerUi(character))
            {
                currentHex.LookAt();
                SelectHex(currentHex.v2);
            }
        }

        if (!ShouldShowPlayerUi(character)) return;

        var actionsManager = FindFirstObjectByType<Layout>().GetActionsManager();
        if (actionsManager != null)
        {
            actionsManager.Refresh(character);
        }

        var selected = FindFirstObjectByType<Layout>().GetSelectedCharacterIcon();
        if (selected != null)
        {
            selected.RefreshMovementLeft(character);
        }
    }

    private bool ShouldShowPlayerUi(Character character)
    {
        Game g = FindFirstObjectByType<Game>();
        return g != null && g.IsPlayerCurrentlyPlaying() && g.player == character?.GetOwner();
    }
    private void UpdateGenerationProgress(float progress, string stage)
    {
        // Update the progress bar
        if (progressBar != null) progressBar.value = progress;

        // Update the status text
        if (statusText != null)
        {
            string markStart = drawMark ? "<mark=#ffffff>" : "";
            string markEnd = drawMark ? "</mark>" : "";
            string sProgress = progress >= 0.99 ? "Launching the game. Please, wait..." : $"{stage} - {progress * 100:F0}%"; 
            statusText.text = $"{markStart}{sProgress}{markEnd}";
        }
            
    }

    public Hex GetHex(Vector2Int v2)
    {
        if (hexes == null) return null;
        hexes.TryGetValue(v2, out Hex hex);
        return hex;
    }

    private void SetSelectedCharacter(Character value)
    {
        if (selectedCharacter == value) return;
        Character previous = selectedCharacter;
        selectedCharacter = value;

        if (previous != null && previous.hex != null)
        {
            previous.hex.RedrawCharacters(false);
        }
        if (value != null && value.hex != null && value.hex != previous?.hex)
        {
            value.hex.RedrawCharacters(false);
        }

        SelectedCharacterChanged?.Invoke(previous, value);
        if (value != null)
        {
            Game g = FindFirstObjectByType<Game>();
            if (g != null && g.player == value.GetOwner())
            {
                Sounds.Instance?.PlayVoiceExpression(value);
            }
        }
    }

}
