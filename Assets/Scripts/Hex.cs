using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Hex : MonoBehaviour
{
    public Vector2Int v2;
    [Header("References")]
    public Sprite defaultCharacterSprite;

    [Header("Character")]
    public GameObject characterIconPrefab;
    public SpriteRenderer characterIcon;
    public ZoomSpriteRenderer characterIconZoom;
    private float characterIconZoomDefault = 1f;
    private float characterIconOffsetDefault = 0f;

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
    public SpriteRenderer terrainOrNoneMinimapTexture;

    public GameObject fow;
    public GameObject unseen;
    public GameObject movement;
    public MovementCostManager movementCostManager;
    public List<SpriteRenderer> decorPlaceholders = new();
    public List<Sprite> potentialDecors = new();
    public List<Sprite> desertDecors = new();
    public List<Sprite> wastelandDecors = new();

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
    [Header("Selected")]
    public GameObject selectedParticles;

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
    private Illustrations illustrations;

    // Reused buffers to avoid GC in UI building / raycasts
    private static readonly StringBuilder sbChars = new(256);
    private static readonly StringBuilder sbFree = new(256);
    private static readonly StringBuilder sbNeutral = new(256);
    private static readonly StringBuilder sbDark = new(256);
    private static readonly Queue<Vector2Int> areaQueue = new(64);
    private static readonly HashSet<Vector2Int> areaVisited = new();

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
        illustrations = FindFirstObjectByType<Illustrations>();
        if (characterIcon != null)
        {
            characterIconZoom = characterIcon.GetComponent<ZoomSpriteRenderer>();
            if (characterIconZoom != null)
            {
                characterIconZoomDefault = characterIconZoom.zoomFactor;
                characterIconOffsetDefault = characterIconZoom.verticalOffset;
            }
        }

        UpdateMinimapTerrain(IsHexRevealed());
        UpdateVisibilityForFog();
    }

    public bool IsHexRevealed() => !fow.activeSelf;
    public bool IsHexSeen() => IsHexRevealed() && (!unseen || !unseen.activeSelf);
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
        terrainTexture.sortingOrder = row * board.GetWidth() + col;
    }

    public SpriteRenderer GetCharacterSpriteRendererOnHex(Character character)
    {
        return characterIcon != null ? characterIcon.GetComponent<SpriteRenderer>() : null;
    }

    public SpriteRenderer GetArmySpriteRendererOnHex(Character character)
    {
        if (character == null || !character.IsArmyCommander()) return null;
        return character.alignment switch
        {
            AlignmentEnum.freePeople => freeArmySR,
            AlignmentEnum.darkServants => darkArmySR,
            AlignmentEnum.neutral => neutralArmySR,
            _ => null
        };
    }

    public SpriteRenderer GetPortSpriteRenderer()
    {
        return port != null ? port.GetComponent<SpriteRenderer>() : null;
    }

    public bool HasPcPort() => pc != null && pc.hasPort;

    public bool ShouldShowWarshipPort()
    {
        if (!IsHexSeen() || !IsWaterTerrain()) return false;
        for (int i = 0, n = characters.Count; i < n; i++)
        {
            var ch = characters[i];
            if (ch == null || ch.killed || !ch.IsArmyCommander()) continue;
            Army army = ch.GetArmy();
            if (army != null && army.ws > 0) return true;
        }
        return false;
    }

    public bool TryGetKnownCharacterForIcon(out Character known)
    {
        known = null;
        if (board == null) board = FindFirstObjectByType<Board>();

        PlayableLeader player = GetPlayer();
        bool isScouted = IsScouted(player);
        Character selected = board != null ? board.selectedCharacter : null;

        if (selected != null && selected.hex == this && (isScouted || IsFriendlyCharacter(selected, player)))
        {
            known = selected;
            return true;
        }

        for (int i = 0, n = characters.Count; i < n; i++)
        {
            Character candidate = characters[i];
            if (candidate == null || candidate.killed) continue;
            if (isScouted || IsFriendlyCharacter(candidate, player))
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
        if (character == null) return false;
        if (!IsScouted() && !IsFriendlyCharacter(character)) return false;

        text = character.characterName;
        if (character.IsArmyCommander())
        {
            Army army = character.GetArmy();
            if (army != null)
            {
                text += army.GetHoverTextNoXp(false);
            }
        }
        return true;
    }

    public void SetTerrain(TerrainEnum terrainType, Sprite terrainTexture, Color terrainColor)
    {
        this.terrainType = terrainType;
        this.terrainTexture.sprite = terrainTexture;
        // this.terrainTexture.color = terrainColor;
        // if(terrainType == TerrainEnum.mountains) this.terrainTexture.sortingOrder += 1000;
        UpdateMinimapTerrain(IsHexRevealed());
        UpdateDecorForTerrain();
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

        bool seen = IsHexSeen();
        SetActiveFast(freeArmy, seen && hasFree);
        SetActiveFast(neutralArmy, seen && hasNeutral);
        SetActiveFast(darkArmy, seen && hasDark);
        UpdatePortIcon();

        if (refreshHoverText) RefreshHoverText();
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

        SetActiveFast(characterIconPrefab, seen && hasCharacter);
        if (seen && hasCharacter) UpdateCharacterIconSprite();
        UpdatePortIcon();
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

        bool pcRevealed = seen && IsPCRevealed();
        bool ownerIsNonPlayableLeader = pc.owner is NonPlayableLeader;
        bool nplKnownByViewer = ownerIsNonPlayableLeader && viewingLeader != null && (pc.owner as NonPlayableLeader).IsRevealedToLeader(viewingLeader);
        bool shouldShowPc = seen && (pcRevealed || (ownerIsNonPlayableLeader && nplKnownByViewer));
        bool isHiddenAndNotRevealed = pc.isHidden && !pc.hiddenButRevealed;
        float pcAlpha = isHiddenAndNotRevealed ? 0.75f : 1f;
        SetPcSpriteAlpha(pcAlpha);

        // city size visibility
        SetActiveFast(camp, shouldShowPc && pc.citySize == PCSizeEnum.camp);
        SetActiveFast(village, shouldShowPc && pc.citySize == PCSizeEnum.village);
        SetActiveFast(town, shouldShowPc && pc.citySize == PCSizeEnum.town);
        SetActiveFast(majorTown, shouldShowPc && pc.citySize == PCSizeEnum.majorTown);
        SetActiveFast(city, shouldShowPc && pc.citySize == PCSizeEnum.city);
        UpdatePortIcon(shouldShowPc);

        // forts (note: keep tower/fortress visibility rules as in original)
        SetActiveFast(tower, seen && pc != null && pc.fortSize == FortSizeEnum.tower);
        SetActiveFast(keep, seen && pc != null && pc.fortSize == FortSizeEnum.keep);
        SetActiveFast(fort, seen && pc != null && pc.fortSize == FortSizeEnum.fort);
        SetActiveFast(fortress, seen && pc != null && pc.fortSize == FortSizeEnum.fortress);
        SetActiveFast(citadel, seen && pc != null && pc.fortSize == FortSizeEnum.citadel);

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
        bool seen = IsHexSeen();
        bool pcRev = seen && pc != null && IsPCRevealed();

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
                } else if(ch.GetOwner() is NonPlayableLeader && !(ch.GetOwner() as NonPlayableLeader).IsRevealedToLeader(FindAnyObjectByType<Game>().currentlyPlaying))
                {
                    NonPlayableLeader npl = ch.GetOwner() as NonPlayableLeader;
                    Game g = FindAnyObjectByType<Game>();
                    bool isHuman = g != null && g.currentlyPlaying == g.player;
                    npl.RevealToLeader(g.currentlyPlaying, isHuman);
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
        if (BoardNavigator.IsPointerOverVisibleUIElement())
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
            SetActiveFast(selectedParticles, true);
            isSelected = true;
            if (lookAt) LookAt(duration, delay);
        }
    }

    public void Unselect()
    {
        isSelected = false;
        SetActiveFast(hoverHexFrame, false);
        SetActiveFast(selectedParticles, false);
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
            if (IsHexRevealed()) SetActiveFast(unseen, true);
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
        SetActiveFast(fow, true);
        SetActiveFast(unseen, true);
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

    private void RevealNonPlayableLeadersOnHex(PlayableLeader leader, bool showPopup)
    {
        if (leader == null) return;
        if (game == null) game = FindFirstObjectByType<Game>();

        NonPlayableLeader npl = null;

        if (pc != null && pc.owner is NonPlayableLeader pcOwner && !pcOwner.IsRevealedToLeader(leader))
        {
            npl = pcOwner;
        }

        if (npl == null)
        {
            npl = characters.Select(ch => ch.GetOwner() as NonPlayableLeader)
                             .FirstOrDefault(owner => owner != null && !owner.IsRevealedToLeader(leader));
        }

        if (npl != null)
        {
            bool shouldPopup = showPopup && leader == game.player && game.currentlyPlaying == leader;
            npl.RevealToLeader(leader, shouldPopup);
        }
    }

    private void SetPcSpriteAlpha(float alpha)
    {
        SetSpriteAlpha(campSprite, alpha);
        SetSpriteAlpha(villageSprite, alpha);
        SetSpriteAlpha(townSprite, alpha);
        SetSpriteAlpha(majorTownSprite, alpha);
        SetSpriteAlpha(citySprite, alpha);
        if (port != null && port.TryGetComponent<SpriteRenderer>(out var portSprite)) SetSpriteAlpha(portSprite, alpha);
    }

    private static void SetSpriteAlpha(SpriteRenderer sr, float alpha)
    {
        if (!sr) return;
        var c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    private void UpdateMinimapTerrain(bool revealed)
    {
        if (!terrainOrNoneMinimapTexture) return;
        if (revealed)
        {
            terrainOrNoneMinimapTexture.sprite = terrainTexture ? terrainTexture.sprite : null;
            SetSpriteAlpha(terrainOrNoneMinimapTexture, 1f);
        }
        else
        {
            SetSpriteAlpha(terrainOrNoneMinimapTexture, 0f);
        }
    }

    private void UpdateVisibilityForFog()
    {
        bool revealed = IsHexRevealed();
        if (revealed)
        {
            if (isSelected)
            {
                SetActiveFast(hoverHexFrame, true);
                SetActiveFast(selectedParticles, true);
            }
            if (campText) SetActiveFast(campText.gameObject, true);
            if (villageText) SetActiveFast(villageText.gameObject, true);
            if (townText) SetActiveFast(townText.gameObject, true);
            if (majorTownText) SetActiveFast(majorTownText.gameObject, true);
            if (cityText) SetActiveFast(cityText.gameObject, true);
            if (freeArmiesAtHexText) SetActiveFast(freeArmiesAtHexText.gameObject, true);
            if (neutralArmiesAtHexText) SetActiveFast(neutralArmiesAtHexText.gameObject, true);
            if (darkServantArmiesAtHexText) SetActiveFast(darkServantArmiesAtHexText.gameObject, true);
            if (charactersAtHexText) SetActiveFast(charactersAtHexText.gameObject, true);

            if (decorPlaceholders != null)
            {
                for (int i = 0, n = decorPlaceholders.Count; i < n; i++)
                {
                    var placeholder = decorPlaceholders[i];
                    if (placeholder != null) SetActiveFast(placeholder.gameObject, true);
                }
            }
            return;
        }

        SetActiveFast(camp, false);
        SetActiveFast(village, false);
        SetActiveFast(town, false);
        SetActiveFast(majorTown, false);
        SetActiveFast(city, false);
        SetActiveFast(port, false);

        SetActiveFast(tower, false);
        SetActiveFast(keep, false);
        SetActiveFast(fort, false);
        SetActiveFast(fortress, false);
        SetActiveFast(citadel, false);

        SetActiveFast(freeArmy, false);
        SetActiveFast(neutralArmy, false);
        SetActiveFast(darkArmy, false);
        SetActiveFast(characterIconPrefab, false);

        SetActiveFast(movement, false);
        SetActiveFast(hoverHexFrame, false);
        SetActiveFast(selectedParticles, false);

        if (campText) { campText.text = ""; SetActiveFast(campText.gameObject, false); }
        if (villageText) { villageText.text = ""; SetActiveFast(villageText.gameObject, false); }
        if (townText) { townText.text = ""; SetActiveFast(townText.gameObject, false); }
        if (majorTownText) { majorTownText.text = ""; SetActiveFast(majorTownText.gameObject, false); }
        if (cityText) { cityText.text = ""; SetActiveFast(cityText.gameObject, false); }
        if (freeArmiesAtHexText) { freeArmiesAtHexText.text = ""; SetActiveFast(freeArmiesAtHexText.gameObject, false); }
        if (neutralArmiesAtHexText) { neutralArmiesAtHexText.text = ""; SetActiveFast(neutralArmiesAtHexText.gameObject, false); }
        if (darkServantArmiesAtHexText) { darkServantArmiesAtHexText.text = ""; SetActiveFast(darkServantArmiesAtHexText.gameObject, false); }
        if (charactersAtHexText) { charactersAtHexText.text = ""; SetActiveFast(charactersAtHexText.gameObject, false); }

        if (decorPlaceholders != null)
        {
            for (int i = 0, n = decorPlaceholders.Count; i < n; i++)
            {
                var placeholder = decorPlaceholders[i];
                if (placeholder != null) SetActiveFast(placeholder.gameObject, false);
            }
        }
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
        bool showPcPort = (shouldShowPcOverride ?? ShouldShowPcPort()) && pc != null && pc.hasPort;
        bool showWarshipPort = ShouldShowWarshipPort();
        SetActiveFast(port, showPcPort || showWarshipPort);
    }

    private void UpdateCharacterIconSprite()
    {
        if (characterIcon == null) return;
        SpriteRenderer sr = characterIcon.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        PlayableLeader player = GetPlayer();
        bool isScouted = IsScouted(player);
        Character selected = board != null ? board.selectedCharacter : null;

        if (selected != null && selected.hex == this && (isScouted || IsFriendlyCharacter(selected, player)))
        {
            sr.sprite = GetCharacterIllustrationOrDefault(selected);
            UpdateCharacterIconZoom(sr.sprite);
            return;
        }

        Character known = null;
        for (int i = 0, n = characters.Count; i < n; i++)
        {
            Character candidate = characters[i];
            if (candidate == null || candidate.killed) continue;
            if (isScouted || IsFriendlyCharacter(candidate, player))
            {
                known = candidate;
                break;
            }
        }

        sr.sprite = known != null ? GetCharacterIllustrationOrDefault(known) : defaultCharacterSprite;
        UpdateCharacterIconZoom(sr.sprite);
    }

    private Sprite GetCharacterIllustrationOrDefault(Character character)
    {
        if (character == null) return defaultCharacterSprite;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        Sprite sprite = illustrations != null ? illustrations.GetIllustrationByName(character.characterName) : null;
        return sprite != null ? sprite : defaultCharacterSprite;
    }

    private void UpdateCharacterIconZoom(Sprite sprite)
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

    private void UpdateDecorForTerrain()
    {
        if (decorPlaceholders == null || decorPlaceholders.Count == 0) return;

        bool useDecor = terrainType == TerrainEnum.plains
            || terrainType == TerrainEnum.grasslands
            || terrainType == TerrainEnum.shore
            || terrainType == TerrainEnum.hills
            || terrainType == TerrainEnum.desert
            || terrainType == TerrainEnum.wastelands;

        List<Sprite> decorSource = potentialDecors;
        if (terrainType == TerrainEnum.desert) decorSource = desertDecors;
        if (terrainType == TerrainEnum.wastelands) decorSource = wastelandDecors;

        if (!useDecor || decorSource == null || decorSource.Count == 0)
        {
            for (int i = 0; i < decorPlaceholders.Count; i++)
            {
                var placeholder = decorPlaceholders[i];
                if (!placeholder) continue;
                placeholder.sprite = null;
                SetSpriteAlpha(placeholder, 0f);
            }
            return;
        }

        for (int i = 0; i < decorPlaceholders.Count; i++)
        {
            var placeholder = decorPlaceholders[i];
            if (!placeholder) continue;
            if (terrainTexture != null) placeholder.sortingOrder = terrainTexture.sortingOrder;

            if (UnityEngine.Random.value > 0.5f)
            {
                int index = UnityEngine.Random.Range(0, decorSource.Count);
                placeholder.sprite = decorSource[index];
                SetSpriteAlpha(placeholder, 1f);
            }
            else
            {
                placeholder.sprite = null;
                SetSpriteAlpha(placeholder, 0f);
            }
        }
    }

    private void RevealInternal(Leader scoutedByPlayer, bool isPlayerTurn)
    {
        if (scoutedByPlayer) scoutedBy.Add(scoutedByPlayer);
        if (isPlayerTurn)
        {
            SetActiveFast(fow, false);
            SetActiveFast(unseen, false);
        }
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
    }

    public void ClearScouting()
    {
        if (scoutedBy.Count == 0) return;
        scoutedBy.Clear();
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
        bool shouldBeUnseen = IsHexRevealed();
        bool unseenChanged = unseen != null && unseen.activeSelf != shouldBeUnseen;
        if (unseenChanged) SetActiveFast(unseen, shouldBeUnseen);
        UpdateVisibilityForFog();
        UpdateMinimapTerrain(IsHexRevealed());
        if (game == null) game = FindFirstObjectByType<Game>();
        if (game != null) scoutedBy.Remove(game.currentlyPlaying);
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
        return character.IsArmyCommander() ? TerrainData.terrainCosts[terrainType] : 1;
    }

    public bool IsWaterTerrain()
    {
        return terrainType == TerrainEnum.shallowWater || terrainType == TerrainEnum.deepWater;
    }

    public bool HasAnyPC()
    {
        return pc != null && pc.citySize != PCSizeEnum.NONE;
    }

    public PC GetPC()
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return null;
        if (pc.IsRevealed()) return pc;
        return null;
    }

    public void SetPC(PC pc, string pcFeature = "", string fortFeature = "", bool isIsland = false)
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
        if(isIsland)
        {
            terrainTexture.sprite = FindFirstObjectByType<Textures>().island;
        }
        UpdateMinimapTerrain(IsHexRevealed());
    }

    public void ShowMovementLeft(int movementLeft, Character character)
    {
        SetActiveFast(movement, true);
        movementCostManager.ShowMovementLeft(Math.Max(0, movementLeft), character);
    }

    public List<Character> GetEnemyCharacters(Leader leader)
    {
        if(scoutedBy.Contains(leader)) return characters.FindAll(x => x.GetOwner() != leader && (x.GetAlignment() != leader.GetAlignment() || x.GetAlignment() == AlignmentEnum.neutral)).ToList();
        return new(){};
    }

    public List<Character> GetFriendlyCharacters(Leader leader)
    {
        return characters.FindAll(x => x.GetOwner() == leader || (x.GetAlignment() == leader.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral)).ToList();
    }


    public List<Character> GetEnemyArmies(Leader leader)
    {
        if(scoutedBy.Contains(leader)) return characters.FindAll(x => x.IsArmyCommander() && x.GetOwner() != leader && (x.GetAlignment() != leader.GetAlignment() || x.GetAlignment() == AlignmentEnum.neutral)).ToList();
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

    // Safe SetActive that avoids redundant calls/dirtying the obj
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetActiveFast(GameObject go, bool state)
    {
        if (go && go.activeSelf != state) go.SetActive(state);
    }
}
