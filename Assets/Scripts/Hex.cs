using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class Hex : MonoBehaviour
{
    private enum SharedParticleType
    {
        Fire,
        Ice,
        Poison,
        Courage,
        Hope
    }

    private sealed class SharedParticlePoolState
    {
        public GameObject template;
        public readonly List<GameObject> instances = new();
        public Vector3 localPosition = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
    }

    public Vector2Int v2;
    [Header("References")]
    public Sprite defaultCharacterSprite;
    public TextMeshPro messageNoUI;
    public SpriteRenderer hexRegion;

    [Header("Character")]
    public SpriteRenderer characterSpriteRenderer;
    public SpriteRenderer bannerSpriteRenderer;
    [SerializeField] private CharacterAnimationController characterAnimationController;

    [Header("PC Name")]
    public TextMeshPro pcName;
    [SerializeField] private bool showPcMarkBackgroundColor = false;

    [Header("Reveal")]
    [SerializeField] private float revealDuration = 1f;

    [Header("Hover")]
    public GameObject artifact;
    public HoverNoUI artifactHover;

    [Header("Hex Info Panel")]
    public GameObject hexInfoArrow;
    public GameObject hexInfo;
    public TextMeshPro hexInfoText;
    [SerializeField] private float hexInfoHoverDelay = 2f;

    [Header("Grid Sprite Rendereres")]
    public GameObject spriteRendererLayoutIcon;
    public SpriteRendererGridLayout characterClassesIconGrid;
    public SpriteRendererGridLayout armyCharactersIconGrid;

    public SpriteRenderer terrainTexture;
    public GameObject cliffGameObject;
    public GameObject hexTextureWater;

    public GameObject movement;
    public MovementCostManager movementCostManager;



    [Header("Frames")]
    public GameObject hoverHexFrame;
    public GameObject tipHexFrame;
    public GameObject scoutedHexFrame;
    public GameObject darknessHexFrame;

    [Header("Particles")]
    public GameObject selectedParticles;
    public GameObject fireParticles;
    public GameObject iceParticles;
    public GameObject poisonParticles;
    public GameObject courageParticles;
    public GameObject hopeParticles;


    [Header("Outline")]
    public float characterOutlineSize = 10f;
    // public float bannerOutlineSize = 70f;
    private int darknessTurnsRemaining = 0;

    [Header("Selection Alpha")]
    [SerializeField] private float selectedBlinkMinAlpha = 0.5f;
    [SerializeField] private float selectedBlinkMaxAlpha = 1f;
    [SerializeField] private float selectedBlinkSpeed = 2f;

    [Header("Data")]
    [SerializeField] private PC pc;
    [SerializeField] private string assignedLandRegion;
    [SerializeField] private bool isRevealed;
    [SerializeField] private bool mapOnlyRevealed;
    [SerializeField] private bool isCurrentlyUnseen;
    public TerrainEnum terrainType;    
    public List<Army> armies = new();
    public List<Character> characters = new();
    public List<Artifact> hiddenArtifacts = new();


    private Coroutine armyArrangeCoroutine;
    private Coroutine classArrangeCoroutine;
    private Coroutine hexInfoShowCoroutine;
    private Coroutine _arrowBounceCoroutine;
    private float _arrowOriginX;
    private readonly List<Character> _hexInfoCharacters = new();
    private readonly List<Army> _hexInfoArmies = new();   // null entry = character link, non-null = army link
    private int _lastHexInfoLinkIdx = -1;
    private SelectedCharacterIcon _selectedIcon;
    private bool artifactRevealed = false;
    private static Hex s_hexInfoActiveHex;

    // Use HashSet for O(1) contains
    private HashSet<Leader> scoutedBy = new();
    private readonly Dictionary<Leader, int> scoutedByTurns = new();
    private readonly HashSet<Leader> persistentScoutedBy = new();
    private readonly Dictionary<Leader, int> anchoredWarships = new();
    private int anchoredWarshipsTotal = 0;
    private Coroutine revealPulseCoroutine;
    private Vector3 terrainBaseScale;
    private bool terrainBaseScaleCaptured;

    public bool isSelected = false;

    // Cached for speed
    private PlayableLeader leader;

    // Cached singletons/components (avoid repeated global lookups)
    private Colors colors;
    private Board board;
    private BoardNavigator navigator;
    private Game game;

    private Illustrations illustrations;
    private HexTextureMapping hexTextureMapping;
    private Sprite baseTerrainSprite;
    // private Coroutine bannerRetryCoroutine;

    // Reused buffers to avoid GC in UI building / raycasts
    private static readonly StringBuilder sbChars = new(256);
    private static readonly StringBuilder sbFree = new(256);
    private static readonly StringBuilder sbNeutral = new(256);
    private static readonly StringBuilder sbDark = new(256);
    private static readonly Queue<Vector2Int> areaQueue = new(64);
    private static readonly HashSet<Vector2Int> areaVisited = new();
    private static GameObject sharedSelectedParticles;
    private static Hex sharedSelectedParticlesOwner;
    private static Vector3 sharedSelectedParticlesLocalPosition = Vector3.zero;
    private static Quaternion sharedSelectedParticlesLocalRotation = Quaternion.identity;
    private static Vector3 sharedSelectedParticlesLocalScale = Vector3.one;
    private static readonly Dictionary<SharedParticleType, SharedParticlePoolState> sharedParticlePools = new();
    private static Transform sharedParticlePoolRoot;
    private static Material sharedCharacterOutlineMaterial;
    // private static Material sharedBannerOutlineMaterial;
    private MaterialPropertyBlock characterOutlinePropertyBlock;
    private Color? lastAppliedCharacterOutlineColor;
    private float? lastAppliedCharacterOutlineSize;
    // private Color? lastAppliedBannerOutlineColor;
    // private float? lastAppliedBannerOutlineSize;

    private const string Unknown = "Unknown character(s)";
    private const int DarknessTurnsDefault = 2;
    private const int SharedOneShotParticlePoolSize = 3;
    private const string CharacterOutlineMaterialPath = "Materials/CharacterOutline";
    // private const string BannerOutlineMaterialPath = "Materials/BannerOutline";
    private static readonly int OutlineColorShaderId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineSizeShaderId = Shader.PropertyToID("_OutlineSize");
    private const float NonSelectedCharacterAlpha = 0.9f;
    private bool isCharacterHovered = false;

    void Awake()
    {
        pc = null;

        // Cache singletons once
        game = FindFirstObjectByType<Game>();
        colors = FindFirstObjectByType<Colors>();
        board = FindFirstObjectByType<Board>();
        navigator = FindFirstObjectByType<BoardNavigator>();
        illustrations = FindFirstObjectByType<Illustrations>();
        hexTextureMapping = GetComponent<HexTextureMapping>();
        ApplyCharacterOutlineMaterial();
        InitializeSharedSelectedParticles();
        InitializeSharedOneShotParticles();
        /*if (characterIcon != null)
        {
            characterIconZoom = characterIcon.GetComponent<ZoomSpriteRenderer>();
            if (characterIconZoom != null)
            {
                characterIconZoomDefault = characterIconZoom.zoomFactor;
                characterIconOffsetDefault = characterIconZoom.verticalOffset;
            }
        }*/
        UpdateMinimapTerrain(IsHexRevealed());
        UpdateVisibilityForFog();
        UpdateParticles();
        SetActiveFast(hexInfoArrow, false);
        SetActiveFast(hexInfo, false);
    }

    void Update()
    {
        UpdateCharacterSpriteAlpha();
        if (hexInfo != null && hexInfo.activeSelf)
        {
            if (!IsMouseOverHexOrPanel())
                Unhover();
            else
            {
                UpdateHexInfoLinkHover();
                if (Input.GetMouseButtonDown(0) && _lastHexInfoLinkIdx >= 0)
                    HandleHexInfoLinkClick(_lastHexInfoLinkIdx);
            }
        }
    }

    public bool IsHexRevealed() => isRevealed;
    public bool IsHexSeen() => IsHexRevealed() && !mapOnlyRevealed && !isCurrentlyUnseen;
    public List<Hex> GetHexesInRadius(int radius)
    {
        if (board == null) board = FindFirstObjectByType<Board>();
        List<Hex> results = new();
        if (board == null) return results;

        results.Add(this);
        if (radius <= 0) return results;

        var queue = new Queue<Vector2Int>(32);
        var visited = new HashSet<Vector2Int>();
        queue.Enqueue(v2);
        visited.Add(v2);

        int currentRadius = 0;
        while (queue.Count > 0 && currentRadius < radius)
        {
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                var currentHex = queue.Dequeue();
                var neighbors = ((currentHex.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                for (int j = 0; j < neighbors.Length; j++)
                {
                    var offset = neighbors[j];
                    var neighborPos = new Vector2Int(currentHex.x + offset.x, currentHex.y + offset.y);
                    if (!visited.Add(neighborPos)) continue;

                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        results.Add(neighborHex);
                        queue.Enqueue(neighborPos);
                    }
                }
            }
            currentRadius++;
        }

        return results;
    }

    public string GetText()
    {
        if (v2.x < 0 || v2.y < 0) return "";
        return $" at {v2.x}, {v2.y}";
    }

    public PlayableLeader GetPlayer()
    {
        if (leader != null) return leader;
        leader = game.player;
        return leader;
    }

    public bool IsPCRevealed(PlayableLeader overrideLeader = null)
    {
        if(pc == null) return false;
        return pc.IsRevealed(overrideLeader);        
    }

    public bool IsScouted(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        return l != null && scoutedBy.Contains(l);
    }

    public bool IsScoutedBy(Leader leader)
    {
        return leader != null && scoutedBy.Contains(leader);
    }

    public int GetScoutedTurnsRemaining(Leader leader)
    {
        if (leader == null) return 0;
        return scoutedByTurns.TryGetValue(leader, out int turns) ? turns : 0;
    }

    public bool IsFriendlyPC(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        if (l == null || pc == null || pc.owner == null) return false;
        if (!IsPCRevealed(l)) return false;
        if(l == pc.owner) return true;
        var a = pc.owner.GetAlignment();
        return a != AlignmentEnum.neutral && a == l.GetAlignment();
    }

    public bool IsFriendlyCharacter(Character character, PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        if (l == character.GetOwner()) return true;
        if (l == null || !character || character.killed) return false;
        var a = character.GetOwner().GetAlignment();
        return a != AlignmentEnum.neutral && a == l.GetAlignment();
    }

    public void Initialize(int row, int col)
    {
        v2 = new Vector2Int(row, col);
        assignedLandRegion = null;
        if (hexRegion != null) hexRegion.enabled = false;
        if (game == null) game = FindFirstObjectByType<Game>();
        // SpriteRenderer.sortingOrder is effectively signed 16-bit, so keep
        // terrain in (-9999, 0): above the hexRegionFrame underlay (-9999),
        // below every fixed-order hex child. Row decides front-to-back; the
        // col parity bit breaks same-row neighbor ties so their overlap is
        // cut deterministically instead of z-fighting.
        terrainTexture.sortingOrder = -1 - (row * 2) - (col & 1);
        if (terrainTexture != null) terrainTexture.gameObject.SetActive(true);
    }

    public SpriteRenderer GetCharacterSpriteRendererOnHex()
    {
        return characterSpriteRenderer;
    }

    public SpriteRenderer GetArmySpriteRendererOnHex(Character character)
    {
        // Deprecated: army icons are now dynamically instantiated per commander.
        // Returning null disables the legacy army-mover animation in Board.cs.
        return null;
    }

    public GameObject GetArmyIconForCommander(Character commander)
    {
        if (armyCharactersIconGrid == null || commander == null) return null;
        Transform gridTransform = armyCharactersIconGrid.transform;
        for (int i = 0; i < gridTransform.childCount; i++)
        {
            GameObject child = gridTransform.GetChild(i).gameObject;
            SpriteRendererIconManager manager = child.GetComponent<SpriteRendererIconManager>();
            if (manager != null && manager.character == commander)
            {
                return child;
            }
        }
        return null;
    }

    public SpriteRenderer GetPortSpriteRenderer()
    {
        return null;
    }

    public bool HasPcPort() => pc != null && pc.hasPort;

    public bool ShouldShowWarshipPort()
    {
        return false;
    }

    public bool TryGetKnownCharacterForIcon(out Character known)
    {
        known = null;
        if (board == null) board = FindFirstObjectByType<Board>();

        PlayableLeader player = GetPlayer();
        bool isSeen = IsHexSeen();
        if (!isSeen) return false;

        bool isScouted = IsScouted(player);
        Character selected = board != null ? board.selectedCharacter : null;

        if (selected != null && selected.hex == this &&
            (isScouted || IsFriendlyCharacter(selected, player) || selected.IsArmyCommander()))
        {
            known = selected;
            return true;
        }

        if (isScouted)
        {
            for (int i = 0, n = characters.Count; i < n; i++)
            {
                Character candidate = characters[i];
                if (candidate == null || candidate.killed || candidate.hex != this) continue;
                known = candidate;
                return true;
            }
        }

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            Character candidate = characters[i];
            if (candidate == null || candidate.killed || candidate.hex != this) continue;
            if (IsFriendlyCharacter(candidate, player))
            {
                known = candidate;
                return true;
            }
        }

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            Character candidate = characters[i];
            if (candidate == null || candidate.killed || candidate.hex != this) continue;
            if (candidate.IsArmyCommander())
            {
                known = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetPreviewTextForCharacter(Character character, out string text)
    {
        text = null;
        if (character == null || character.hex != this) return false;
        PlayableLeader viewer = GetPlayer();
        bool canSee = IsScouted(viewer) || IsFriendlyCharacter(character, viewer);
        bool isSeen = IsHexSeen();
        bool viewerHasCharacter = viewer != null && HasCharacterOfLeader(viewer);
        if (!canSee && !(character.IsArmyCommander() && (viewerHasCharacter || isSeen))) return false;

        text = character.characterName;
        if (character.IsArmyCommander())
        {
            Army army = character.GetArmy();
            if (army != null) text += army.GetHoverTextNoXp();
        }
        return true;
    }

    public void SetTerrain(TerrainEnum terrainType, Sprite terrainTexture, Color terrainColor)
    {
        this.terrainType = terrainType;
        if (terrainTexture != null)
        {
            baseTerrainSprite = terrainTexture;
        }
        else
        {
            if (hexTextureMapping == null) hexTextureMapping = GetComponent<HexTextureMapping>();
            baseTerrainSprite = hexTextureMapping != null ? hexTextureMapping.GetTerrainBaseSprite(terrainType) : null;
        }
        ApplyHexTextureSprite();
        // this.terrainTexture.color = terrainColor;
        // if(terrainType == TerrainEnum.mountains) this.terrainTexture.sortingOrder += 1000;
    }

    public void RedrawArmies(bool refreshHoverText = true)
    {
        ClearArmyIcons();

        bool hasArmies = armies.Count > 0;
        bool seen = IsHexSeen();
        bool hasVisibleCharacters = false;

        if (seen && spriteRendererLayoutIcon != null && armyCharactersIconGrid != null)
        {
            PlayableLeader viewer = GetPlayer();
            bool isScouted = IsScouted(viewer);

            for (int i = 0, n = armies.Count; i < n; i++)
            {
                Army army = armies[i];
                Character commander = army?.GetCommander();
                if (commander == null) continue;

                GameObject icon = Instantiate(spriteRendererLayoutIcon, armyCharactersIconGrid.transform);
                SpriteRendererIconManager manager = icon.GetComponent<SpriteRendererIconManager>();
                if (manager != null)
                {
                    manager.Initialize(commander);
                }
            }

            for (int i = 0, n = characters.Count; i < n; i++)
            {
                Character ch = characters[i];
                if (ch == null || ch.killed || ch.hex != this) continue;
                if (ch.IsArmyCommander()) continue;

                bool isFriendly = IsFriendlyCharacter(ch, viewer);
                bool canSee = isFriendly || (isScouted && !ch.IsHidden());
                if (!canSee) continue;

                hasVisibleCharacters = true;
                string spriteName = ch.GetOwner() != null
                    ? ch.GetOwner().GetAlignment().ToString() + "Character"
                    : "unknownCharacter";

                GameObject icon = Instantiate(spriteRendererLayoutIcon, armyCharactersIconGrid.transform);
                SpriteRendererIconManager manager = icon.GetComponent<SpriteRendererIconManager>();
                if (manager != null)
                {
                    manager.Initialize(ch, spriteName);
                }
            }

            if (armyArrangeCoroutine != null) StopCoroutine(armyArrangeCoroutine);
            armyArrangeCoroutine = StartCoroutine(DelayedArrangeArmies());
        }

        SetActiveFast(armyCharactersIconGrid != null ? armyCharactersIconGrid.gameObject : null, seen && (hasArmies || hasVisibleCharacters));
        UpdatePortIcon();

        if (refreshHoverText) RefreshHoverText();
    }

    private IEnumerator DelayedArrangeArmies()
    {
        yield return null;
        if (armyCharactersIconGrid != null) armyCharactersIconGrid.Arrange();
        armyArrangeCoroutine = null;
    }

    private void ClearArmyIcons()
    {
        if (armyCharactersIconGrid == null) return;
        if (armyArrangeCoroutine != null)
        {
            StopCoroutine(armyArrangeCoroutine);
            armyArrangeCoroutine = null;
        }
        Transform gridTransform = armyCharactersIconGrid.transform;
        for (int i = gridTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(gridTransform.GetChild(i).gameObject);
        }
    }

    public void RedrawCharacters(bool refreshHoverText = true)
    {
        bool seen = IsHexSeen();
        bool hasCharacter = false;
        for (int i = 0, n = characters.Count; i < n; i++)
        {
            if (characters[i] != null)
            {
                hasCharacter = true;
                break;
            }
        }

        SetActiveFast(characterSpriteRenderer.gameObject, seen && hasCharacter);
        if (seen && hasCharacter)
        {
            UpdateCharacterIconSprite();
            UpdateClassIcons();
        }
        else
        {
            GetCharacterAnimationController()?.Clear();
            ClearOutlineColor();
            ClearClassIcons();
        }
        // UpdateBannerSpriteForKnownCharacter();
        if (refreshHoverText) RefreshHoverText();
    }

    public void RedrawPC(bool refreshHoverText = true)
    {
        if(pc == null) return;
        if (game == null) game = FindFirstObjectByType<Game>();

        bool seen = IsHexSeen();
        PlayableLeader viewingLeader = game != null ? game.currentlyPlaying : null;
        bool isHuman = viewingLeader != null && game != null && viewingLeader == game.player;

        if (seen) RevealNonPlayableLeadersOnHex(viewingLeader, isHuman);

        bool shouldShowPc = ShouldShowPcVisual();
        ApplyHexTextureSprite();
        UpdatePortIcon(shouldShowPc);
        UpdatePcWorldText(shouldShowPc);

        if (refreshHoverText) RefreshHoverText();
    }

    public void RefreshVisibilityRendering()
    {
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
        RefreshHoverText();
    }

    public void RefreshHoverText()
    {
        bool seen = IsHexSeen();
        PlayableLeader viewer = GetPlayer();
        bool isScouted = IsScouted(viewer);
        bool viewerHasCharacter = viewer != null && HasCharacterOfLeader(viewer);
        
        sbChars.Clear();
        sbFree.Clear();
        sbDark.Clear();
        sbNeutral.Clear();
        _hexInfoCharacters.Clear();
        _hexInfoArmies.Clear();

        // Track whether we've already shown an Unknown for each bucket
        bool unkCharsShown = false;
        bool unkFreeShown = false;
        bool unkDarkShown = false;
        bool unkNeutralShown = false;

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            var ch = characters[i];
            if (ch == null || ch.killed || ch.hex != this)
            {
                continue;
            }
            bool isFriendly = IsFriendlyCharacter(ch, viewer);
            bool canSeeNonCommander = isFriendly || (isScouted && !ch.IsHidden());
            bool canSeeCommander = canSeeNonCommander || viewerHasCharacter || seen;
            bool canSee = ch.IsArmyCommander() ? canSeeCommander : canSeeNonCommander;

            if (canSee)
            {
                bool canRevealNpl = seen || isScouted;
                if (canRevealNpl && game.IsPlayerCurrentlyPlaying() && ch.GetOwner() is NonPlayableLeader && !(ch.GetOwner() as NonPlayableLeader).IsRevealedToPlayer())
                {
                    NonPlayableLeader npl = ch.GetOwner() as NonPlayableLeader;
                    npl.RevealToPlayer();
                }
                else if (canRevealNpl && ch.GetOwner() is NonPlayableLeader && !(ch.GetOwner() as NonPlayableLeader).IsRevealedToLeader(FindAnyObjectByType<Game>().currentlyPlaying))
                {
                    NonPlayableLeader npl = ch.GetOwner() as NonPlayableLeader;
                    Game g = FindAnyObjectByType<Game>();
                    bool isHuman = g != null && g.currentlyPlaying == g.player;
                    npl.RevealToLeader(g.currentlyPlaying, isHuman);
                }
                
                var charName = ch.GetHoverText(true, true, true, false, false, true);
                int linkIdx = _hexInfoCharacters.Count;
                _hexInfoCharacters.Add(ch);
                _hexInfoArmies.Add(null);
                string linkedName = $"<link=\"{linkIdx}\"><color=#FFFFFF>{charName}</color></link>";
                if (ch.IsArmyCommander())
                {
                    Army army = ch.GetArmy();
                    var armyText = army != null ? army.GetHoverText() : ch.GetHoverText(false, false, false, true, false, false);
                    switch (ch.alignment)
                    {
                        case AlignmentEnum.freePeople: sbFree.Append(armyText).Append('\n'); break;
                        case AlignmentEnum.neutral: sbNeutral.Append(armyText).Append('\n'); break;
                        case AlignmentEnum.darkServants: sbDark.Append(armyText).Append('\n'); break;
                    }
                    int armyLinkIdx = _hexInfoCharacters.Count;
                    _hexInfoCharacters.Add(ch);
                    _hexInfoArmies.Add(army);
                    string armyDisplay = army != null ? army.GetHoverTextHexInfo() : $"\n\t{armyText.Trim()}";
                    string linkedArmy = $"<link=\"{armyLinkIdx}\"><color=#FFFFFF>{armyDisplay}</color></link>";
                    sbChars.Append(linkedName).Append(linkedArmy).Append('\n');
                }
                else
                {
                    sbChars.Append(linkedName).Append('\n');
                }
            }
            else
            {
                if (ch.IsArmyCommander())
                {
                    switch (ch.alignment)
                    {
                        case AlignmentEnum.freePeople:
                            if (!unkFreeShown) { sbFree.Append(Unknown).Append('\n'); unkFreeShown = true; }
                            break;
                        case AlignmentEnum.neutral:
                            if (!unkNeutralShown) { sbNeutral.Append(Unknown).Append('\n'); unkNeutralShown = true; }
                            break;
                        case AlignmentEnum.darkServants:
                            if (!unkDarkShown) { sbDark.Append(Unknown).Append('\n'); unkDarkShown = true; }
                            break;
                    }
                    if (!unkCharsShown) { sbChars.Append(Unknown).Append('\n'); unkCharsShown = true; }
                }
                else
                {
                    if (!unkCharsShown) { sbChars.Append(Unknown).Append('\n'); unkCharsShown = true; }
                }
            }
        }

        // Trim trailing newlines and always push an explicit refresh, even when the hex is empty.
        string hoverText = sbChars.ToString().TrimEnd('\n');
        if (hexInfoText != null) hexInfoText.text = hoverText;
    }


    public void Hover()
    {
        if (BoardNavigator.IsPointerOverVisibleUIElement())
        {
            Unhover();
            return;
        }

        if (!IsHexSeen())
        {
            Unhover();
            return;
        }

        if (s_hexInfoActiveHex != null && s_hexInfoActiveHex != this && s_hexInfoActiveHex.IsMouseOverHexOrPanel())
            return;

        if (hexInfoShowCoroutine == null && (hexInfo == null || !hexInfo.activeSelf))
            hexInfoShowCoroutine = StartCoroutine(ShowHexInfoAfterDelay());

        if (isSelected)
        {
            SetActiveFast(hoverHexFrame, false);
            return;
        }

        SetActiveFast(hoverHexFrame, true);
    }

    private IEnumerator ShowHexInfoAfterDelay()
    {
        yield return new WaitForSeconds(hexInfoHoverDelay);
        bool hasText = hexInfoText != null && !string.IsNullOrWhiteSpace(hexInfoText.text);
        SetActiveFast(hexInfoArrow, hasText);
        SetActiveFast(hexInfo, hasText);
        if (hasText)
        {
            s_hexInfoActiveHex = this;
            StartArrowBounce();
        }
        hexInfoShowCoroutine = null;
    }

    private void StartArrowBounce()
    {
        if (_arrowBounceCoroutine != null) StopCoroutine(_arrowBounceCoroutine);
        if (hexInfoArrow == null) return;
        _arrowOriginX = hexInfoArrow.transform.localPosition.x;
        _arrowBounceCoroutine = StartCoroutine(ArrowBounceCoroutine());
    }

    private void StopArrowBounce()
    {
        if (_arrowBounceCoroutine == null) return;
        StopCoroutine(_arrowBounceCoroutine);
        _arrowBounceCoroutine = null;
        if (hexInfoArrow != null)
        {
            Vector3 p = hexInfoArrow.transform.localPosition;
            p.x = _arrowOriginX;
            hexInfoArrow.transform.localPosition = p;
        }
    }

    private IEnumerator ArrowBounceCoroutine()
    {
        Transform t = hexInfoArrow.transform;
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float t01 = Mathf.PingPong(elapsed, 1f);
            t01 = t01 * t01 * (3f - 2f * t01); // smoothstep
            Vector3 p = t.localPosition;
            p.x = Mathf.Lerp(_arrowOriginX, 0.2f, t01);
            t.localPosition = p;
            yield return null;
        }
    }

    public void Unhover()
    {
        SetActiveFast(hoverHexFrame, false);
        if (IsMouseOverHexOrPanel()) return;
        if (hexInfoShowCoroutine != null) { StopCoroutine(hexInfoShowCoroutine); hexInfoShowCoroutine = null; }
        if (_lastHexInfoLinkIdx >= 0) ApplyHexInfoLinkHighlight(-1);
        _lastHexInfoLinkIdx = -1;
        StopArrowBounce();
        SetActiveFast(hexInfoArrow, false);
        SetActiveFast(hexInfo, false);
        if (s_hexInfoActiveHex == this) s_hexInfoActiveHex = null;
    }

    private void UpdateHexInfoLinkHover()
    {
        if (hexInfoText == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        int linkIdx = GetHoveredHexInfoLinkIndex(cam);
        if (linkIdx == _lastHexInfoLinkIdx) return;
        _lastHexInfoLinkIdx = linkIdx;
        ApplyHexInfoLinkHighlight(linkIdx);

        if (_selectedIcon == null) _selectedIcon = FindFirstObjectByType<SelectedCharacterIcon>();
        if (_selectedIcon == null) return;

        if (linkIdx >= 0 && linkIdx < _hexInfoCharacters.Count)
        {
            Army army = linkIdx < _hexInfoArmies.Count ? _hexInfoArmies[linkIdx] : null;
            if (army != null)
            {
                _selectedIcon.RefreshForArmy(army);
            }
            else
            {
                Character ch = _hexInfoCharacters[linkIdx];
                if (ch != null && !ch.killed)
                {
                    TryGetPreviewTextForCharacter(ch, out string hoverText);
                    bool isScouted = IsScouted();
                    _selectedIcon.RefreshHoverPreview(ch, hoverText, isScouted, isScouted);
                }
            }
        }
        else
        {
            if (board != null && board.selectedCharacter != null)
                _selectedIcon.Refresh(board.selectedCharacter);
            else
                _selectedIcon.Hide();
        }
    }

    private void HandleHexInfoLinkClick(int linkIdx)
    {
        if (linkIdx < 0 || linkIdx >= _hexInfoCharacters.Count) return;
        Character ch = _hexInfoCharacters[linkIdx];
        if (ch == null || ch.killed) return;
        if (board == null) board = FindFirstObjectByType<Board>();
        if (board != null) board.SelectCharacter(ch);
    }

    private static readonly Color32 LinkColorDefault = new(0xFF, 0xFF, 0xFF, 0xFF);
    private static readonly Color32 LinkColorHover   = new(0xFF, 0xD7, 0x00, 0xFF);

    private void ApplyHexInfoLinkHighlight(int hoveredLinkIdx)
    {
        if (hexInfoText == null) return;
        hexInfoText.ForceMeshUpdate();
        TMP_TextInfo ti = hexInfoText.textInfo;

        for (int i = 0; i < ti.linkCount; i++)
        {
            Color32 col = i == hoveredLinkIdx ? LinkColorHover : LinkColorDefault;
            TMP_LinkInfo link = ti.linkInfo[i];
            for (int j = 0; j < link.linkTextLength; j++)
            {
                int ci = link.linkTextfirstCharacterIndex + j;
                if (ci >= ti.characterCount) break;
                TMP_CharacterInfo ch = ti.characterInfo[ci];
                if (!ch.isVisible) continue;
                int vi = ch.vertexIndex;
                int mi = ch.materialReferenceIndex;
                ti.meshInfo[mi].colors32[vi]     = col;
                ti.meshInfo[mi].colors32[vi + 1] = col;
                ti.meshInfo[mi].colors32[vi + 2] = col;
                ti.meshInfo[mi].colors32[vi + 3] = col;
            }
        }
        hexInfoText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private int GetHoveredHexInfoLinkIndex(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane textPlane = new Plane(hexInfoText.transform.forward, hexInfoText.transform.position);
        if (!textPlane.Raycast(ray, out float dist)) return -1;

        Vector3 localHit = hexInfoText.transform.InverseTransformPoint(ray.GetPoint(dist));
        TMP_TextInfo info = hexInfoText.textInfo;
        for (int i = 0; i < info.linkCount; i++)
        {
            TMP_LinkInfo link = info.linkInfo[i];
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool hasVisible = false;
            for (int j = 0; j < link.linkTextLength; j++)
            {
                int ci = link.linkTextfirstCharacterIndex + j;
                if (ci >= info.characterCount) break;
                TMP_CharacterInfo ch = info.characterInfo[ci];
                if (!ch.isVisible) continue;
                hasVisible = true;
                minX = Mathf.Min(minX, ch.bottomLeft.x);
                maxX = Mathf.Max(maxX, ch.topRight.x);
                minY = Mathf.Min(minY, ch.bottomLeft.y);
                maxY = Mathf.Max(maxY, ch.topRight.y);
            }
            if (hasVisible &&
                localHit.x >= minX && localHit.x <= maxX &&
                localHit.y >= minY && localHit.y <= maxY)
                return i;
        }
        return -1;
    }

    private bool IsMouseOverHexOrPanel()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        // 3D raycast — hex uses BoxCollider (not Collider2D)
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var hits3d = Physics.RaycastAll(ray);
        for (int i = 0; i < hits3d.Length; i++)
        {
            Transform t = hits3d[i].transform;
            while (t != null)
            {
                if (t == transform) return true;
                t = t.parent;
            }
        }

        // Check hexInfo and hexInfoArrow sprite bounds (no collider required)
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(
            Input.mousePosition.x, Input.mousePosition.y,
            transform.position.z - cam.transform.position.z));
        if (IsMouseOverSprites(hexInfo, mouseWorld)) return true;
        if (IsMouseOverSprites(hexInfoArrow, mouseWorld)) return true;

        return false;
    }

    private static bool IsMouseOverSprites(GameObject go, Vector3 mouseWorld)
    {
        if (go == null || !go.activeSelf) return false;
        var renderers = go.GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds b = renderers[i].bounds;
            if (mouseWorld.x >= b.min.x && mouseWorld.x <= b.max.x &&
                mouseWorld.y >= b.min.y && mouseWorld.y <= b.max.y)
                return true;
        }
        return false;
    }

    public void Select(bool lookAt = true, float duration = 1.0f, float delay = 0.0f)
    {
        if (!IsHidden())
        {
            SetActiveFast(hoverHexFrame, false);
            isSelected = true;
            if (lookAt) LookAt(duration, delay);
        }
    }

    public void Unselect()
    {
        isSelected = false;
        SetActiveFast(hoverHexFrame, false);
    }

    public void LookAt(float duration = 1.0f, float delay = 0.0f)
    {
        if (!game.IsPlayerCurrentlyPlaying()) return;
        if (!IsHexSeen()) return;
        // Avoid GameObject.Find/string allocs; use our own transform
        if (navigator == null) navigator = FindFirstObjectByType<BoardNavigator>();
        if (navigator != null) navigator.LookAt(transform.position, duration, delay);
    }

    public bool HasCharacter(Character c) => characters.Contains(c);

    public bool HasPcOfLeader(Leader c)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return false;
        return pc.owner == c;
    }

    public bool HasArmyOfLeader(Leader c)
    {
        for (int i = 0, n = armies.Count; i < n; i++)
        {
            var cmd = armies[i].GetCommander();
            if (cmd == c || cmd.GetOwner() == c) return true;
        }
        return false;
    }

    public bool HasCharacterOfLeader(Leader c)
    {
        for (int i = 0, n = characters.Count; i < n; i++)
        {
            var ch = characters[i];
            if (ch == c || ch.GetOwner() == c) return true;
        }
        return false;
    }

    public bool LeaderSeesHex(Leader c) => HasArmyOfLeader(c) || HasPcOfLeader(c) || HasCharacterOfLeader(c);

    public void Reveal(Leader scoutedByPlayer = null)
    {
        RevealInternal(scoutedByPlayer, game.IsPlayerCurrentlyPlaying());
    }

    public void RevealPC()
    {
        if (pc == null || pc.IsRevealed()) return;
        pc.Reveal();
    }

    public void Unreveal(Leader unrevealedPlayer = null)
    {
        if (unrevealedPlayer)
        {
            AlignmentEnum unreleavedPlayerAlignment = unrevealedPlayer.GetAlignment();
            List<Leader> toRemove = scoutedBy
                .Where(ch => ch != null && ch.GetAlignment() != unreleavedPlayerAlignment)
                .ToList();
            for (int i = 0; i < toRemove.Count; i++)
            {
                Leader leader = toRemove[i];
                scoutedBy.Remove(leader);
                scoutedByTurns.Remove(leader);
            }
            RebuildScoutingCache();
        }
        isRevealed = true;
        if (!mapOnlyRevealed && game.currentlyPlaying == game.player && characters.Find(x => x.GetOwner() == game.player) == null)
        {
            if (IsHexRevealed()) isCurrentlyUnseen = true;
        }
        UpdateMinimapTerrain(IsHexRevealed());
        
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
        RefreshHoverText();
    }

    public void Obscure(Leader obscuredBy = null)
    {
        Unreveal(obscuredBy);
        isRevealed = false;
        mapOnlyRevealed = false;
        isCurrentlyUnseen = false;
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
        RefreshHoverText();
    }

    public void RevealArea(int radius = 1, bool lookAt = true, Leader scoutedByPlayer = null)
    {
        if (board == null) board = FindFirstObjectByType<Board>();

        bool isPlayerTurn = game.IsPlayerCurrentlyPlaying();
        RevealInternal(scoutedByPlayer, isPlayerTurn);
        if (radius <= 0 || board == null) { if (isPlayerTurn && lookAt) LookAt(); return; }

        var queue = areaQueue;
        var visited = areaVisited;
        queue.Clear();
        visited.Clear();
        queue.Enqueue(v2);
        visited.Add(v2);

        int currentRadius = 0;
        while (queue.Count > 0 && currentRadius < radius)
        {
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                var currentHex = queue.Dequeue();
                var neighbors = ((currentHex.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                for (int j = 0; j < neighbors.Length; j++)
                {
                    var offset = neighbors[j];
                    var neighborPos = new Vector2Int(currentHex.x + offset.x, currentHex.y + offset.y);
                    if (!visited.Add(neighborPos)) continue;

                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        neighborHex.RevealInternal(scoutedByPlayer, isPlayerTurn);
                        queue.Enqueue(neighborPos);
                    }
                }
            }
            currentRadius++;
        }
        
        if(game.IsPlayerCurrentlyPlaying()) {
            if (lookAt) LookAt();
            MinimapManager.RefreshMinimap();
        }
    }

    public void RevealMapOnlyArea(int radius = 1, bool lookAt = true, bool refreshMinimap = true)
    {
        if (board == null) board = FindFirstObjectByType<Board>();

        RevealMapOnlyInternal();
        if (radius <= 0 || board == null)
        {
            if (game.IsPlayerCurrentlyPlaying() && lookAt) LookAt();
            return;
        }

        var queue = areaQueue;
        var visited = areaVisited;
        queue.Clear();
        visited.Clear();
        queue.Enqueue(v2);
        visited.Add(v2);

        int currentRadius = 0;
        while (queue.Count > 0 && currentRadius < radius)
        {
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                var currentHex = queue.Dequeue();
                var neighbors = ((currentHex.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                for (int j = 0; j < neighbors.Length; j++)
                {
                    var offset = neighbors[j];
                    var neighborPos = new Vector2Int(currentHex.x + offset.x, currentHex.y + offset.y);
                    if (!visited.Add(neighborPos)) continue;

                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        neighborHex.RevealMapOnlyInternal();
                        queue.Enqueue(neighborPos);
                    }
                }
            }
            currentRadius++;
        }

        if (game.IsPlayerCurrentlyPlaying())
        {
            if (lookAt) LookAt();
            if (refreshMinimap) MinimapManager.RefreshMinimap();
        }
    }

    private void RevealNonPlayableLeadersOnHex(PlayableLeader leader, bool showPopup)
    {
        if (leader == null) return;
        if (game == null) game = FindFirstObjectByType<Game>();

        if (!IsHexSeen()) return;
        bool isScouted = IsScouted(leader);
        NonPlayableLeader npl = null;

        if (pc != null && pc.owner is NonPlayableLeader pcOwner && pc.IsRevealed(leader) && !pcOwner.IsRevealedToLeader(leader))
        {
            npl = pcOwner;
        }

        if (npl == null && characters != null)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                Character ch = characters[i];
                if (ch == null) continue;
                if (ch.GetOwner() is not NonPlayableLeader owner) continue;
                if (owner.IsRevealedToLeader(leader)) continue;
                bool canReveal = !ch.IsHidden() && (ch.IsArmyCommander() || isScouted);
                if (!canReveal) continue;
                npl = owner;
                break;
            }
        }

        if (npl != null)
        {
            bool shouldPopup = showPopup && leader == game.player && game.currentlyPlaying == leader;
            npl.RevealToLeader(leader, shouldPopup);
            return;
        }

        if (showPopup && leader == game.player && game.currentlyPlaying == leader)
        {
            NonPlayableLeader pending = null;
            if (pc != null && pc.owner is NonPlayableLeader pendingOwner && pc.IsRevealed(leader) && pendingOwner.ShouldShowPlayerRevealPopup())
            {
                pending = pendingOwner;
            }
            if (pending == null && characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    Character ch = characters[i];
                    if (ch == null) continue;
                    if (ch.GetOwner() is not NonPlayableLeader owner) continue;
                    if (!owner.ShouldShowPlayerRevealPopup()) continue;
                    bool canReveal = !ch.IsHidden() && (ch.IsArmyCommander() || isScouted);
                    if (!canReveal) continue;
                    pending = owner;
                    break;
                }
            }
            if (pending != null)
            {
                pending.RevealToLeader(leader, true);
            }
        }
    }

    private void SetHexSpriteAlpha(float alpha)
    {
        SetSpriteAlpha(terrainTexture, alpha);
    }

    private static void SetSpriteAlpha(SpriteRenderer sr, float alpha)
    {
        if (!sr) return;
        var c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    private void ApplyCharacterOutlineMaterial()
    {
        if (!characterSpriteRenderer) return;

        if (sharedCharacterOutlineMaterial == null)
        {
            sharedCharacterOutlineMaterial = Resources.Load<Material>(CharacterOutlineMaterialPath);
            if (sharedCharacterOutlineMaterial == null)
            {
                Debug.LogWarning($"Hex could not load character outline material at Resources/{CharacterOutlineMaterialPath}.");
                return;
            }
        }

        if (characterSpriteRenderer.sharedMaterial != sharedCharacterOutlineMaterial)
        {
            characterSpriteRenderer.sharedMaterial = sharedCharacterOutlineMaterial;
        }

        // Banner outline material loading commented out
        /*
        if (sharedBannerOutlineMaterial == null)
        {
            sharedBannerOutlineMaterial = Resources.Load<Material>(BannerOutlineMaterialPath);
            if (sharedBannerOutlineMaterial == null)
            {
                Debug.LogWarning($"Hex could not load banner outline material at Resources/{BannerOutlineMaterialPath}.");
                return;
            }
        }

        if (bannerSpriteRenderer && bannerSpriteRenderer.sharedMaterial != sharedBannerOutlineMaterial)
        {
            bannerSpriteRenderer.sharedMaterial = sharedBannerOutlineMaterial;
        }
        */
    }

    private void UpdateMinimapTerrain(bool revealed)
    {
        /* Terrain or None logic commented out — using hexRegion only.
        if (!terrainOrNoneMinimapTexture) return;
        terrainOrNoneMinimapTexture.sprite = terrainTexture ? terrainTexture.sprite : null;
        if (revealed)
        {
            SetSpriteAlpha(terrainOrNoneMinimapTexture, isCurrentlyUnseen ? 0.1f : 1f);
        }
        else
        {
            SetSpriteAlpha(terrainOrNoneMinimapTexture, 0f);
        }
        */
    }

    private void UpdateVisibilityForFog()
    {
        bool revealed = IsHexRevealed();
        bool seen = IsHexSeen();
        ApplyRegionColor();
        if (terrainTexture != null) SetActiveFast(terrainTexture.gameObject, revealed);
        UpdateTerrainVisualAlpha();
        if (revealed)
        {
            SetActiveFast(hoverHexFrame, false);
            UpdateArtifactVisibility();
            UpdateParticles();
            RefreshFrontierRowVisuals();
            UpdatePcWorldText(ShouldShowPcVisual());
            return;
        }

        SetActiveFast(armyCharactersIconGrid != null ? armyCharactersIconGrid.gameObject : null, false);
        SetActiveFast(characterSpriteRenderer.gameObject, false);
        SetActiveFast(artifact, false);
        if (artifactHover) SetActiveFast(artifactHover.gameObject, false);

        SetActiveFast(movement, false);
        SetActiveFast(hoverHexFrame, false);
        if (sharedSelectedParticlesOwner == this)
        {
            SetSharedSelectedParticlesActive(false);
        }
        StopSharedOneShotParticlesOnThisHex();
        SetActiveFast(scoutedHexFrame, false);
        SetActiveFast(darknessHexFrame, false);

        UpdatePcWorldText(false);

        UpdateParticles();
        RefreshFrontierRowVisuals();
    }

    private bool ShouldShowPcPort()
    {
        if (pc == null || !pc.hasPort) return false;
        bool seen = IsHexSeen();
        if (!seen) return false;
        if (game == null) game = FindFirstObjectByType<Game>();

        PlayableLeader viewingLeader = game != null ? game.currentlyPlaying : null;
        bool ownerIsNonPlayableLeader = pc.owner is NonPlayableLeader;
        bool nplKnownByViewer = ownerIsNonPlayableLeader && viewingLeader != null && (pc.owner as NonPlayableLeader).IsRevealedToLeader(viewingLeader);
        bool pcRevealed = IsPCRevealed();
        return pcRevealed || (ownerIsNonPlayableLeader && nplKnownByViewer);
    }

    private void UpdatePortIcon(bool? shouldShowPcOverride = null)
    {
        return;
    }

    private void UpdateCharacterIconSprite()
    {
        if (characterSpriteRenderer == null) return;

        if (TryGetKnownCharacterForIcon(out Character known))
        {
            CharacterAnimationController animationController = GetCharacterAnimationController();
            if (animationController != null && animationController.Show(known))
            {
                // The animator now drives the sprite renderer's sprite each frame.
                UpdateOutlineColor(known);
                return;
            }

            Sprite sprite = null;
            if (illustrations != null)
            {
                Leader knownAsLeader = known as Leader;
                LeaderBiomeConfig knownBiome = knownAsLeader != null ? knownAsLeader.GetBiome() : null;
                string spriteName = knownBiome?.characterSprite;
                if (!string.IsNullOrEmpty(spriteName))
                    sprite = illustrations.GetIllustrationByName(spriteName, false);
                if (sprite == null)
                    sprite = illustrations.GetIllustrationByName(known.race.ToString(), false);
            }
            characterSpriteRenderer.sprite = sprite != null ? sprite : defaultCharacterSprite;
            UpdateOutlineColor(known);
        }
        else
        {
            GetCharacterAnimationController()?.Clear();
            characterSpriteRenderer.sprite = defaultCharacterSprite;
            ClearOutlineColor();
        }
    }

    private CharacterAnimationController GetCharacterAnimationController()
    {
        if (characterAnimationController == null && characterSpriteRenderer != null)
        {
            characterAnimationController = characterSpriteRenderer.GetComponent<CharacterAnimationController>();
            if (characterAnimationController == null)
                characterAnimationController = characterSpriteRenderer.gameObject.AddComponent<CharacterAnimationController>();
        }
        return characterAnimationController;
    }

    public void PlayCharacterActionAnimation(Character character)
    {
        if (character == null) return;
        if (characterSpriteRenderer == null || !characterSpriteRenderer.gameObject.activeInHierarchy) return;
        GetCharacterAnimationController()?.PlayAction(character);
    }

    private void UpdateClassIcons()
    {
        ClearClassIcons();

        if (characterClassesIconGrid == null || spriteRendererLayoutIcon == null) return;
        if (!TryGetKnownCharacterForIcon(out Character known)) return;

        void AddClassIcon(string className, int level)
        {
            if (level <= 0) return;
            GameObject icon = Instantiate(spriteRendererLayoutIcon, characterClassesIconGrid.transform);
            SpriteRendererIconManager manager = icon.GetComponent<SpriteRendererIconManager>();
            if (manager != null)
            {
                Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(className, false) : null;
                if (manager.armySprite != null) manager.armySprite.sprite = sprite;
                if (manager.nationText != null) manager.nationText.text = level.ToString();
            }
        }

        AddClassIcon("commander", known.GetCommander());
        AddClassIcon("agent", known.GetAgent());
        AddClassIcon("emmissary", known.GetEmmissary());
        AddClassIcon("mage", known.GetMage());

        if (classArrangeCoroutine != null) StopCoroutine(classArrangeCoroutine);
        classArrangeCoroutine = StartCoroutine(DelayedArrangeClasses());

        SetActiveFast(characterClassesIconGrid.gameObject, true);
    }

    private void ClearClassIcons()
    {
        if (characterClassesIconGrid == null) return;
        if (classArrangeCoroutine != null)
        {
            StopCoroutine(classArrangeCoroutine);
            classArrangeCoroutine = null;
        }
        Transform gridTransform = characterClassesIconGrid.transform;
        for (int i = gridTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(gridTransform.GetChild(i).gameObject);
        }
        SetActiveFast(characterClassesIconGrid.gameObject, false);
    }

    private IEnumerator DelayedArrangeClasses()
    {
        yield return null;
        if (characterClassesIconGrid != null) characterClassesIconGrid.Arrange();
        classArrangeCoroutine = null;
    }

    /*private void UpdateBannerSpriteForKnownCharacter()
    {
        if (bannerSpriteRenderer == null)
        {
            return;
        }

        if (!IsHexSeen())
        {
            ClearBannerSprite();
            return;
        }

        if (!TryGetKnownCharacterForBanner(out Character known))
        {
            ClearBannerSprite();
            return;
        }

        UpdateBannerSprite(known);
    }*/

    /*private bool TryGetKnownCharacterForBanner(out Character known)
    {
        known = null;
        if (TryGetKnownCharacterForIcon(out known))
        {
            return true;
        }

        if (board == null) board = FindFirstObjectByType<Board>();

        PlayableLeader player = GetPlayer();
        bool isScouted = IsScouted(player);
        Character selected = board != null ? board.selectedCharacter : null;

        if (selected != null && selected.hex == this &&
            (isScouted || IsFriendlyCharacter(selected, player) || selected.GetOwner() == player))
        {
            known = selected;
            return true;
        }

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            Character candidate = characters[i];
            if (candidate == null || candidate.killed || candidate.hex != this) continue;
            if (IsFriendlyCharacter(candidate, player) || candidate.GetOwner() == player)
            {
                known = candidate;
                return true;
            }
            if (isScouted && candidate.GetOwner() != null)
            {
                known = candidate;
                return true;
            }
        }

        return false;
    }*/

    /*private void UpdateBannerSprite(Character character)
    {
        if (bannerSpriteRenderer == null) return;

        if (character == null)
        {
            ClearBannerSprite();
            // ClearBannerOutline();
            return;
        }

        Leader owner = character.GetOwner();
        string bannerName = ResolveBannerName(owner);
        if (string.IsNullOrWhiteSpace(bannerName))
        {
            ClearBannerSprite();
            return;
        }

        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        if (illustrations == null)
        {
            ClearBannerSprite();
            // ClearBannerOutline();
            return;
        }

        if (!illustrations.IsLoaded)
        {
            QueueBannerRetry();
            ClearBannerSprite();
            ClearBannerOutline();
            return;
        }

        Sprite ownerBannerSprite = illustrations != null ? illustrations.GetIllustrationByName(bannerName, false) : null;
        if (ownerBannerSprite == null)
        {
            ClearBannerSprite();
            ClearBannerOutline();
            return;
        }

        if (bannerSpriteRenderer.sprite != ownerBannerSprite)
        {
            bannerSpriteRenderer.sprite = ownerBannerSprite;
        }
        SetActiveFast(bannerSpriteRenderer.gameObject, true);
        UpdateBannerOutline(character.GetOwner());
        CancelBannerRetry();
    }*/

    /*private void ClearBannerSprite()
    {
        if (bannerSpriteRenderer == null)
        {
            return;
        }

        SetActiveFast(bannerSpriteRenderer.gameObject, false);
    }*/

    /*private void UpdateBannerOutline(Leader owner)
    {
        if (!bannerSpriteRenderer) return;
        ApplyOutlineSettings(
            bannerSpriteRenderer,
            owner != null ? owner.nationColor : Color.white,
            bannerOutlineSize,
            isBanner: true);
    }*/

    /*private void ClearBannerOutline()
    {
        if (!bannerSpriteRenderer) return;
        ApplyOutlineSettings(bannerSpriteRenderer, Color.white, bannerOutlineSize, isBanner: true);
    }*/

    /*private void QueueBannerRetry()
    {
        if (bannerRetryCoroutine != null)
        {
            return;
        }

        bannerRetryCoroutine = StartCoroutine(RetryBannerWhenIllustrationsReady());
    }*/

    /*private void CancelBannerRetry()
    {
        if (bannerRetryCoroutine == null)
        {
            return;
        }

        StopCoroutine(bannerRetryCoroutine);
        bannerRetryCoroutine = null;
    }*/

    /*private IEnumerator RetryBannerWhenIllustrationsReady()
    {
        while (illustrations == null || !illustrations.IsLoaded)
        {
            if (illustrations == null)
            {
                illustrations = FindFirstObjectByType<Illustrations>();
            }
            yield return null;
        }

        bannerRetryCoroutine = null;

        if (!this || !gameObject.activeInHierarchy)
        {
            yield break;
        }

        RedrawCharacters(false);
    }*/

    /*private static string ResolveBannerName(Leader owner)
    {
        if (owner == null)
        {
            return null;
        }

        LeaderBiomeConfig biome = owner.GetBiome();
        if (biome == null)
        {
            return null;
        }

        if (owner is PlayableLeader playableLeader)
        {
            string selectedSubdeckId = playableLeader.GetSelectedSubdeckId();
            if (!string.IsNullOrWhiteSpace(selectedSubdeckId) && biome.variants != null)
            {
                LeaderVariantConfig variant = biome.variants.Find(entry =>
                    entry != null
                    && ((!string.IsNullOrWhiteSpace(entry.variantId) && string.Equals(entry.variantId, selectedSubdeckId, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(entry.subdeckId) && string.Equals(entry.subdeckId, selectedSubdeckId, StringComparison.OrdinalIgnoreCase))));

                if (!string.IsNullOrWhiteSpace(variant?.banner))
                {
                    return variant.banner;
                }
            }
        }

        return biome.banner;
    }*/

    private void UpdateOutlineColor(Character character)
    {
        Leader owner = character != null ? character.GetOwner() : null;
        if (owner == null)
        {
            ClearOutlineColor();
            // ClearBannerOutline();
            return;
        }
        ApplyOutlineColorFromBanner(null, owner);
        // UpdateBannerOutline(owner);
    }

    private void ApplyOutlineColorFromBanner(Sprite bannerSprite, Leader owner)
    {
        ApplyOutlineSettings(characterSpriteRenderer, owner != null ? owner.nationColor : Color.white, characterOutlineSize, isBanner: false);
        // if (bannerSpriteRenderer)
        // {
        //     ApplyOutlineSettings(bannerSpriteRenderer, owner != null ? owner.nationColor : Color.white, bannerOutlineSize, isBanner: true);
        // }
    }

    private void ClearOutlineColor()
    {
        ApplyOutlineSettings(characterSpriteRenderer, Color.white, characterOutlineSize, isBanner: false);
    }

    private void ApplyOutlineSettings(SpriteRenderer spriteRenderer, Color outlineColor, float outlineSize, bool isBanner)
    {
        if (!spriteRenderer) return;

        Color? lastColor = isBanner ? /*lastAppliedBannerOutlineColor*/ null : lastAppliedCharacterOutlineColor;
        float? lastSize = isBanner ? /*lastAppliedBannerOutlineSize*/ null : lastAppliedCharacterOutlineSize;

        bool colorChanged = !lastColor.HasValue || lastColor.Value != outlineColor;
        bool sizeChanged = !lastSize.HasValue || !Mathf.Approximately(lastSize.Value, outlineSize);
        if (!colorChanged && !sizeChanged) return;

        // if (isBanner)
        // {
        //     lastAppliedBannerOutlineColor = outlineColor;
        //     lastAppliedBannerOutlineSize = outlineSize;
        // }
        // else
        {
            lastAppliedCharacterOutlineColor = outlineColor;
            lastAppliedCharacterOutlineSize = outlineSize;
        }

        if (characterOutlinePropertyBlock == null)
        {
            characterOutlinePropertyBlock = new MaterialPropertyBlock();
        }

        spriteRenderer.GetPropertyBlock(characterOutlinePropertyBlock);
        characterOutlinePropertyBlock.SetColor(OutlineColorShaderId, outlineColor);
        characterOutlinePropertyBlock.SetFloat(OutlineSizeShaderId, outlineSize);
        spriteRenderer.SetPropertyBlock(characterOutlinePropertyBlock);
    }

    private static readonly Dictionary<Sprite, Color> dominantColorCache = new();

    /*private static bool TryGetDominantBannerColor(Sprite bannerSprite, out Color dominantColor)
    {
        dominantColor = Color.white;
        if (bannerSprite == null)
        {
            return false;
        }

        if (dominantColorCache.TryGetValue(bannerSprite, out Color cachedColor))
        {
            dominantColor = cachedColor;
            return true;
        }

        if (bannerSprite.texture == null)
        {
            return false;
        }

        try
        {
            Texture2D texture = bannerSprite.texture;
            Rect rect = bannerSprite.textureRect;
            int startX = Mathf.RoundToInt(rect.x);
            int startY = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            Color[] pixels = texture.GetPixels(startX, startY, width, height);
            Dictionary<int, (Vector3 sum, int count)> buckets = new();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.a < 0.2f) continue;

                Color.RGBToHSV(pixel, out float hue, out float saturation, out float value);
                if (saturation < 0.3f) continue;
                if (value < 0.2f || value > 0.95f) continue;

                int hueBucket = Mathf.Clamp(Mathf.FloorToInt(hue * 12f), 0, 11);
                int satBucket = Mathf.Clamp(Mathf.FloorToInt(saturation * 4f), 0, 3);
                int valBucket = Mathf.Clamp(Mathf.FloorToInt(value * 4f), 0, 3);
                int key = hueBucket | (satBucket << 8) | (valBucket << 16);

                if (buckets.TryGetValue(key, out var bucket))
                {
                    bucket.sum += new Vector3(pixel.r, pixel.g, pixel.b);
                    bucket.count++;
                    buckets[key] = bucket;
                }
                else
                {
                    buckets[key] = (new Vector3(pixel.r, pixel.g, pixel.b), 1);
                }
            }

            if (buckets.Count == 0)
            {
                dominantColorCache[bannerSprite] = Color.white;
                return false;
            }

            KeyValuePair<int, (Vector3 sum, int count)> bestBucket = buckets.OrderByDescending(entry => entry.Value.count).First();
            Vector3 average = bestBucket.Value.sum / Mathf.Max(1, bestBucket.Value.count);
            dominantColor = new Color(average.x, average.y, average.z, 1f);
            dominantColorCache[bannerSprite] = dominantColor;
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }*/


    /*private void UpdateCharacterIconZoom(Sprite sprite)
    {
        if (characterIconZoom == null && characterIcon != null)
        {
            characterIconZoom = characterIcon.GetComponent<ZoomSpriteRenderer>();
            if (characterIconZoom != null)
            {
                characterIconZoomDefault = characterIconZoom.zoomFactor;
                characterIconOffsetDefault = characterIconZoom.verticalOffset;
            }
        }
        if (characterIconZoom == null) return;

        bool useZoom = sprite != null && sprite != defaultCharacterSprite;
        if (useZoom)
        {
            characterIconZoom.zoomFactor = characterIconZoomDefault;
            characterIconZoom.verticalOffset = characterIconOffsetDefault;
        }
        else
        {
            characterIconZoom.zoomFactor = 1f;
            characterIconZoom.verticalOffset = 0f;
        }
        characterIconZoom.Refresh();
        characterIconZoom.enabled = useZoom;
    }
*/
    private void RevealInternal(Leader scoutedByPlayer, bool isPlayerTurn)
    {
        bool wasSeen = IsHexSeen();
        isRevealed = true;
        mapOnlyRevealed = false;
        if (scoutedByPlayer)
        {
            scoutedByTurns[scoutedByPlayer] = Math.Max(2, scoutedByTurns.TryGetValue(scoutedByPlayer, out int current) ? current : 0);
            scoutedBy.Add(scoutedByPlayer);
        }
        if (isPlayerTurn) isCurrentlyUnseen = false;
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        PlayableLeader viewer = scoutedByPlayer as PlayableLeader;
        if (viewer == null && game != null) viewer = game.currentlyPlaying;
        var g = game ?? FindFirstObjectByType<Game>();
        bool showPopup = viewer != null && g != null && viewer == g.player && isPlayerTurn;
        RevealNonPlayableLeadersOnHex(viewer, showPopup);
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
        RefreshHoverText();
        if (!wasSeen)
        {
            PlayRevealPulse();
        }
    }

    private void RevealMapOnlyInternal()
    {
        bool wasSeen = IsHexSeen();
        if (wasSeen)
        {
            return;
        }

        isRevealed = true;
        mapOnlyRevealed = true;
        isCurrentlyUnseen = !(game != null && game.player != null && game.player.LeaderSeesHex(this));
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
        RefreshHoverText();
        PlayRevealPulse();
    }

    private void PlayRevealPulse()
    {
        if (terrainTexture == null) return;

        if (revealPulseCoroutine != null)
        {
            StopCoroutine(revealPulseCoroutine);
        }

        if (hexRegion != null) hexRegion.enabled = false;
        revealPulseCoroutine = StartCoroutine(AnimateRevealPulse());
    }

    private IEnumerator AnimateRevealPulse()
    {
        if (terrainTexture == null)
        {
            revealPulseCoroutine = null;
            yield break;
        }

        Transform terrainTransform = terrainTexture.transform;
        if (!terrainBaseScaleCaptured)
        {
            // capture the prefab-defined scale before the first pulse touches
            // it, so interrupted pulses can't bake a mid-animation value
            terrainBaseScale = terrainTransform.localScale;
            terrainBaseScaleCaptured = true;
        }
        Vector3 endScale = terrainBaseScale;
        float scaleEffect = UnityEngine.Random.Range(0.3f, 0.9f);
        Vector3 startScale = new(endScale.x * scaleEffect, endScale.y * scaleEffect, endScale.z);

        terrainTransform.localScale = startScale;
        yield return null;

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            if (terrainTexture == null)
            {
                revealPulseCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / revealDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            terrainTransform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        if (terrainTexture != null)
        {
            terrainTransform.localScale = endScale;
        }

        revealPulseCoroutine = null;
        ApplyRegionColor();
    }

    public void ClearScouting()
    {
        if (scoutedByTurns.Count == 0 && anchoredWarshipsTotal == 0 && persistentScoutedBy.Count == 0)
        {
            if (darknessTurnsRemaining > 0)
            {
                darknessTurnsRemaining--;
                UpdateParticles();
            }
            return;
        }
        if (scoutedByTurns.Count > 0)
        {
            List<Leader> leaders = scoutedByTurns.Keys.ToList();
            for (int i = 0; i < leaders.Count; i++)
            {
                Leader leader = leaders[i];
                scoutedByTurns[leader] = scoutedByTurns[leader] - 1;
                if (scoutedByTurns[leader] <= 0) scoutedByTurns.Remove(leader);
            }
        }
        RebuildScoutingCache();
        if (darknessTurnsRemaining > 0) darknessTurnsRemaining--;
        UpdateParticles();
        RefreshHoverText();
    }

    public void ClearScoutingAll()
    {
        if (scoutedByTurns.Count == 0 && scoutedBy.Count == 0) return;
        scoutedByTurns.Clear();
        scoutedBy.Clear();
        RebuildScoutingCache();
        RefreshHoverText();
    }

    public void UnrevealArea(int radius = 1, bool lookAt = true, Leader unrevealedBy = null)
    {
        if (board == null) board = FindFirstObjectByType<Board>();

        Unreveal(unrevealedBy);
        if (radius <= 0 || board == null) { if (lookAt) LookAt(); return; }

        var queue = new Queue<Vector2Int>(32);
        var visited = new HashSet<Vector2Int>();
        queue.Enqueue(v2);
        visited.Add(v2);

        int currentRadius = 0;
        while (queue.Count > 0 && currentRadius < radius)
        {
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                var currentHex = queue.Dequeue();
                var neighbors = ((currentHex.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                for (int j = 0; j < neighbors.Length; j++)
                {
                    var offset = neighbors[j];
                    var neighborPos = new Vector2Int(currentHex.x + offset.x, currentHex.y + offset.y);
                    if (!visited.Add(neighborPos)) continue;

                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        neighborHex.Unreveal(unrevealedBy);
                        queue.Enqueue(neighborPos);
                    }
                }
            }
            currentRadius++;
        }
    }

    public void ObscureArea(int radius = 1, bool lookAt = true, Leader obscuredBy = null)
    {
        if (board == null) board = FindFirstObjectByType<Board>();

        Obscure(obscuredBy);
        if (radius <= 0 || board == null) { if (lookAt) LookAt(); return; }

        var queue = new Queue<Vector2Int>(32);
        var visited = new HashSet<Vector2Int>();
        queue.Enqueue(v2);
        visited.Add(v2);

        int currentRadius = 0;
        while (queue.Count > 0 && currentRadius < radius)
        {
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                var currentHex = queue.Dequeue();
                var neighbors = ((currentHex.x & 1) == 0) ? board.evenRowNeighbors : board.oddRowNeighbors;

                for (int j = 0; j < neighbors.Length; j++)
                {
                    var offset = neighbors[j];
                    var neighborPos = new Vector2Int(currentHex.x + offset.x, currentHex.y + offset.y);
                    if (!visited.Add(neighborPos)) continue;

                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        neighborHex.Obscure(obscuredBy);
                        queue.Enqueue(neighborPos);
                    }
                }
            }
            currentRadius++;
        }
    }

    public void Hide()
    {
        bool shouldBeUnseen = IsHexRevealed() && mapOnlyRevealed;
        bool unseenChanged = isCurrentlyUnseen != shouldBeUnseen;
        isCurrentlyUnseen = shouldBeUnseen;
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        if (game == null) game = FindFirstObjectByType<Game>();
        if (game != null)
        {
            scoutedBy.Remove(game.currentlyPlaying);
            scoutedByTurns.Remove(game.currentlyPlaying);
            RebuildScoutingCache();
        }
        if (unseenChanged)
        {
            RedrawArmies(false);
            RedrawCharacters(false);
            RedrawPC(false);
            RefreshHoverText();
        }
    }

    public bool IsHidden() => !IsHexRevealed();

    public int GetTerrainCost(Character character)
    {
        if (character != null && character.GetIgnoreTerrainMovementPenalty())
            return 1;
        return character.IsArmyCommander() ? TerrainData.terrainCosts[terrainType] : 1;
    }

    public bool IsWaterTerrain()
    {
        return terrainType == TerrainEnum.shallowWater || terrainType == TerrainEnum.deepWater;
    }

    public bool HasAnchoredWarships() => anchoredWarshipsTotal > 0;

    public bool HasAnchoredWarshipsForLeader(Leader leader)
    {
        return leader != null && anchoredWarships.TryGetValue(leader, out int count) && count > 0;
    }

    public int GetAnchoredWarshipsTotal() => anchoredWarshipsTotal;

    public int GetAnchoredWarshipsForLeader(Leader leader)
    {
        if (leader == null) return 0;
        return anchoredWarships.TryGetValue(leader, out int count) ? count : 0;
    }

    public int AddAnchoredWarships(Leader leader, int amount)
    {
        if (leader == null || amount <= 0) return 0;
        if (anchoredWarships.TryGetValue(leader, out int current))
        {
            anchoredWarships[leader] = current + amount;
        }
        else
        {
            anchoredWarships.Add(leader, amount);
        }
        anchoredWarshipsTotal += amount;
        EnsureAnchoredVisibility(leader);
        UpdatePortIcon();
        RefreshHoverText();
        return amount;
    }

    public int RemoveAnchoredWarships(Leader leader, int amount)
    {
        if (leader == null || amount <= 0) return 0;
        if (!anchoredWarships.TryGetValue(leader, out int current) || current <= 0) return 0;
        int removed = Math.Min(amount, current);
        int remaining = current - removed;
        if (remaining > 0)
        {
            anchoredWarships[leader] = remaining;
        }
        else
        {
            anchoredWarships.Remove(leader);
        }
        anchoredWarshipsTotal -= removed;
        UpdatePortIcon();
        RefreshHoverText();
        UpdateAnchoredVisibilityAfterRemoval(leader);
        return removed;
    }

    public int TakeAnchoredWarships(Leader leader)
    {
        if (leader == null) return 0;
        if (!anchoredWarships.TryGetValue(leader, out int current) || current <= 0) return 0;
        anchoredWarships.Remove(leader);
        anchoredWarshipsTotal -= current;
        UpdatePortIcon();
        RefreshHoverText();
        UpdateAnchoredVisibilityAfterRemoval(leader);
        return current;
    }

    private void EnsureAnchoredVisibility(Leader leader)
    {
        if (leader == null) return;
        if (!leader.visibleHexes.Contains(this)) leader.visibleHexes.Add(this);
        scoutedBy.Add(leader);
        if (game == null) game = FindFirstObjectByType<Game>();
        if (game != null && game.player == leader && game.IsPlayerCurrentlyPlaying())
        {
            Reveal(leader);
        }
    }

    private void UpdateAnchoredVisibilityAfterRemoval(Leader leader)
    {
        if (leader == null) return;
        if (HasAnchoredWarshipsForLeader(leader)) return;
        if (leader.visibleHexes.Contains(this) && !leader.LeaderSeesHex(this))
        {
            leader.visibleHexes.Remove(this);
        }
    }

    public bool HasAnyPC()
    {
        return pc != null && pc.citySize != PCSizeEnum.NONE;
    }

    public bool ShouldShowPcVisual()
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return false;
        bool seen = IsHexSeen();
        if (!seen) return false;
        if (game == null) game = FindFirstObjectByType<Game>();

        PlayableLeader viewingLeader = game != null ? game.currentlyPlaying : null;
        bool pcRevealed = IsPCRevealed();
        bool ownerIsNonPlayableLeader = pc.owner is NonPlayableLeader;
        bool nplKnownByViewer = ownerIsNonPlayableLeader && viewingLeader != null && (pc.owner as NonPlayableLeader).IsRevealedToLeader(viewingLeader);
        return pcRevealed || (ownerIsNonPlayableLeader && nplKnownByViewer);
    }

    public PC GetPC()
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return null;
        if (pc.IsRevealed()) return pc;
        return null;
    }

    public PC GetPCData()
    {
        return pc != null && pc.citySize != PCSizeEnum.NONE ? pc : null;
    }

    public Sprite GetBaseTerrainSprite()
    {
        return baseTerrainSprite;
    }

    public void SetLandRegion(string region)
    {
        assignedLandRegion = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        ApplyRegionColor();
    }

    private void ApplyRegionColor()
    {
        if (hexRegion == null) return;
        bool show = !string.IsNullOrWhiteSpace(assignedLandRegion) && IsHexRevealed() && revealPulseCoroutine == null;
        hexRegion.enabled = show;
        if (show) hexRegion.color = RegionColors.GetColor(assignedLandRegion, alpha: 1f);
    }

    public string GetLandRegion()
    {
        return assignedLandRegion;
    }

    public void SetPC(PC pc, string pcFeature = "", string fortFeature = "", bool isIsland = false)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return;
        this.pc = pc;
        if(isIsland)
        {
            if (hexTextureMapping == null) hexTextureMapping = GetComponent<HexTextureMapping>();
            baseTerrainSprite = hexTextureMapping != null ? hexTextureMapping.GetIslandSprite() : baseTerrainSprite;
        }
        if (pc.owner is NonPlayableLeader)
        {
            EnsurePersistentScouting(pc.owner);
        }
        ApplyHexTextureSprite();
    }

    public void ShowMovementLeft(int movementLeft, Character character)
    {
        SetActiveFast(movement, true);
        movementCostManager.ShowMovementLeft(Math.Max(0, movementLeft), character);
    }

    public List<Character> GetEnemyCharacters(Leader leader)
    {
        if (ShouldIgnoreScouting(leader) || scoutedBy.Contains(leader))
            return characters.FindAll(x => !x.IsHidden() && x.GetOwner() != leader && (x.GetAlignment() != leader.GetAlignment() || x.GetAlignment() == AlignmentEnum.neutral)).ToList();
        return new(){};
    }

    public List<Character> GetFriendlyCharacters(Leader leader)
    {
        return characters.FindAll(x => x.GetOwner() == leader || (x.GetAlignment() == leader.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral)).ToList();
    }


    public List<Character> GetEnemyArmies(Leader leader)
    {
        if (ShouldIgnoreScouting(leader) || scoutedBy.Contains(leader))
            return characters.FindAll(x => x.IsArmyCommander() && x.GetOwner() != leader && (x.GetAlignment() != leader.GetAlignment() || x.GetAlignment() == AlignmentEnum.neutral)).ToList();
        return new(){};
    }

    public List<Character> GetFriendlyArmies(Leader leader)
    {
        return characters.FindAll(x => x.IsArmyCommander() && (x.GetOwner() == leader || (x.GetAlignment() == leader.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral))).ToList();
    }

    public string GetHoverV2()
    {
        return $"@{v2.x},{v2.y}";
    }

    public void RevealArtifact()
    {
        artifactRevealed = true;
        UpdateArtifactVisibility();
    }

    public void UpdateArtifactVisibility()
    {
        bool shouldShow = artifactRevealed && hiddenArtifacts != null && hiddenArtifacts.Count > 0 && IsHexSeen();
        SetActiveFast(artifact, shouldShow);
        if (artifactHover) SetActiveFast(artifactHover.gameObject, shouldShow);
    }

    public void EnsurePersistentScouting(Leader leader)
    {
        if (leader == null) return;
        if (persistentScoutedBy.Add(leader))
        {
            scoutedBy.Add(leader);
        }
    }

    private void RebuildScoutingCache()
    {
        scoutedBy.Clear();
        foreach (Leader leader in persistentScoutedBy)
        {
            if (leader != null) scoutedBy.Add(leader);
        }
        foreach (var entry in scoutedByTurns)
        {
            if (entry.Key != null && entry.Value > 0) scoutedBy.Add(entry.Key);
        }
        foreach (var entry in anchoredWarships)
        {
            if (entry.Key != null) scoutedBy.Add(entry.Key);
        }
        UpdateParticles();
    }

    private bool ShouldIgnoreScouting(Leader leader)
    {
        if (leader == null) return false;
        if (leader is NonPlayableLeader) return true;
        if (leader is PlayableLeader pl && game != null && game.player != pl) return true;
        return false;
    }

    public void MarkDarknessByPlayer(int turns = DarknessTurnsDefault)
    {
        if (turns <= 0) return;
        darknessTurnsRemaining = Math.Max(darknessTurnsRemaining, turns);
        UpdateParticles();
    }

    public void PlayFireParticles()
    {
        if (!ShouldShowPlayerParticles()) return;
        PlaySharedOneShotParticles(SharedParticleType.Fire);
    }

    public void PlayIceParticles()
    {
        if (!ShouldShowPlayerParticles()) return;
        PlaySharedOneShotParticles(SharedParticleType.Ice);
    }

    public void PlayStatusEffectParticles(StatusEffectEnum effect)
    {
        if (!ShouldShowPlayerParticles()) return;

        switch (effect)
        {
            case StatusEffectEnum.Poisoned:
                PlaySharedOneShotParticles(SharedParticleType.Poison);
                break;
            case StatusEffectEnum.Encouraged:
                PlaySharedOneShotParticles(SharedParticleType.Courage);
                break;
            case StatusEffectEnum.Hope:
                PlaySharedOneShotParticles(SharedParticleType.Hope);
                break;
        }
    }

    private bool ShouldShowPlayerParticles()
    {
        if (!IsHexSeen()) return false;
        if (game == null) game = FindFirstObjectByType<Game>();
        return game != null && game.player != null;
    }

    private void UpdateParticles()
    {
        if (game == null) game = FindFirstObjectByType<Game>();
        PlayableLeader player = game != null ? game.player : null;
        bool seen = IsHexSeen();
        bool scoutedByPlayer = player != null && scoutedBy.Contains(player);
        SetActiveFast(scoutedHexFrame, seen && scoutedByPlayer);
        SetActiveFast(darknessHexFrame, seen && darknessTurnsRemaining > 0);
    }

    // Safe SetActive that avoids redundant calls/dirtying the obj
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetActiveFast(GameObject go, bool state)
    {
        if (go && go.activeSelf != state) go.SetActive(state);
    }

    private void RefreshFrontierRowVisuals()
    {
        bool revealed = IsHexRevealed();
        bool isWaterHex = terrainType == TerrainEnum.shallowWater || terrainType == TerrainEnum.deepWater;
        float frontierAlpha = isCurrentlyUnseen ? 0.1f : 1f;

        if (!revealed)
        {
            SetActiveFast(cliffGameObject, false);
            SetActiveFast(hexTextureWater, false);
            return;
        }

        SetActiveFast(hexTextureWater, isWaterHex);
        SetActiveFast(cliffGameObject, !isWaterHex);
        SetFrontierRowAlpha(frontierAlpha);
    }

    private void InitializeSharedSelectedParticles()
    {
        if (selectedParticles == null)
        {
            return;
        }

        if (sharedSelectedParticles == null)
        {
            sharedSelectedParticlesLocalPosition = selectedParticles.transform.localPosition;
            sharedSelectedParticlesLocalRotation = selectedParticles.transform.localRotation;
            sharedSelectedParticlesLocalScale = selectedParticles.transform.localScale;
            sharedSelectedParticles = Instantiate(selectedParticles, transform);
            sharedSelectedParticles.name = "SharedSelectedParticles";
            SetActiveFast(sharedSelectedParticles, false);
        }

        Destroy(selectedParticles);
        selectedParticles = null;
    }

    private void SetSharedSelectedParticlesActive(bool active)
    {
        if (sharedSelectedParticles == null)
        {
            return;
        }

        if (!active)
        {
            if (sharedSelectedParticlesOwner == this)
            {
                sharedSelectedParticlesOwner = null;
                SetActiveFast(sharedSelectedParticles, false);
            }
            return;
        }

        sharedSelectedParticlesOwner = this;
        if (sharedSelectedParticles.transform.parent != transform)
        {
            sharedSelectedParticles.transform.SetParent(transform, false);
        }

        sharedSelectedParticles.transform.localPosition = sharedSelectedParticlesLocalPosition;
        sharedSelectedParticles.transform.localRotation = sharedSelectedParticlesLocalRotation;
        sharedSelectedParticles.transform.localScale = sharedSelectedParticlesLocalScale;
        SetActiveFast(sharedSelectedParticles, true);
    }

    private void InitializeSharedOneShotParticles()
    {
        RegisterSharedParticleTemplate(SharedParticleType.Fire, fireParticles);
        fireParticles = null;

        RegisterSharedParticleTemplate(SharedParticleType.Ice, iceParticles);
        iceParticles = null;

        RegisterSharedParticleTemplate(SharedParticleType.Poison, poisonParticles);
        poisonParticles = null;

        RegisterSharedParticleTemplate(SharedParticleType.Courage, courageParticles);
        courageParticles = null;

        RegisterSharedParticleTemplate(SharedParticleType.Hope, hopeParticles);
        hopeParticles = null;
    }

    private void RegisterSharedParticleTemplate(SharedParticleType type, GameObject particlesObject)
    {
        if (particlesObject == null)
        {
            return;
        }

        if (!sharedParticlePools.TryGetValue(type, out SharedParticlePoolState state))
        {
            state = new SharedParticlePoolState();
            state.localPosition = particlesObject.transform.localPosition;
            state.localRotation = particlesObject.transform.localRotation;
            state.localScale = particlesObject.transform.localScale;
            state.template = Instantiate(particlesObject, GetSharedParticlePoolRoot());
            state.template.name = $"{type}SharedParticleTemplate";
            SetActiveFast(state.template, false);
            sharedParticlePools[type] = state;
        }

        Destroy(particlesObject);
    }

    private void PlaySharedOneShotParticles(SharedParticleType type)
    {
        GameObject particlesObject = AcquireSharedParticleInstance(type);
        if (particlesObject == null)
        {
            return;
        }

        ParticleSystem[] systems = particlesObject.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            SetActiveFast(particlesObject, false);
            return;
        }

        SetActiveFast(particlesObject, true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            systems[i].Clear(true);
            systems[i].Play(true);
        }

        StartCoroutine(DisableSharedParticlesWhenDone(particlesObject, systems));
    }

    private GameObject AcquireSharedParticleInstance(SharedParticleType type)
    {
        if (!sharedParticlePools.TryGetValue(type, out SharedParticlePoolState state) || state.template == null)
        {
            return null;
        }

        GameObject instance = null;
        for (int i = 0; i < state.instances.Count; i++)
        {
            GameObject candidate = state.instances[i];
            if (candidate != null && !candidate.activeSelf)
            {
                instance = candidate;
                break;
            }
        }

        if (instance == null)
        {
            if (state.instances.Count < SharedOneShotParticlePoolSize)
            {
                instance = Instantiate(state.template, transform);
                instance.name = $"{type}SharedParticle";
                state.instances.Add(instance);
            }
            else
            {
                instance = state.instances[0];
                SetActiveFast(instance, false);
            }
        }

        if (instance == null)
        {
            return null;
        }

        if (instance.transform.parent != transform)
        {
            instance.transform.SetParent(transform, false);
        }

        instance.transform.localPosition = state.localPosition;
        instance.transform.localRotation = state.localRotation;
        instance.transform.localScale = state.localScale;
        return instance;
    }

    private IEnumerator DisableSharedParticlesWhenDone(GameObject particlesObject, ParticleSystem[] systems)
    {
        if (!particlesObject || systems == null || systems.Length == 0) yield break;

        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }
            if (anyAlive) yield return null;
        }

        SetActiveFast(particlesObject, false);
    }

    private void StopSharedOneShotParticlesOnThisHex()
    {
        foreach (SharedParticlePoolState state in sharedParticlePools.Values)
        {
            if (state == null || state.instances == null) continue;

            for (int i = 0; i < state.instances.Count; i++)
            {
                GameObject instance = state.instances[i];
                if (instance == null || instance.transform.parent != transform) continue;

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int j = 0; j < systems.Length; j++)
                {
                    if (systems[j] == null) continue;
                    systems[j].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                SetActiveFast(instance, false);
            }
        }
    }

    private static Transform GetSharedParticlePoolRoot()
    {
        if (sharedParticlePoolRoot == null)
        {
            GameObject root = new("HexSharedParticlePool");
            sharedParticlePoolRoot = root.transform;
        }

        return sharedParticlePoolRoot;
    }

    private void ApplyHexTextureSprite()
    {
        if (terrainTexture == null) return;
        if (hexTextureMapping == null) hexTextureMapping = GetComponent<HexTextureMapping>();

        terrainTexture.gameObject.SetActive(true);
        Sprite sprite = hexTextureMapping != null ? hexTextureMapping.GetSprite(this) : baseTerrainSprite;
        terrainTexture.sprite = sprite;
        UpdateTerrainVisualAlpha();
        UpdateMinimapTerrain(IsHexRevealed());
    }

    private void UpdateTerrainVisualAlpha()
    {
        float terrainAlpha = isCurrentlyUnseen ? 0.1f : 1f;
        if (!isCurrentlyUnseen && ShouldShowPcVisual() && pc != null && pc.isHidden)
        {
            terrainAlpha = 0.35f;
        }

        SetHexSpriteAlpha(terrainAlpha);
    }

    private void SetFrontierRowAlpha(float alpha)
    {
        if (cliffGameObject != null)
        {
            SetSpriteAlpha(cliffGameObject.GetComponent<SpriteRenderer>(), alpha);
        }

        if (hexTextureWater != null)
        {
            SetSpriteAlpha(hexTextureWater.GetComponent<SpriteRenderer>(), alpha);
        }
    }

    private void UpdatePcWorldText(bool shouldShowPc)
    {
        bool showText = shouldShowPc && pc != null && (pc.citySize != PCSizeEnum.NONE || pc.hasPort);

        if (pcName != null)
        {
            if (showText)
            {
                pcName.text = BuildPcNameLabel();
                SetActiveFast(pcName.gameObject, true);
            }
            else
            {
                pcName.text = string.Empty;
                SetActiveFast(pcName.gameObject, false);
            }
        }
    }

    private string BuildPcNameLabel()
    {
        if (pc == null) return string.Empty;

        string formattedName = pc.pcName ?? string.Empty;

        StringBuilder builder = new();
        if(showPcMarkBackgroundColor)
        {
            builder.Append("<mark=");
            builder.Append(GetPcAlignmentMarkColorHex());
            builder.Append(">");
        }

        builder.Append("<color=");
        builder.Append(GetPcNationColorHex());
        builder.Append('>');
        builder.Append(formattedName);
        builder.Append("</color>\n");

        if (pc.citySize != PCSizeEnum.NONE)
        {
            builder.Append("<sprite name=\"pc\"><color=");
            builder.Append(GetGradientColorHex((int)pc.citySize, (int)PCSizeEnum.camp, (int)PCSizeEnum.city));
            builder.Append('>');
            builder.Append((int)pc.citySize);
            builder.Append("</color>");
        }

        if (pc.fortSize > FortSizeEnum.NONE)
        {
            if (pc.citySize != PCSizeEnum.NONE || pc.hasPort) builder.Append(' ');
            builder.Append("<sprite name=\"fort\"><color=");
            builder.Append(GetGradientColorHex((int)pc.fortSize, (int)FortSizeEnum.tower, (int)FortSizeEnum.citadel));
            builder.Append('>');
            builder.Append((int)pc.fortSize);
            builder.Append("</color>");
        }

        builder.Append("<sprite name=\"loyalty\"><color=");
        builder.Append(GetLoyaltyColorHex(pc.loyalty));
        builder.Append('>');
        builder.Append(Math.Max(0, pc.loyalty));
        builder.Append("</color>");

        if (pc.hasPort) builder.Append(" <sprite name=\"port\">");

        if(showPcMarkBackgroundColor) builder.Append("</mark>");

        return builder.ToString();
    }

    private static string GetGradientColorHex(int value, int minValue, int maxValue)
    {
        float t = Mathf.InverseLerp(minValue, maxValue, value);
        Color blended = Color.Lerp(new Color(1f, 0.85f, 0.2f, 1f), new Color(0.2f, 0.8f, 0.2f, 1f), t);
        return $"#{ColorUtility.ToHtmlStringRGB(blended)}";
    }

    private static string GetLoyaltyColorHex(int loyaltyValue)
    {
        if (loyaltyValue <= 33) return "#ff4d4d";
        if (loyaltyValue <= 66) return "#ffd54f";
        return "#00c853";
    }

    private string GetPcNationColorHex()
    {
        if (pc?.owner == null || colors == null)
        {
            return "#FFFFFF";
        }

        Color nationColor = pc.owner.nationColor;
        return $"#{ColorUtility.ToHtmlStringRGB(nationColor)}";
    }

    public void SetCharacterHovered(bool hovered)
    {
        if (isCharacterHovered == hovered) return;
        isCharacterHovered = hovered;
    }

    private void UpdateCharacterSpriteAlpha()
    {
        if (characterSpriteRenderer == null || !characterSpriteRenderer.gameObject.activeSelf) return;

        if (isCharacterHovered)
        {
            SetSpriteAlpha(characterSpriteRenderer, 1f);
            return;
        }

        if (board == null) board = FindFirstObjectByType<Board>();
        Character selected = board != null ? board.selectedCharacter : null;

        if (selected != null && selected.hex == this && characterSpriteRenderer.sprite != null && characterSpriteRenderer.sprite != defaultCharacterSprite)
        {
            float t = Mathf.PingPong(Time.time * selectedBlinkSpeed, 1f);
            float alpha = Mathf.Lerp(selectedBlinkMinAlpha, selectedBlinkMaxAlpha, t);
            SetSpriteAlpha(characterSpriteRenderer, alpha);
        }
        else if (selected != null)
        {
            SetSpriteAlpha(characterSpriteRenderer, NonSelectedCharacterAlpha);
        }
        else
        {
            SetSpriteAlpha(characterSpriteRenderer, 1f);
        }
    }

    private string GetPcAlignmentMarkColorHex()
    {
        string color = pc?.owner == null || colors == null
            ? "#FFFFFF"
            : colors.GetHexColorByName(pc.owner.GetAlignment().ToString());
        if (string.IsNullOrWhiteSpace(color)) return "#FFFFFF66";

        string trimmed = color.Trim();
        if (trimmed.StartsWith("#"))
        {
            string hex = trimmed[1..];
            if (hex.Length == 6) return $"#{hex}66";
            if (hex.Length == 8) return $"#{hex[..6]}66";
        }

        return "#FFFFFFAA";
    }
}
