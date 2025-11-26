using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Hex : MonoBehaviour
{
    public Vector2Int v2;

    [Header("Rendering")]

    public GameObject camp;
    public GameObject village;
    public GameObject town;
    public GameObject majorTown;
    public GameObject city;

    public SpriteRenderer campSprite;
    public SpriteRenderer villageSprite;
    public SpriteRenderer townSprite;
    public SpriteRenderer majorTownSprite;
    public SpriteRenderer citySprite;

    public TextMeshPro campText;
    public TextMeshPro villageText;
    public TextMeshPro townText;
    public TextMeshPro majorTownText;
    public TextMeshPro cityText;

    public GameObject tower;
    public GameObject keep;
    public GameObject fort;
    public GameObject fortress;
    public GameObject citadel;
    
    public SpriteRenderer towerSprite;
    public SpriteRenderer keepSprite;
    public SpriteRenderer fortSprite;
    public SpriteRenderer fortressSprite;
    public SpriteRenderer citadelSprite;

    public GameObject characterIcon;

    public TextMeshPro freeArmiesAtHexText;
    public TextMeshPro neutralArmiesAtHexText;
    public TextMeshPro darkServantArmiesAtHexText;
    public TextMeshPro charactersAtHexText;
    public GameObject port;

    public GameObject freeArmy;
    public GameObject darkArmy;
    public GameObject neutralArmy;

    public TerrainEnum terrainType;
    public SpriteRenderer terrainTexture;

    public GameObject fow;
    public GameObject movement;
    public MovementCostManager movementCostManager;

    public SpriteRenderer campSR;
    public SpriteRenderer villageSR;
    public SpriteRenderer townSR;
    public SpriteRenderer majorTownSR;
    public SpriteRenderer citySR;
    public SpriteRenderer freeArmySR;
    public SpriteRenderer neutralArmySR;
    public SpriteRenderer darkArmySR;

    [Header("Hover")]
    public GameObject hoverHexFrame;

    [Header("Data")]
    [SerializeField] private PC pc;

    public List<Army> armies = new();
    public List<Character> characters = new();
    public List<Artifact> hiddenArtifacts = new();

    // Use HashSet for O(1) contains
    private HashSet<Leader> scoutedBy = new();

    public bool isSelected = false;

    // Cached for speed
    private PlayableLeader leader;

    // Cached singletons/components (avoid repeated global lookups)
    private Colors colors;
    private Board board;
    private BoardNavigator navigator;
    private Game game;

    private Features features;

    // Reused buffers to avoid GC in UI building / raycasts
    private static readonly StringBuilder sbChars = new(256);
    private static readonly StringBuilder sbFree = new(256);
    private static readonly StringBuilder sbNeutral = new(256);
    private static readonly StringBuilder sbDark = new(256);
    private static readonly List<RaycastResult> raycastResults = new(16);
    private static readonly Queue<Vector2Int> areaQueue = new(64);
    private static readonly HashSet<Vector2Int> areaVisited = new();
    private static PointerEventData sharedPED;

    private const string Unknown = "?";

    void Awake()
    {
        pc = null;

        // Cache singletons once
        game = FindFirstObjectByType<Game>();
        features = FindFirstObjectByType<Features>();
        colors = FindFirstObjectByType<Colors>();
        board = FindFirstObjectByType<Board>();
        navigator = FindFirstObjectByType<BoardNavigator>();

        darkArmySR = darkArmy ? darkArmy.GetComponent<SpriteRenderer>() : null;

        if (EventSystem.current && sharedPED == null) sharedPED = new PointerEventData(EventSystem.current);
    }

    public bool IsHexRevealed() => !fow.activeSelf;

    public string GetText()
    {
        if (v2.x < 0 || v2.y < 0) return "";
        return $" @[{v2.x}, {v2.y}]";
    }

    public PlayableLeader GetPlayer()
    {
        if (leader != null) return leader;
        leader = game.player;
        return leader;
    }

    public bool IsPCRevealed(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        if (l == null || pc == null) return false;

        if (!pc.isHidden || pc.hiddenButRevealed) return true;

        var pcOwner = pc.owner;
        if (pcOwner == l) return true;

        var pcAlign = pcOwner.GetAlignment();
        var lAlign = l.GetAlignment();
        return pcAlign != AlignmentEnum.neutral && pcAlign == lAlign;
    }

    public bool IsScouted(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        return l != null && scoutedBy.Contains(l);
    }

    public bool IsFriendlyPC(PlayableLeader overrideLeader = null)
    {
        var l = overrideLeader ? overrideLeader : GetPlayer();
        if (l == null || pc == null) return false;
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
        if (game == null) game = FindFirstObjectByType<Game>();
    }

    public SpriteRenderer GetCharacterSpriteRendererOnHex(Character character)
    {
        if (character.IsArmyCommander())
        {
            return character.alignment switch
            {
                AlignmentEnum.freePeople => freeArmySR,
                AlignmentEnum.darkServants => darkArmySR,
                AlignmentEnum.neutral => neutralArmySR,
                _ => characterIcon.GetComponent<SpriteRenderer>()
            };
        }
        return characterIcon.GetComponent<SpriteRenderer>();
    }

    public void SetTerrain(TerrainEnum terrainType, Sprite terrainTexture, Color terrainColor)
    {
        this.terrainType = terrainType;
        this.terrainTexture.sprite = terrainTexture;
        // this.terrainTexture.color = terrainColor;
    }

    public void RedrawArmies(bool refreshHoverText = true)
    {
        // scan once; no LINQ allocs
        bool hasFree = false, hasNeutral = false, hasDark = false;
        for (int i = 0, n = armies.Count; i < n; i++)
        {
            var a = armies[i].GetCommander().alignment;
            if (a == AlignmentEnum.freePeople) hasFree = true;
            else if (a == AlignmentEnum.neutral) hasNeutral = true;
            else if (a == AlignmentEnum.darkServants) hasDark = true;
        }

        bool revealed = IsHexRevealed();
        SetActiveFast(freeArmy, revealed && hasFree);
        SetActiveFast(neutralArmy, revealed && hasNeutral);
        SetActiveFast(darkArmy, revealed && hasDark);

        if (refreshHoverText) RefreshHoverText();
    }

    public void RedrawCharacters(bool refreshHoverText = true)
    {
        SetActiveFast(characterIcon, IsHexRevealed() && characters.Find((x) => !x.IsArmyCommander()) != null);
        if (refreshHoverText) RefreshHoverText();
    }

    public void RedrawPC(bool refreshHoverText = true)
    {
        if(pc == null) return;
        bool revealed = IsHexRevealed();
        bool pcRevealed = revealed && IsPCRevealed();

        if(game.IsPlayerCurrentlyPlaying() && pc.owner is NonPlayableLeader && !(pc.owner as NonPlayableLeader).IsRevealedToPlayer() && pcRevealed)
        {
            NonPlayableLeader npl = pc.owner as NonPlayableLeader;
            npl.RevealToPlayer();
        } 

        // city size visibility
        SetActiveFast(camp, pcRevealed && pc.citySize == PCSizeEnum.camp);
        SetActiveFast(village, pcRevealed && pc.citySize == PCSizeEnum.village);
        SetActiveFast(town, pcRevealed && pc.citySize == PCSizeEnum.town);
        SetActiveFast(majorTown, pcRevealed && pc.citySize == PCSizeEnum.majorTown);
        SetActiveFast(city, pcRevealed && pc.citySize == PCSizeEnum.city);
        SetActiveFast(port, pcRevealed && pc.hasPort);

        // forts (note: keep tower/fortress visibility rules as in original)
        SetActiveFast(tower, revealed && pc != null && pc.fortSize == FortSizeEnum.tower);
        SetActiveFast(keep, revealed && pc != null && pc.fortSize == FortSizeEnum.keep);
        SetActiveFast(fort, revealed && pc != null && pc.fortSize == FortSizeEnum.fort);
        SetActiveFast(fortress, revealed && pc != null && pc.fortSize == FortSizeEnum.fortress);
        SetActiveFast(citadel, revealed && pc != null && pc.fortSize == FortSizeEnum.citadel);

        if (refreshHoverText) RefreshHoverText();
    }

    public string GetLoyalty()
    {
        if (pc != null && (pc.owner == game.player || (pc.owner.alignment == game.player.alignment && pc.owner.alignment != AlignmentEnum.neutral) || scoutedBy.Contains(game.player))) {
            return pc.GetLoyaltyText();
        }
        return "";
    }
    public string GetProduction()
    {
        if (pc != null && (pc.owner == game.player || (pc.owner.alignment == game.player.alignment && pc.owner.alignment != AlignmentEnum.neutral) || scoutedBy.Contains(game.player))) {
            return pc.GetProducesHoverText();
        }
        return "";
    }

    public void RefreshHoverText()
    {
        bool revealed = IsHexRevealed();
        bool pcRev = revealed && IsPCRevealed();

        if (pcRev)
        {            
            if (camp && camp.activeSelf)
            {
                campText.text = $"{pc.pcName}<sprite name=\"pc\">[1] {GetLoyalty()}{GetProduction()}";
                campText.color = colors.GetColorByName(pc.owner.GetAlignment().ToString());
            }
            if (village && village.activeSelf)
            {
                villageText.text = $"{pc.pcName}<sprite name=\"pc\">[2] {GetLoyalty()}{GetProduction()}";
                villageText.color = colors.GetColorByName(pc.owner.GetAlignment().ToString());
            }
            if (town && town.activeSelf)
            {
                townText.text = $"{pc.pcName}<sprite name=\"pc\">[3] {GetLoyalty()}{GetProduction()}";
                townText.color = colors.GetColorByName(pc.owner.GetAlignment().ToString());
            }
            if (majorTown && majorTown.activeSelf)
            {
                majorTownText.text = $"{pc.pcName}<sprite name=\"pc\">[4] {GetLoyalty()}{GetProduction()}";
                majorTownText.color = colors.GetColorByName(pc.owner.GetAlignment().ToString());
            }
            if (city && city.activeSelf)
            {
                cityText.text = $"{pc.pcName}<sprite name=\"pc\">[5] {GetLoyalty()}{GetProduction()}";
                cityText.color = colors.GetColorByName(pc.owner.GetAlignment().ToString());
            }
        }
        
        sbChars.Clear();
        sbFree.Clear();
        sbDark.Clear();
        sbNeutral.Clear();

        // Track whether we've already shown an Unknown for each bucket
        bool unkCharsShown = false;
        bool unkFreeShown = false;
        bool unkDarkShown = false;
        bool unkNeutralShown = false;

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            var ch = characters[i];
            bool canSee = IsScouted() || IsFriendlyCharacter(ch);

            if (canSee)
            {
                
                if(game.IsPlayerCurrentlyPlaying() && ch.GetOwner() is NonPlayableLeader && !(ch.GetOwner() as NonPlayableLeader).IsRevealedToPlayer())
                {
                    NonPlayableLeader npl = ch.GetOwner() as NonPlayableLeader;
                    npl.RevealToPlayer();
                } 
                
                var charName = ch.GetHoverText(true, true, true, false, true);
                if (ch.IsArmyCommander())
                {
                    var text = ch.GetHoverText(false, true, true, true, true);
                    switch (ch.alignment)
                    {
                        case AlignmentEnum.freePeople: sbFree.Append(text).Append('\n'); break;
                        case AlignmentEnum.neutral: sbNeutral.Append(text).Append('\n'); break;
                        case AlignmentEnum.darkServants: sbDark.Append(text).Append('\n'); break;
                    }
                }
                else
                {
                    sbChars.Append(charName).Append('\n');
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
                }
                else
                {
                    if (!unkCharsShown) { sbChars.Append(Unknown).Append('\n'); unkCharsShown = true; }
                }
            }
            

            // Trim a trailing newline if present
            charactersAtHexText.text = sbChars.ToString().TrimEnd('\n');
            freeArmiesAtHexText.text = sbFree.ToString().TrimEnd('\n');
            darkServantArmiesAtHexText.text = sbDark.ToString().TrimEnd('\n');
            neutralArmiesAtHexText.text = sbNeutral.ToString().TrimEnd('\n');
        }
    }


    public void Hover()
    {
        if (IsPointerOverVisibleUIElement())
        {
            Unhover();
            return;
        }
        SetActiveFast(hoverHexFrame, true);
    }

    public void Unhover()
    {
        if (!isSelected) SetActiveFast(hoverHexFrame, false);
    }

    public void Select(bool lookAt = true, float duration = 1.0f, float delay = 0.0f)
    {
        if (!IsHidden())
        {
            SetActiveFast(hoverHexFrame, true);
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

    public void Unreveal(Leader unrevealedPlayer = null)
    {
        if(unrevealedPlayer)
        {
            AlignmentEnum unreleavedPlayerAlignment = unrevealedPlayer.GetAlignment();
            foreach (var ch in scoutedBy)
            {
                if (ch.GetAlignment() != unreleavedPlayerAlignment)
                {
                    scoutedBy.Remove(ch.GetOwner());
                }
            }
        }
        if (game.currentlyPlaying == game.player && characters.Find(x => x.GetOwner() == game.player) == null)
        {
            SetActiveFast(fow, true);
        }
        
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

    private void RevealInternal(Leader scoutedByPlayer, bool isPlayerTurn)
    {
        if (scoutedByPlayer) scoutedBy.Add(scoutedByPlayer);
        if (isPlayerTurn) SetActiveFast(fow, false);
        RedrawArmies(false);
        RedrawCharacters(false);
        RedrawPC(false);
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

    public void Hide()
    {
        SetActiveFast(fow, true);
        if (game == null) game = FindFirstObjectByType<Game>();
        if (game != null) scoutedBy.Remove(game.currentlyPlaying);
    }

    public bool IsHidden() => !IsHexRevealed();

    public int GetTerrainCost(Character character)
    {
        return character.IsArmyCommander() ? TerrainData.terrainCosts[terrainType] : 1;
    }

    private static bool IsPointerOverVisibleUIElement()
    {
        if (EventSystem.current == null) return false;

        if (sharedPED == null) sharedPED = new PointerEventData(EventSystem.current);
        sharedPED.position = Input.mousePosition;

        raycastResults.Clear();
        EventSystem.current.RaycastAll(sharedPED, raycastResults);

        for (int i = 0, n = raycastResults.Count; i < n; i++)
        {
            var go = raycastResults[i].gameObject;
            if (go.TryGetComponent<Canvas>(out _)) continue;

            if (go.TryGetComponent<Image>(out var img))
                if (img.raycastTarget && img.color.a > 0.01f) return true;

            if (go.TryGetComponent<TextMeshProUGUI>(out var tmp) && tmp.color.a > 0.01f)
                return true;
        }
        return false;
    }

    public bool IsWaterTerrain()
    {
        return terrainType == TerrainEnum.shallowWater || terrainType == TerrainEnum.deepWater;
    }

    public PC GetPC()
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return null;
        if (!pc.isHidden || pc.hiddenButRevealed) return pc;
        return null;
    }

    public void SetPC(PC pc, string pcFeature = "", string fortFeature = "")
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return;
        this.pc = pc;
        if(pcFeature != "")
        {
            Sprite pcFeatureSprite = features.GetFeatureByName(pcFeature);
            campSprite.sprite = pcFeatureSprite;
            villageSprite.sprite = pcFeatureSprite;
            townSprite.sprite = pcFeatureSprite;
            majorTownSprite.sprite = pcFeatureSprite;
            citySprite.sprite = pcFeatureSprite;
        }
        if(fortFeature != "")
        {
            Sprite fortFeatureSprite = features.GetFeatureByName(fortFeature);
            towerSprite.sprite = fortFeatureSprite;
            fortSprite.sprite = fortFeatureSprite;
            keepSprite.sprite = fortFeatureSprite;
            fortressSprite.sprite = fortFeatureSprite;
            citadelSprite.sprite = fortFeatureSprite;
        }
    }

    public void ShowMovementLeft(int movementLeft, Character character)
    {
        SetActiveFast(movement, true);
        movementCostManager.ShowMovementLeft(Math.Max(0, movementLeft), character);
    }

    // Safe SetActive that avoids redundant calls/dirtying the obj
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetActiveFast(GameObject go, bool state)
    {
        if (go && go.activeSelf != state) go.SetActive(state);
    }
}
