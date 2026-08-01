using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RetroLOTR.Scenarios;

[RequireComponent(typeof(BoardGenerator), typeof(NationSpawner))]
public class Board : MonoBehaviour
{
    [Header("Board Size")]
    [SerializeField] [Range(1, 200)] int width = 80;
    [SerializeField] [Range(1, 200)] int height = 80;

    [Header("Scenario (optional)")]
    [Tooltip("Resources-relative scenario name to load instead of generating a procedural map. " +
             "Used for in-editor testing; at runtime GameConfig.ScenarioToLoad (set by the menu) takes priority.")]
    [SerializeField] private string scenarioToLoadInEditor = "";
    // The authored scenario the board is currently loading; null means procedural generation.
    private ScenarioData activeScenario;
    public ScenarioData ActiveScenario => activeScenario;

    [Header("Hex Configuration")]
    public GameObject hexPrefab;
    public Vector2 hexSize;
    // Runtime-copied by HexSeamlessTerrain (play-mode grid toggle must never dirty the asset).
    public Material hexSeamlessBlendMaterial;
    // Separate, dedicated asset for just the neon grid's look (color/intensity/width/glow/hue) —
    // HexSeamlessTerrain copies these specific properties onto its runtime blend-material clone,
    // so tuning the grid never means touching hexSeamlessBlendMaterial itself.
    public Material hexGridMaterial;

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
    // Tracks the in-flight hex encounter resolution so movement can block until the
    // player has resolved it (encounters and opportunity cards are blocking events).
    private Task hexEncounterTask;
    // Kept solely as EnsurePortMover's parent anchor (the preserved-but-currently-inert port
    // mover) — no longer holds or copies a character sprite.
    [SerializeField] private SpriteRenderer characterMoverImage;
    // [SerializeField] private SpriteRenderer characterBannerMoverImage;
    [SerializeField] private SpriteRenderer freeArmyMoverImage;
    [SerializeField] private SpriteRenderer darkServantsMoverImage;
    [SerializeField] private SpriteRenderer neutralMoverImage;
    private SpriteRenderer portMoverImage;

    [Header("Start button")]
    public Button startButton;
    public Button tutorialButton;

    [Header("Debug")]
    public bool redraw = false;
    public bool regenerate = false;
    [Tooltip("Neon seam-grid overlay from the Scenario Creator preview (HexSeamlessTerrain's " +
        "_GridOn) — off by default in-game. Toggle to show/hide it on the live board.")]
    public bool showHexGrid = false;
    private bool appliedHexGridState;

    [Header("On the fly")]
    // Colors object
    public Colors colors;
    // Board Generator
    public BoardGenerator boardGenerator;
    // Nation Spawner
    public NationSpawner nationSpawner;

    [Header("Region Labels")]
    public RegionLabelManager regionLabelManager;

    // Array to store the terrain types
    public TerrainEnum[,] terrainGrid;
    // Dictionary to store all generated hexes
    public Dictionary<Vector2Int, Hex> hexes;
    public List<Hex> hexesWithCharacters;
    public List<Hex> hexesWithPCs;
    public List<Hex> hexesWithArtifacts;
    private Game cachedGame;
    private Layout cachedLayout;
    private ActionsManager cachedActionsManager;

    // Direction vectors for hex neighbors (v2 = (row, col); row 0 = north, rows grow southward).
    public readonly Vector2Int[] evenRowNeighbors = new[] {
        new Vector2Int(1, 0),   // Southeast
        new Vector2Int(0, 1),   // East
        new Vector2Int(-1, 0),   // Northeast
        new Vector2Int(-1, -1),  // Northwest
        new Vector2Int(0, -1),  // West
        new Vector2Int(1, -1)   // Southwest
    };

    public readonly Vector2Int[] oddRowNeighbors = new[] {
        new Vector2Int(1,1),   // Southeast
        new Vector2Int(0, 1),   // East
        new Vector2Int(-1, 1),   // Northeast
        new Vector2Int(-1, 0),  // Northwest
        new Vector2Int(0, -1),  // West
        new Vector2Int(1, 0)   // Southwest
    };

    private bool initialized = false;
    private bool terrainTexturesHidden = false;

    private Game GetGame()
    {
        if (cachedGame == null) cachedGame = FindFirstObjectByType<Game>();
        return cachedGame;
    }

    private Layout GetLayout()
    {
        if (cachedLayout == null) cachedLayout = FindFirstObjectByType<Layout>();
        return cachedLayout;
    }

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

        if (regionLabelManager == null)
            regionLabelManager = FindFirstObjectByType<RegionLabelManager>();

        // Subscribe to generation progress events
        boardGenerator.OnGenerationProgress += UpdateGenerationProgress;

        StartCoroutine(BeginAfterScenarioChoice());
    }

    private void Awake()
    {
        // Fresh scene load = fresh choice. SkipIntro reloads (scenario switch from
        // the old dropdown flow) keep the already-made choice.
        if (!GameConfig.SkipIntro) GameConfig.ScenarioChosen = false;

        // Kicked off here (rather than lazily on the first Hex.RedrawCharacters() call) so the
        // baked spritesheet load has the whole campaign-selection-screen + board-generation
        // window as a head start, instead of starting cold right as the board first becomes
        // visible with characters already standing on it.
        CharacterSpritesheets.EnsureLoading();
    }

    [Header("Campaign Selection")]
    [Tooltip("Disabled campaign/scenario selection object already present in the scene.")]
    [SerializeField] private RetroLOTR.Scenarios.CampaignSelectionManager campaignSelectionScreen;

    // Nothing happens — no intro video, no generation, no leader selector — until the
    // player picks an authored scenario or the default random campaign.
    private IEnumerator BeginAfterScenarioChoice()
    {
        if (!GameConfig.ScenarioChosen)
        {
            ShowCampaignSelection();
            yield return new WaitUntil(() => GameConfig.ScenarioChosen);
        }

        StartCoroutine(ReleaseThrottleIfNoVideo());
        ResolveActiveScenario();
        yield return StartCoroutine(DrawCoroutine());
    }

    // Enables the authored selection screen that is already present (and initially disabled)
    // in the scene.
    private void ShowCampaignSelection()
    {
        if (campaignSelectionScreen != null)
        {
            campaignSelectionScreen.gameObject.SetActive(true);
            return;
        }

        Debug.LogError("Board: no disabled CampaignSelectionScreen scene object is wired; starting the default campaign.");
        GameConfig.ScenarioToLoad = null;
        GameConfig.ScenarioChosen = true;
    }

    // The generation frame budget is throttled only to keep the intro video smooth.
    // If no intro video exists in this scene, release it so generation runs at full speed.
    private IEnumerator ReleaseThrottleIfNoVideo()
    {
        yield return null;
        yield return null;
        IntroVideoManager intro = FindFirstObjectByType<IntroVideoManager>();
        if (intro == null || !intro.isActiveAndEnabled)
        {
            boardGenerator.SetVideoPlaying(false);
        }
    }

    // Picks the scenario to load (menu selection wins over the in-editor test field) and
    // resizes the board to match before any generation runs.
    private void ResolveActiveScenario()
    {
        string scenarioName = GameConfig.HasScenario ? GameConfig.ScenarioToLoad : scenarioToLoadInEditor;
        if (string.IsNullOrWhiteSpace(scenarioName)) return;

        activeScenario = ScenarioLoader.Load(scenarioName);
        if (activeScenario == null)
        {
            Debug.LogError($"Board: failed to load scenario '{scenarioName}'; falling back to procedural generation.");
            return;
        }

        ConfigureSize(activeScenario.width, activeScenario.height);
    }

    // Allows the scenario loader to size the board before generation. No effect once hexes exist.
    public void ConfigureSize(int newWidth, int newHeight)
    {
        width = Mathf.Clamp(newWidth, 1, 200);
        height = Mathf.Clamp(newHeight, 1, 200);
    }

    private void Update()
    {
        if (redraw)
        {
            redraw = false;
            StartCoroutine(DrawCoroutine());
        }

        if (showHexGrid != appliedHexGridState)
        {
            appliedHexGridState = showHexGrid;
            HexSeamlessTerrain.SetGridEnabled(showHexGrid);
        }
    }

    public int GetWidth() => width;

    public int GetHeight() => height;

    public void ForceDraw()
    {
        StartCoroutine(DrawCoroutine(true));
    }

    private IEnumerator DrawCoroutine(bool forced = false)
    {
        if (!initialized || forced)
        {
            if (activeScenario != null)
            {
                // Authored scenario: take terrain straight from the data instead of generating it,
                // then instantiate hexes exactly as the procedural path does.
                var loadTimer = System.Diagnostics.Stopwatch.StartNew();
                terrainGrid = ScenarioLoader.BuildTerrainGrid(activeScenario);
                OnTerrainGenerated(terrainGrid);
                boardGenerator.SetTerrainGrid(terrainGrid);
                Debug.Log($"[ScenarioLoad] terrain grid built in {loadTimer.ElapsedMilliseconds} ms");
                loadTimer.Restart();
                yield return StartCoroutine(boardGenerator.InstantiateHexesCoroutine(OnHexesInstantiated));
                Debug.Log($"[ScenarioLoad] hex instantiation + nation spawn took {loadTimer.ElapsedMilliseconds} ms");
            }
            else
            {
                // Generate terrain first
                bool gridSizeMismatch = terrainGrid != null &&
                    (terrainGrid.GetLength(0) != GetHeight() || terrainGrid.GetLength(1) != GetWidth());
                if (terrainGrid == null || regenerate || gridSizeMismatch)
                {
                    yield return StartCoroutine(boardGenerator.GenerateTerrainCoroutine(OnTerrainGenerated));
                }

                // Then instantiate hexes
                yield return StartCoroutine(boardGenerator.InstantiateHexesCoroutine(OnHexesInstantiated));
            }
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

        if (activeScenario != null)
            nationSpawner.SpawnFromScenario(activeScenario);
        else
            nationSpawner.Spawn();

        if (regionLabelManager != null && hexes != null)
            regionLabelManager.Generate(hexes.Values);

        initialized = true;
        if (startButton != null) startButton.interactable = true;
        if (tutorialButton != null) tutorialButton.interactable = true;
        
        var hexList = GetHexes();
        if (hexList != null)
        {
            hexList.ForEach(x => {
                if (x != null)
                {
                    var hoverTile = x.GetComponent<OnHoverTile>();
                    if (hoverTile != null) hoverTile.enabled = true;
                }
            });
        }
        
        StartCoroutine(SpawnArtifacts());
        HideGenerationProgressUi();
    }

    public void HideGenerationProgressUi()
    {
        HideGenerationUiObject(progressBar != null ? progressBar.gameObject : null);
        HideGenerationUiObject(statusText != null ? statusText.gameObject : null);

        if (progressBar != null && statusText != null)
        {
            Transform progressParent = progressBar.transform.parent;
            Transform statusParent = statusText.transform.parent;
            if (progressParent != null && progressParent == statusParent)
            {
                HideGenerationUiObject(progressParent.gameObject);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void HideGenerationUiObject(GameObject target)
    {
        if (target == null) return;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (target.activeSelf)
        {
            target.SetActive(false);
        }
    }

    IEnumerator SpawnArtifacts()
    {
        // Get all hexes
        List<Hex> hexes = GetHexes();

        List<Artifact> hiddenArtifacts = ArtifactRepository.GetAllClones();

        PlaceTutorialArtifacts(hiddenArtifacts);

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

    private void PlaceTutorialArtifacts(List<Artifact> hiddenArtifacts)
    {
        if (hiddenArtifacts == null) return;
        PlayableLeader[] leaders = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None);
        if (leaders == null || leaders.Length == 0) return;

        HashSet<Hex> usedHexes = new();
        foreach (PlayableLeader leader in leaders)
        {
            if (leader == null || leader.hex == null) continue;
            LeaderBiomeConfig biome = leader.GetBiome();
            if (biome == null || biome.tutorialArtifacts == null || biome.tutorialArtifacts.Count == 0) continue;

            for (int i = hiddenArtifacts.Count - 1; i >= 0; i--)
            {
                Artifact artifact = hiddenArtifacts[i];
                if (artifact == null) continue;
                if (biome.tutorialArtifacts.Any(a => a != null && !string.IsNullOrWhiteSpace(a.artifactName) && string.Equals(a.artifactName, artifact.artifactName, StringComparison.OrdinalIgnoreCase)))
                {
                    hiddenArtifacts.RemoveAt(i);
                }
            }

            List<Hex> anchorHexes = new();
            List<Hex> anchorPrimaryHexes = new();
            if (biome.tutorialAnchors != null && biome.tutorialAnchors.Count > 0)
            {
                NonPlayableLeader[] nonPlayableLeaders = FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None);
                foreach (string anchorName in biome.tutorialAnchors)
                {
                    if (string.IsNullOrWhiteSpace(anchorName)) continue;
                    NonPlayableLeader anchor = nonPlayableLeaders.FirstOrDefault(npl => npl != null && string.Equals(npl.characterName, anchorName, StringComparison.OrdinalIgnoreCase));
                    if (anchor?.hex == null) continue;
                    anchorPrimaryHexes.Add(anchor.hex);
                    anchorHexes.AddRange(anchor.hex.GetHexesInRadius(1));
                }
            }

            List<Hex> candidates = new();
            candidates.AddRange(anchorPrimaryHexes);
            candidates.AddRange(anchorHexes);
            candidates.Add(leader.hex);
            candidates.AddRange(leader.hex.GetHexesInRadius(2));
            candidates = candidates
                .Where(h => h != null && !h.IsWaterTerrain())
                .Distinct()
                .ToList();

            int candidateIndex = 0;
            foreach (Artifact template in biome.tutorialArtifacts)
            {
                if (template == null || string.IsNullOrWhiteSpace(template.artifactName)) continue;
                Artifact artifact = template.Clone();

                Hex target = null;
                while (candidateIndex < candidates.Count)
                {
                    Hex candidate = candidates[candidateIndex++];
                    if (candidate == null) continue;
                    if (usedHexes.Contains(candidate)) continue;
                    target = candidate;
                    break;
                }

                if (target == null) target = leader.hex;
                target.hiddenArtifacts.Add(artifact.Clone());
                usedHexes.Add(target);
            }
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

    // UnityEvent pickers (e.g. a UI Button's OnClick) only list public methods/properties, not
    // plain public fields — showHexGrid itself can't be wired up directly, hence this wrapper.
    public void ToggleHexGrid()
    {
        showHexGrid = !showHexGrid;
        appliedHexGridState = showHexGrid;
        HexSeamlessTerrain.SetGridEnabled(showHexGrid);
    }

    public void ToggleAllTerrainTextures()
    {
        if (hexes == null) return;
        terrainTexturesHidden = !terrainTexturesHidden;
        foreach (var hex in hexes.Values)
        {
            if (hex == null) continue;
            bool revealed = hex.IsHexRevealed();
            if (hex.terrainTexture != null)
                hex.terrainTexture.gameObject.SetActive(revealed && !terrainTexturesHidden);
        }

        if (Camera.main != null && regionLabelManager != null)
        {
            int layer = LayerMask.NameToLayer(regionLabelManager.labelsLayerName);
            if (layer >= 0)
            {
                if (terrainTexturesHidden)
                    Camera.main.cullingMask |= 1 << layer;
                else
                    Camera.main.cullingMask &= ~(1 << layer);
            }
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
            Game game = GetGame();
            Leader player = game != null ? game.player : null;
            if (player == null) return;
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
                List<Character> myCharacters = hexes[selection].characters.FindAll(x => x.GetOwner() == player.GetOwner() && !x.IsKidnapped());

                if (myCharacters.Count < 1)
                {
                    SetSelectedCharacter(null);
                    HideSelectedCharacterUi();
                    return;
                }

                if (myCharacters.Count == 1)
                {
                    UnselectHex();
                    selectedHex = selection;
                    hexes[selection].Select(lookAt, duration, delay);

                    SetSelectedCharacter(myCharacters[0]);
                    RefreshSelectedCharacterUi();
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
                    
                    RefreshSelectedCharacterUi();
                }

            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            selectedHex = Vector2Int.one * -1;
            SetSelectedCharacter(null);
            HideSelectionUi();
            return;
        }
        if (hexes != null && hexes.TryGetValue(selection, out var selected))
        {
            Music.Instance?.UpdateForHex(selected);
        }
    }

    public void UnselectHex()
    {
        if (selectedHex != Vector2Int.one * -1 && hexes != null && hexes.TryGetValue(selectedHex, out Hex hex))
        {
            hex.Unselect();
        }
        selectedHex = Vector2Int.one * -1;

        // Execute these actions after the delay
        HideSelectionUi();
        SetSelectedCharacter(null);
        Music.Instance?.UpdateForHex(null);
    }

    public void UnselectCharacter()
    {
        SetSelectedCharacter(null);
    }

    private void RefreshSelectedCharacterUi()
    {
        Layout layout = GetLayout();
        layout?.GetSelectedCharacterIcon()?.Refresh(selectedCharacter);
    }

    private void HideSelectedCharacterUi()
    {
        Layout layout = GetLayout();
        layout?.GetSelectedCharacterIcon()?.Hide();
    }

    private void HideSelectionUi()
    {
        Layout layout = FindFirstObjectByType<Layout>();
        layout?.GetActionsManager()?.Hide();
        layout?.GetSelectedCharacterIcon()?.Hide();
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
        if (character.hex != null && targetHexCoordinates == character.hex.v2)
        {
            FindFirstObjectByType<HexPathRenderer>()?.HidePath();
            return;
        }

        moving = true;
        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        List<Vector2Int> path = pathRenderer.FindPath(character.hex.v2, targetHexCoordinates, character);

        StartCoroutine(MoveCoroutine(character, path));
    }

    // Turns the hex's OWN persistent character controller in place toward worldDelta, then
    // starts its walk-cycle animation. Replaces the old StartMoverWalkAnimation-on-a-separate-
    // mover-object approach: there's no second SpriteRenderer/CharacterAnimationController to
    // activate or rescale anymore, since we now drive fromSR's own controller directly and
    // Hex.RedrawCharacters() already keeps its GameObject active whenever a character occupies
    // the hex.
    private IEnumerator TurnCharacterTowardMovement(Character character, CharacterAnimationController controller, Vector3 worldDelta)
    {
        if (controller == null) yield break;
        yield return controller.TurnTowardMovement(character, worldDelta);
        controller.PlayMovement(character, worldDelta);
    }

    // Lerps the hex's own persistent character SpriteRenderer directly between two world
    // positions instead of copying its look onto a separate mover clone. No
    // GetDesiredWorldScale/CopyPropertyBlock/ApplyMoverOutline needed — fromSR already IS the
    // correctly-scaled, correctly-dressed sprite for whatever it's showing. toSR (the
    // destination hex's own, separate renderer) stays hidden throughout so the destination
    // isn't revealed early, exactly as before. Bumps fromSR's own sortingOrder while airborne
    // (matching the old mover's +100 offset), then restores its baseline local transform and
    // sortingOrder, and switches its controller back to Standing Idle — the orientation reached
    // by the walk/turn is deliberately left as-is (not reset to Forward) so the character stays
    // facing the direction it actually landed in; see Character.lastFacingOrientation for how
    // that facing then carries over to the next hex's own controller instance.
    private IEnumerator WalkCharacterOnHex(
        Character character,
        CharacterAnimationController controller,
        SpriteRenderer fromSR,
        SpriteRenderer toSR,
        Vector3 start,
        Vector3 end,
        float duration,
        Camera followCam = null,
        AnimationCurve ease = null)
    {
        if (fromSR == null) yield break;

        Vector3 baseLocalPos = fromSR.transform.localPosition;
        Quaternion baseLocalRot = fromSR.transform.localRotation;
        Vector3 baseLocalScale = fromSR.transform.localScale;
        int baseSortingOrder = fromSR.sortingOrder;

        bool toPrevEnabled = toSR != null && toSR.enabled;
        if (toSR != null) toSR.enabled = false;

        fromSR.sortingOrder = baseSortingOrder + 100;
        fromSR.transform.position = start;

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
            fromSR.transform.position = pos;

            if (followCam != null)
            {
                Vector3 camPos = pos + camOffset;
                camPos.z = followCam.transform.position.z;
                followCam.transform.position = camPos;
            }

            yield return null;
        }

        fromSR.transform.position = end;

        if (controller != null)
        {
            controller.SetAnimation(CharacterAnimationController.AnimationKind.StandingIdle);
            controller.SetLoop(false);
        }

        fromSR.transform.localPosition = baseLocalPos;
        fromSR.transform.localRotation = baseLocalRot;
        fromSR.transform.localScale = baseLocalScale;
        fromSR.sortingOrder = baseSortingOrder;
        if (toSR != null) toSR.enabled = toPrevEnabled;
    }

    // Banner/army/port portion of the dual (army-commander) movement animation — the character
    // portion now runs separately via WalkCharacterOnHex, driving the hex's own persistent
    // sprite/controller directly instead of a mover clone. Banner and army/port movers are
    // currently always inert in practice: Hex.GetArmySpriteRendererOnHex/GetPortSpriteRenderer
    // are permanently stubbed to null and Hex.ShouldShowWarshipPort always returns false — this
    // is preserved so army/port animation picks back up automatically if those are ever
    // un-stubbed, without needing to touch this function again.
    private IEnumerator AnimateSpriteBetweenDual(
        SpriteRenderer fromArmySR,
        SpriteRenderer toArmySR,
        SpriteRenderer moverArmySR,
        SpriteRenderer fromPortSR,
        SpriteRenderer toPortSR,
        SpriteRenderer moverPortSR,
        Vector3 armyStart,
        Vector3 armyEnd,
        Vector3 portStart,
        Vector3 portEnd,
        bool hideStaticPort,
        float duration,
        Camera followCam = null,
        AnimationCurve ease = null
        )
    {
        bool useArmy = moverArmySR != null && fromArmySR != null;
        bool usePort = moverPortSR != null && fromPortSR != null;

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
            camOffset = followCam.transform.position - armyStart;

        float elapsed = 0f;
        ease ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = ease.Evaluate(t);

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

            if (followCam != null && moverArmySR != null)
            {
                Vector3 camPos = moverArmySR.transform.position + camOffset;
                camPos.z = followCam.transform.position.z;
                followCam.transform.position = camPos;
            }

            yield return null;
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

        if (fromArmySR != null) fromArmySR.enabled = fromArmyPrevEnabled;
        if (toArmySR != null) toArmySR.enabled = toArmyPrevEnabled;

        if (hideStaticPort)
        {
            if (fromPortSR != null) fromPortSR.enabled = fromPortPrevEnabled;
            if (toPortSR != null) toPortSR.enabled = toPortPrevEnabled;
        }
    }

    private IEnumerator AnimateArmyIcon(GameObject armyIcon, Vector3 start, Vector3 end, float duration)
    {
        if (armyIcon == null) yield break;
        AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = ease.Evaluate(t);
            if (armyIcon != null)
                armyIcon.transform.position = Vector3.Lerp(start, end, e);
            yield return null;
        }
        if (armyIcon != null)
            armyIcon.transform.position = end;
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

    // True only while the per-step movement tween is running, so hexes skip the
    // allocation-heavy icon-grid rebuilds during transit. The grids are rebuilt once,
    // frame-budgeted, when the walk finishes (RevealVisibleHexesAsync).
    public static bool SuppressHexIconGrids = false;

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

    /*private SpriteRenderer EnsureCharacterBannerMover(SpriteRenderer mover)
    {
        if (mover == null) return null;
        if (characterBannerMoverImage != null) return characterBannerMoverImage;

        Transform parent = mover.transform.parent;
        if (parent != null)
        {
            Transform existing = parent.Find("bannerMover");
            if (existing != null)
            {
                characterBannerMoverImage = existing.GetComponent<SpriteRenderer>();
                if (characterBannerMoverImage != null) return characterBannerMoverImage;
            }
        }

        GameObject bannerMover = new("bannerMover");
        bannerMover.transform.SetParent(mover.transform.parent, false);
        characterBannerMoverImage = bannerMover.AddComponent<SpriteRenderer>();
        characterBannerMoverImage.gameObject.SetActive(false);
        return characterBannerMoverImage;
    }*/

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
            actionsManager = GetLayout().GetActionsManager();
            actionsManager.Hide();
            selected = GetLayout().GetSelectedCharacterIcon();
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

        // appearanceFromCharSR/appearanceFromCharSprite used to let the mover borrow a specific
        // sprite rather than whatever fromSR happened to show. Moot now that we drive fromSR's
        // own controller by character IDENTITY (TurnTowardMovement(character, ...)/Show(character))
        // instead of copying a sprite onto a stand-in.

        // Skip the allocation-heavy per-hex icon-grid rebuilds while the unit is in transit;
        // they are rebuilt once after arrival by the budgeted refresh below.
        SuppressHexIconGrids = true;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Hex previousHex = hexes[path[i]];
            Hex newHex = hexes[path[i + 1]];
            currentHex = previousHex;

            // Get the visible sprites on each hex
            SpriteRenderer fromSR = previousHex.GetCharacterSpriteRendererOnHex();
            SpriteRenderer toSR = newHex.GetCharacterSpriteRendererOnHex();

            // The character portion of the animation now drives fromSR's OWN persistent
            // CharacterAnimationController directly instead of a separate mover clone. Guard
            // against the rare multi-occupant-hex case where fromSR is currently displaying a
            // DIFFERENT character than the one moving (Hex.TryGetKnownCharacterForIcon picks one
            // of possibly several by priority) — hijacking it would visibly repaint that hex's
            // slot to the wrong identity mid-walk, so such hops just skip the visual animation
            // (the data-model move via MoveCharacterOneHex still happens normally).
            CharacterAnimationController controller = fromSR != null ? previousHex.GetCharacterAnimationController() : null;
            bool canAnimateChar = controller != null
                && previousHex.TryGetKnownCharacterForIcon(out Character shownOnFrom)
                && shownOnFrom == character;
            SpriteRenderer fromArmySR = previousHex.GetArmySpriteRendererOnHex(character);
            SpriteRenderer toArmySR = newHex.GetArmySpriteRendererOnHex(character);
            SpriteRenderer fromPortSR = previousHex.GetPortSpriteRenderer();
            SpriteRenderer toPortSR = newHex.GetPortSpriteRenderer();

            GameObject armyIconObj = previousHex.GetArmyIconForCommander(character);
            Vector3 armyIconStart = armyIconObj != null ? armyIconObj.transform.position : previousHex.transform.position;
            Vector3 armyIconEnd = newHex.transform.position;

            // Use the sprite transforms' world positions (NOT RectTransform)
            Vector3 startPos = (fromSR != null) ? fromSR.transform.position : previousHex.transform.position;
            Vector3 endPos = (toSR != null) ? toSR.transform.position : newHex.transform.position;
            Vector3 startArmyPos = (fromArmySR != null) ? fromArmySR.transform.position : previousHex.transform.position;
            Vector3 endArmyPos = (toArmySR != null) ? toArmySR.transform.position : newHex.transform.position;
            Vector3 startPortPos = (fromPortSR != null) ? fromPortSR.transform.position : previousHex.transform.position;
            Vector3 endPortPos = (toPortSR != null) ? toPortSR.transform.position : newHex.transform.position;

            IEnumerator dualTween = null;
            IEnumerator charTween = null;
            Coroutine armyIconTween = null;

            try
            {
                if (character.IsArmyCommander())
                {
                    bool hasAny = armyMoverSR != null && fromArmySR != null;
                    if (hasAny)
                    {
                    bool hasWarships = character.GetArmy() != null && character.GetArmy().ws > 0;
                    bool fromWarshipPort = previousHex.ShouldShowWarshipPort();
                    bool toWarshipPort = newHex.ShouldShowWarshipPort();
                    bool hasPcPort = previousHex.HasPcPort() || newHex.HasPcPort();
                    bool usePort = hasWarships && fromWarshipPort && toWarshipPort && !hasPcPort;
                    SpriteRenderer portMover = usePort ? EnsurePortMover(characterMoverSR) : null;

                        // The character portion of the animation now runs separately via
                        // charTween/WalkCharacterOnHex below, driving fromSR's own controller
                        // directly — this call only still matters for the (currently
                        // stubbed-to-null/false) army/port portions.
                        dualTween = AnimateSpriteBetweenDual(
                            fromArmySR,
                            toArmySR,
                            armyMoverSR,
                            fromPortSR,
                            toPortSR,
                            portMover,
                            startArmyPos,
                            endArmyPos,
                            startPortPos,
                            endPortPos,
                            usePort,
                            0.35f);
                    }
                }

                if (canAnimateChar)
                {
                    charTween = WalkCharacterOnHex(character, controller, fromSR, toSR, startPos, endPos, 0.35f);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error preparing animation at step {i}: {e.Message}\n{e.StackTrace}");
                HandleMovementFailure(character, currentHex, path, i);
                dualTween = null;
                charTween = null;
            }

            if (canAnimateChar && charTween != null)
                yield return TurnCharacterTowardMovement(character, controller, endPos - startPos);

            // Started here (not earlier, while still preparing the tween above) so the army
            // icon's own tween begins on the same frame as the character's position tween
            // instead of during the turn-in-place phase that precedes it — previously the
            // army icon started sliding while the character was still turning in place.
            if (armyIconObj != null)
            {
                armyIconObj.transform.SetParent(null, true);
                armyIconTween = StartCoroutine(AnimateArmyIcon(armyIconObj, armyIconStart, armyIconEnd, 0.35f));
            }

            Coroutine dualCoroutine = dualTween != null ? StartCoroutine(dualTween) : null;
            if (charTween != null) yield return charTween;
            if (dualCoroutine != null) yield return dualCoroutine;

            if (armyIconTween != null)
            {
                yield return armyIconTween;
                if (armyIconObj != null) Destroy(armyIconObj);
            }

            try
            {
                // Commit logic AFTER the tween so Redraw snaps to the new hex cleanly.
                // Defer the whole-board visibility refresh off the per-step hot path so the
                // character tween stays smooth; the local RevealArea inside still animates
                // the newly discovered hexes. A single budgeted refresh runs after arrival.
                // finishMovement stays false even on the last hop: it would drain the
                // character's remaining movement (no more moves this turn). Arrival effects
                // (hex selection, situation cards) run after the loop instead.
                MoveCharacterOneHex(character, previousHex, newHex, false, false, deferVisibilityRefresh: true);
                currentHex = newHex;
                if (showPlayerUi) selected.RefreshMovementLeft(character);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during character movement at step {i}: {e.Message}\n{e.StackTrace}");
                SuppressHexIconGrids = false;
                HandleMovementFailure(character, currentHex, path, i);
                yield break;
            }

            // Encounters and opportunity (situation) cards are blocking: if stepping onto
            // this hex raised one, halt the walk here until the player resolves it. Only the
            // player is held — AI resolves these without on-screen UI, so it never stalls.
            if (showPlayerUi)
            {
                while (HasBlockingEventPending())
                    yield return null;
            }

            // optional: tiny pacing pause
            // yield return new WaitForSeconds(0.02f);
        }

        // Re-enable icon grids so the post-arrival refresh repopulates them.
        SuppressHexIconGrids = false;

        // Now that the walk has finished, reconcile fog/visibility for the whole board once,
        // spread across frames (batched yields) so it never spikes the frame.
        Game moveGame = FindFirstObjectByType<Game>();
        if (moveGame != null && moveGame.IsPlayerCurrentlyPlaying() && moveGame.player == character.GetOwner())
        {
            yield return StartCoroutine(moveGame.player.RevealVisibleHexesAsync());
        }

        try
        {
            // Per-hop card refreshes were skipped during the walk (deferVisibilityRefresh);
            // reconcile the hand once now that the character has arrived.
            if (ShouldRefreshCardInteractionsFor(character))
            {
                RefreshCardInteractions();
            }
            if (showPlayerUi)
            {
                selected.RefreshMovementLeft(character);
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
        }

        // Opportunity cards fire where the character STOPS. Walk hops pass
        // finishMovement: false (it would drain the remaining movement), so the
        // destination check happens here instead; it self-guards to the player's turn.
        if (character != null && !character.killed)
        {
            CheckAndShowSituationCards(character, character.hex);
            TriggerOwnPcGrantIfStandingOnOne(character, character.hex);
        }
    }

    public void MoveCharacterOneHex(Character character, Hex previousHex, Hex newHex, bool finishMovement = false, bool lookAt = true, bool rememberPreviousHex = true, bool deferVisibilityRefresh = false) {
        int movedBefore = character.moved;
        bool wasWater = previousHex != null && previousHex.IsWaterTerrain();
        bool isWater = newHex != null && newHex.IsWaterTerrain();
        Game g = GetGame();
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
            if (rememberPreviousHex)
            {
                character.previousHex = previousHex;
            }
            character.hex = newHex;
            character.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
            Character.RefreshArtifactPcVisibilityForHex(newHex);
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
            if (g != null && character.GetOwner() == g.player)
            {
                if (lookAt) newHex.LookAt();
                character.hex.RevealArea(1, lookAt);

                if (g != null && g.player is PlayableLeader movingLeader)
                {
                    string hexRegion = newHex.GetLandRegion();
                    if (string.IsNullOrWhiteSpace(hexRegion))
                    {
                        PC pc = newHex.GetPCData();
                        if (pc != null)
                        {
                            DeckManager dm = FindFirstObjectByType<DeckManager>();
                            if (dm != null) hexRegion = dm.ResolveRegionForPc(pc);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(hexRegion) && movingLeader.TryDiscoverRegion(hexRegion))
                        DeckManager.NotifyRegionDiscovered(hexRegion, newHex);
                }

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

            TutorialManager.Instance?.HandleCharacterArrived(character, newHex);

            if (!character.GetOwner().LeaderSeesHex(previousHex)) character.GetOwner().visibleHexes.Remove(previousHex);
            character.GetOwner().visibleHexes.Add(newHex);
            // The whole-board visibility refresh is the per-step frame spike during a walk.
            // When deferred (multi-hex path moves), skip it here; MoveCoroutine runs a single
            // frame-budgeted RevealVisibleHexesAsync after arrival instead.
            if (!deferVisibilityRefresh && g != null && g.IsPlayerCurrentlyPlaying() && g.player == character.GetOwner())
            {
                character.GetOwner().RefreshVisibleHexesImmediate();
            }

            // Re-evaluating every card in hand is wasted work on each hop of a walk —
            // nothing is playable mid-transit. Deferred (multi-hex) moves refresh once
            // after arrival in MoveCoroutine instead.
            if (!deferVisibilityRefresh && ShouldRefreshCardInteractionsFor(character))
            {
                RefreshCardInteractions();
            }

            // Opportunity cards only fire where the character STOPS — stepping through a hex
            // mid-path never interrupts the walk with a full-screen overlay.
            if (finishMovement)
            {
                CheckAndShowSituationCards(character, newHex);
                TriggerOwnPcGrantIfStandingOnOne(character, newHex);
            }

            if (newHex.HasPendingEncounters && character != null && !character.killed)
            {
                Task encounterTask = TriggerHexEncountersAsync(character, newHex);
                // Only the player's move waits on resolution; track the task so MoveCoroutine
                // can block on it. AI resolves in the background and must never stall the
                // player's later movement, so its task is left fire-and-forget.
                if (ShouldShowPlayerUi(character)) hexEncounterTask = encounterTask;
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

            // Climate hazards of the tile just entered (snow frostbite / desert sunburn).
            TerrainEntryEffects.ProcessEntry(character, newHex);

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
            Hex previousCharacterHex = character.hex;
            character.hex = currentHex;
            character.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousCharacterHex);
            Character.RefreshArtifactPcVisibilityForHex(currentHex);

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
            Hex previousHex = character.hex;
            character.hex = currentHex;
            character.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
            Character.RefreshArtifactPcVisibilityForHex(currentHex);

            // Redraw
            currentHex.RedrawCharacters();
            currentHex.RedrawArmies();
            if (ShouldShowPlayerUi(character))
            {
                currentHex.LookAt();
                SelectHex(currentHex.v2);
            }

            if (ShouldRefreshCardInteractionsFor(character))
            {
                RefreshCardInteractions();
            }
        }

        if (!ShouldShowPlayerUi(character)) return;

        var selected = GetLayout().GetSelectedCharacterIcon();
        if (selected != null)
        {
            selected.RefreshMovementLeft(character);
        }
    }

    private bool ShouldShowPlayerUi(Character character)
    {
        Game g = GetGame();
        return g != null && g.IsPlayerCurrentlyPlaying() && g.player == character?.GetOwner();
    }

    private bool ShouldRefreshCardInteractionsFor(Character character)
    {
        Game g = GetGame();
        return g != null && g.IsPlayerCurrentlyPlaying() && character != null && character.GetOwner() == g.player;
    }

    private ActionsManager GetActionsManager()
    {
        if (cachedActionsManager == null) cachedActionsManager = FindFirstObjectByType<ActionsManager>();
        return cachedActionsManager;
    }

    private void RefreshCardInteractions()
    {
        Card.RequestInteractionRefreshAll();
        GetActionsManager()?.RefreshInteractableState();
    }

    // True while a blocking event (an encounter dialog, an opportunity/situation card, or
    // an in-flight encounter resolution) is awaiting the player. Movement waits on this so
    // it never advances over an unresolved event.
    private bool HasBlockingEventPending()
    {
        if (SelectionDialog.IsShowing) return true;
        if (SituationCardsUI.IsShowing) return true;
        if (hexEncounterTask != null && !hexEncounterTask.IsCompleted) return true;
        return false;
    }

    private static async Task TriggerHexEncountersAsync(Character character, Hex hex)
    {
        while (hex != null && hex.HasPendingEncounters)
        {
            // Don't stack an encounter on top of another blocking event. If an opportunity
            // card or another dialog is already on screen, let it resolve first — the first
            // event to appear blocks the rest.
            while (SituationCardsUI.IsShowing || SelectionDialog.IsShowing)
                await Task.Yield();

            CardData card = hex.TakeFirstPendingEncounter();
            if (card == null) break;
            await EncounterResolver.ResolveAsync(card, character);
        }
    }

    [Header("Situation Cards")]
    [SerializeField] private SituationCardsUI situationCardsUIPrefab;
    [Tooltip("Seconds the camera rests on the character's hex before the opportunity-card overlay covers the screen.")]
    [SerializeField] private float situationCardsFocusDelay = 1f;
    private Coroutine situationCardsSequence;
    // Whether ShowSituationCardsSequence currently holds CenterDisplayLock. StopCoroutine
    // (below) abandons a coroutine mid-flight without running any finally/cleanup, so a
    // held lock must be released explicitly here or a stale sequence would wedge every
    // other center display (turn banner, PC/region grant preview) shut forever.
    private bool situationCardsHoldCenterLock;

    private void ReleaseCenterLockIfHeldBySituationCards()
    {
        if (!situationCardsHoldCenterLock) return;
        situationCardsHoldCenterLock = false;
        CenterDisplayLock.Release();
    }

    private void CheckAndShowSituationCards(Character character, Hex hex)
    {
        if (character == null || hex == null) { Debug.Log("[SituationCards] null character or hex"); return; }
        Game g = FindFirstObjectByType<Game>();
        if (g == null) { Debug.Log("[SituationCards] no Game found"); return; }
        if (!g.IsPlayerCurrentlyPlaying()) { Debug.Log("[SituationCards] not player's turn"); return; }
        if (g.player != character.GetOwner()) { Debug.Log($"[SituationCards] character {character.characterName} not owned by player"); return; }

        DeckManager deckManager = FindFirstObjectByType<DeckManager>();
        if (deckManager == null) { Debug.Log("[SituationCards] no DeckManager found"); return; }

        var activeSituations = SituationEvaluator.GetActiveSituations(character, hex);
        Debug.Log($"[SituationCards] {character.characterName} @ {hex.name} — active situations: [{string.Join(", ", activeSituations)}]");

        List<CardData> situationCards = deckManager.GetSituationCards(g.player, character, hex);
        Debug.Log($"[SituationCards] matched cards: {situationCards.Count} — [{string.Join(", ", situationCards.Select(c => c.name))}]");

        if (situationCards == null || situationCards.Count == 0) return;

        if (situationCardsSequence != null)
        {
            StopCoroutine(situationCardsSequence);
            ReleaseCenterLockIfHeldBySituationCards();
        }
        situationCardsSequence = StartCoroutine(ShowSituationCardsSequence(situationCards, character, hex));
    }

    // The overlay covers the whole screen, so let the player see WHERE the opportunity arose:
    // settle the camera on the hex, hold it for a beat, then pop the cards. Opportunity cards
    // are an exclusive center-screen display, same as the turn banner and the PC/region grant
    // preview — CenterDisplayLock ensures only one of those is ever up at a time.
    private IEnumerator ShowSituationCardsSequence(List<CardData> situationCards, Character character, Hex hex)
    {
        hex.LookAt();
        yield return new WaitForSeconds(situationCardsFocusDelay);

        // Let any dialog raised in the meantime (e.g. an encounter on the same hex) resolve first.
        while (SelectionDialog.IsShowing || (hexEncounterTask != null && !hexEncounterTask.IsCompleted))
            yield return null;

        // The situation was true for THIS hex when the walk ended; if the world moved on while
        // we waited (character moved/died, turn changed hands), don't show stale opportunities.
        Game g = FindFirstObjectByType<Game>();
        if (character == null || character.killed || character.hex != hex) { situationCardsSequence = null; yield break; }
        if (g == null || !g.IsPlayerCurrentlyPlaying() || g.player != character.GetOwner()) { situationCardsSequence = null; yield break; }

        if (SituationCardsUI.Instance == null)
        {
            if (situationCardsUIPrefab != null)
            {
                Instantiate(situationCardsUIPrefab);
            }
            else
            {
                var go = new GameObject("SituationCardsUI");
                go.AddComponent<SituationCardsUI>();
            }
        }

        yield return CenterDisplayLock.WaitCoroutine();
        situationCardsHoldCenterLock = true;

        SituationCardsUI.Instance.Show(situationCards, character);
        yield return new WaitUntil(() => !SituationCardsUI.IsShowing);

        ReleaseCenterLockIfHeldBySituationCards();
        situationCardsSequence = null;
    }

    // Re-triggers a PC's resource-granting effect (PCAction.asyncEffect's already-founded
    // branch) whenever a character is standing on one of its own leader's founded PCs —
    // once when they arrive (called alongside CheckAndShowSituationCards) and again at the
    // start of every turn (Character.NewTurn -> this same method). Founding itself is a
    // separate, existing flow (PCAction's not-yet-founded branch, still only reachable by
    // manually playing the PC card) — this never re-founds anything. Fires for every leader,
    // human and AI alike; the card reveal/token-flight visuals are human-player-only.
    //
    // CenterDisplayLock is also structurally required here, not just for the visual gating:
    // PCAction/MaterialRetrieval are singletons cached and shared by class name in
    // ActionsManager (see ActionsManager.ResolveActionByRef) — every card of that action type
    // across every leader/character reuses the SAME instance. This method holds one of those
    // singletons "checked out" (Initialize'd, then not yet Execute'd) across the ~1.5-2.8s
    // show/spin delay below; without serializing, a second grant firing on the same tick
    // (another owned PC/region at turn start, or another leader's turn-start grant sharing the
    // same action type) would re-Initialize the shared instance out from under the first,
    // corrupting its character/card fields mid-flight.
    public async void TriggerOwnPcGrantIfStandingOnOne(Character character, Hex hex)
    {
        if (character == null || character.killed || hex == null) return;

        PC pc = hex.GetPCData();
        if (pc == null || pc.owner != character.GetOwner()) return;

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        CardData pcCard = deckManager?.FindPcCardByPcName(pc.pcName);
        if (pcCard == null) return;

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager?.ResolveActionByRef(pcCard.GetActionRef(), pcCard) is not PCAction pcAction) return;

        pcAction.Initialize(character, pcCard);
        if (!pcAction.IsAlreadyFoundedAndOwnedBySelf(character)) return;

        Game game = FindFirstObjectByType<Game>();
        bool showToPlayer = game != null && game.player == character.GetOwner();

        // Don't even queue for the lock while the Turn/Gathering-Resources banners are still
        // playing - SemaphoreSlim always hands a released permit to an already-queued async
        // waiter before TurnBanner's own Wait(0) polling gets a look-in, so registering here
        // too early would let this grant cut in front of the Gathering Resources banner.
        while (TurnBanner.IsShowing) await Task.Yield();

        await CenterDisplayLock.WaitAsync();
        try
        {
            if (showToPlayer)
            {
                CardCenterPreview.Instance?.ShowPreview(pcCard);
                await Task.Delay(TimeSpan.FromSeconds(1.5));
                CardCenterPreview.Instance?.HidePreview();

                // Hold off granting resources (and the StoresManager gain animation it triggers)
                // until the token has visually landed on the hex.
                var tokenArrived = new TaskCompletionSource<bool>();
                CardPlayFlight.LaunchFromData(pcCard, hex, () => tokenArrived.TrySetResult(true));
                await tokenArrived.Task;
            }

            // Re-validate after the wait — the world can move on (character killed, PC lost)
            // while the sequence above was held up.
            if (character == null || character.killed || pc.owner != character.GetOwner()) return;

            pcAction.Initialize(character, pcCard);
            await pcAction.Execute();
        }
        finally
        {
            CenterDisplayLock.Release();
        }
    }

    // Region counterpart of TriggerOwnPcGrantIfStandingOnOne: re-grants the region's Land
    // card resources (materials only — Land cards have no other effect) for a character
    // standing anywhere in that region. Land cards have no founding concept, so unlike the
    // PC grant there's no already-founded check — just resolve and re-apply.
    public async void TriggerRegionLandGrant(Character character, Hex hex)
    {
        if (character == null || character.killed || hex == null) return;

        string region = hex.GetLandRegion();
        if (string.IsNullOrWhiteSpace(region)) return;

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        CardData landCard = deckManager?.FindLandCardByRegion(region);
        if (landCard == null) return;

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        CharacterAction action = actionsManager?.ResolveActionByRef(landCard.GetActionRef(), landCard);
        if (action == null) return;

        Game game = FindFirstObjectByType<Game>();
        bool showToPlayer = game != null && game.player == character.GetOwner();

        // See TriggerOwnPcGrantIfStandingOnOne: hold off queuing for the lock until the
        // Turn/Gathering-Resources banners have fully finished.
        while (TurnBanner.IsShowing) await Task.Yield();

        await CenterDisplayLock.WaitAsync();
        try
        {
            if (showToPlayer)
            {
                CardCenterPreview.Instance?.ShowPreview(landCard);
                await Task.Delay(TimeSpan.FromSeconds(1.5));
                CardCenterPreview.Instance?.HidePreview();

                // Hold off granting resources (and the StoresManager gain animation it triggers)
                // until the token has visually landed on the hex.
                var tokenArrived = new TaskCompletionSource<bool>();
                CardPlayFlight.LaunchFromData(landCard, hex, () => tokenArrived.TrySetResult(true));
                await tokenArrived.Task;
            }

            // Re-validate after the wait — the world can move on (character killed/moved)
            // while the sequence above was held up.
            if (character == null || character.killed) return;

            action.Initialize(character, landCard);
            await action.Execute();
        }
        finally
        {
            CenterDisplayLock.Release();
        }
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
            Game g = GetGame();
            if (g != null && g.player == value.GetOwner())
            {
                Sounds.Instance?.PlayVoiceExpression(value);
            }
        }
    }

}
