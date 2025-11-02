using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Hex : MonoBehaviour
{
    public Vector2Int v2;

    [Header("Rendering")]
    public SpriteRenderer sprite;

    public GameObject camp;
    public GameObject village;
    public GameObject town;
    public GameObject majorTown;
    public GameObject city;

    public GameObject tower;
    public GameObject keep;
    public GameObject fort;
    public GameObject fortress;
    public GameObject citadel;

    public GameObject characterIcon;

    public TextMeshPro freeArmiesAtHexText;
    public TextMeshPro neutralArmiesAtHexText;
    public TextMeshPro darkServantArmiesAtHexText;
    public TextMeshPro charactersAtHexText;
    public GameObject encounter;
    public GameObject port;

    public GameObject freeArmy;
    public GameObject darkArmy;
    public GameObject neutralArmy;

    public TerrainEnum terrainType;
    public SpriteRenderer terrainTexture;

    public GameObject fow;

    [Header("Hover")]
    public GameObject hoverHexFrame;
    /*public GameObject hoverTooltip;
    public GameObject hoverIcon;
    public SpriteRenderer hoverHidden;
    public TextMeshPro hoverPcName;
    public TextMeshPro hoverProduces;
    public GameObject hoverCharacterIcon;*/
    
    [Header("Data")]
    [SerializeField] private PC pc;

    public List<Army> armies;
    public List<Character> characters;
    public List<EncountersEnum> encounters;
    public List<Artifact> hiddenArtifacts = new();

    private Illustrations illustrations;

    public bool isSelected = false;

    void Awake()
    {
        pc = null;
        sprite = GetComponent<SpriteRenderer>();
        illustrations = FindFirstObjectByType<Illustrations>();
        armies = new();
        characters = new();
        encounters = new ();
        hiddenArtifacts = new();
    }

    public void RedrawArmies()
    {
        bool hasFreeArmy = armies.Find(x => x.GetCommander().alignment == AlignmentEnum.freePeople) != null;
        bool hasNeutralArmy = armies.Find(x => x.GetCommander().alignment == AlignmentEnum.neutral) != null;
        bool hasDarkArmy = armies.Find(x => x.GetCommander().alignment == AlignmentEnum.darkServants) != null;

        freeArmy.SetActive(hasFreeArmy);
        neutralArmy.SetActive(hasNeutralArmy);
        darkArmy.SetActive(hasDarkArmy);

        RefreshHoverText();
    }

    public void RedrawCharacters()
    {
        characterIcon.SetActive(characters.Find(x => !x.IsArmyCommander()) != null);
        RefreshHoverText();
    }

    public void RedrawPC()
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return;

        Leader player = FindFirstObjectByType<Game>().player;

        bool isRevealed = false;

        if (player)
        {
            isRevealed = !pc.isHidden || pc.hiddenButRevealed || pc.owner == player || (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == player.GetAlignment());
        }

		if (isRevealed)
        {
            camp.SetActive(pc.citySize == PCSizeEnum.camp);
            village.SetActive(pc.citySize == PCSizeEnum.village);
            town.SetActive(pc.citySize == PCSizeEnum.town);
            majorTown.SetActive(pc.citySize == PCSizeEnum.majorTown);
            city.SetActive(pc.citySize == PCSizeEnum.city);
            port.SetActive(pc.hasPort);
        }

		tower.SetActive(pc.fortSize == FortSizeEnum.tower);
        keep.SetActive(pc.fortSize == FortSizeEnum.keep);
        fort.SetActive(pc.fortSize == FortSizeEnum.fort);
        fortress.SetActive(pc.fortSize == FortSizeEnum.fortress);
        citadel.SetActive(pc.fortSize == FortSizeEnum.citadel);

        RedrawEncounters();
        RefreshHoverText();
    }

    public void RedrawEncounters()
    {
        encounter.SetActive(encounters.Count > 0);
    }

    public void RefreshHoverText()
    {

        if (characterIcon.activeSelf)
        {
            charactersAtHexText.text = "";
            freeArmiesAtHexText.text = "";
            darkServantArmiesAtHexText.text = "";
            neutralArmiesAtHexText.text = "";
            foreach (Character character in characters)
            {
                charactersAtHexText.text += $"<mark=#ffffff>{character.GetHoverText(true, true, false)}</mark>\n";
                if(character.IsArmyCommander())
                {
                    string armyText = $"<mark=#ffffff>{character.GetHoverText(false, false, true)}</mark>\n";
                    switch (character.alignment)
                    {
                        case AlignmentEnum.freePeople:
                            freeArmiesAtHexText.text += armyText;
                            break;
                        case AlignmentEnum.neutral:
                            neutralArmiesAtHexText.text += armyText;
                            break;
                        case AlignmentEnum.darkServants:
                            darkServantArmiesAtHexText.text += armyText;
                            break;
                    }
                }                
            }
        }

        /*
        hoverPcName.text = "";
        hoverProduces.text = "";

        hoverIcon.SetActive(false);

        hoverHidden.gameObject.SetActive(false);
        
        if (pc != null && pc.citySize != PCSizeEnum.NONE)
        {

            Leader player = FindFirstObjectByType<Game>().player;

            bool isRevealed = false;

            if (player)
            {
                isRevealed = !pc.isHidden || pc.hiddenButRevealed || pc.owner == player || (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == player.GetAlignment());
            }
            if (isRevealed)
            {
                hoverPcName.text = pc.pcName;
                hoverProduces.text = pc.GetProducesHoverText();
                
                if (pc.owner != null)
                {
                    Sprite illustrationSmall = illustrationsSmall.GetIllustrationByName(pc.owner);
                    if (illustrationSmall != null)
                    {
                        hoverIcon.GetComponent<SpriteRenderer>().sprite = illustrationSmall;
                        hoverIcon.SetActive(true);
                    }
                }

                hoverHidden.gameObject.SetActive(pc.hiddenButRevealed);

            }
        }*/
    }

    public void Hover()
    {
        if(IsPointerOverVisibleUIElement())
        {
            Unhover();
            return;
        }
        if (!IsHidden())
        {
            hoverHexFrame.SetActive(true);
            // hoverTooltip.SetActive(true);
            try
            {
                float ortoSize = Camera.main.orthographicSize;
                // float factor = 6;
                /*hoverTooltip.transform.localScale = new Vector3(ortoSize/ factor, ortoSize/ factor, 1);
                hoverTooltip.transform.localPosition = new Vector3(
                    hoverTooltip.transform.localPosition.x,
                    0.5f * 0.1f * factor + (hoverTooltip.transform.localScale.y),
                    1
                );*/
            } catch(System.Exception)
            {

            }
            
        }
    }

    public void Unhover()
    {
        if(!isSelected) hoverHexFrame.SetActive(false);
        // hoverTooltip.SetActive(false);
    }

    public void Select()
    {
        if (!IsHidden())
        {
            hoverHexFrame.SetActive(true);
            isSelected = true;
            LookAt();
        }
    }

    public void Unselect()
    {
        isSelected = false;
        hoverHexFrame.SetActive(false);
        // hoverTooltip.SetActive(false);
    }

    public void LookAt()
    {
        GameObject goHex = GameObject.Find($"{v2.x},{v2.y}");
        FindFirstObjectByType<BoardNavigator>().LookAt(goHex.transform.position);
    }

    public bool HasCharacter(Character c)
    {
        return characters.Contains(c);
    }

    public bool HasPcOfLeader(Leader c)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return false;
        return pc.owner == c;
    }

    public bool HasArmyOfLeader(Leader c)
    {
        return armies.Find(x => x.GetCommander() == c || x.GetCommander().owner == c) != null;
    }

    public bool HasCharacterOfLeader(Leader c)
    {
        return characters.Find(x => x == c || x.owner == c) != null;
    }

    public bool LeaderSeesHex(Leader c)
    {
        return HasArmyOfLeader(c) || HasPcOfLeader(c) || HasCharacterOfLeader(c);
    }

    public void Reveal()
    {
        fow.SetActive(false);
    }

    public void RevealArea(int radius = 1)
    {
        Board board = FindFirstObjectByType<Board>();
        // First reveal this hex
        Reveal();

        // No need to continue if radius is 0
        if (radius <= 0) return;

        // Queue for breadth-first search
        Queue<Vector2> queue = new Queue<Vector2>();
        // Set to keep track of visited hexes
        HashSet<Vector2> visited = new HashSet<Vector2>();

        // Start with this hex
        queue.Enqueue(v2);
        visited.Add(v2);

        // Track current radius
        int currentRadius = 0;

        while (queue.Count > 0 && currentRadius < radius)
        {
            // Process all hexes at the current radius level
            int hexCount = queue.Count;
            for (int i = 0; i < hexCount; i++)
            {
                Vector2 currentHex = queue.Dequeue();

                // Get the appropriate neighbor vectors based on whether the row is even or odd
                Vector2Int[] neighbors = Mathf.RoundToInt(currentHex.x) % 2 == 0
                    ? board.evenRowNeighbors
                    : board.oddRowNeighbors;

                // Check all neighbors
                foreach (Vector2Int offset in neighbors)
                {
                    Vector2 neighborPos = new Vector2(
                        currentHex.x + offset.x,
                        currentHex.y + offset.y
                    );

                    // Skip if already visited
                    if (visited.Contains(neighborPos)) continue;

                    // Mark as visited
                    visited.Add(neighborPos);

                    // If hex exists in the board, reveal it and add to queue
                    if (board.hexes.TryGetValue(neighborPos, out Hex neighborHex))
                    {
                        neighborHex.Reveal();
                        queue.Enqueue(neighborPos);
                    }
                }
            }

            // Move to the next radius level
            currentRadius++;
        }
        LookAt();
    }

    public void Hide()
    {
        fow.SetActive(true);
    }

    public bool IsHidden()
    {
        return fow.activeSelf;
    }
    public int GetTerrainCost(Character character)
    {
        return character.IsArmyCommander() ? TerrainData.terrainCosts[terrainType] : 1;
    }
    private static bool IsPointerOverVisibleUIElement()
    {
        if (EventSystem.current == null) return false;

        // Set up the new Pointer Event
        PointerEventData eventData = new(EventSystem.current) { position = Input.mousePosition };

        List<RaycastResult> results = new();

        // Raycast using the Graphics Raycaster and the Event Data
        EventSystem.current.RaycastAll(eventData, results);

        // Only return true if we hit a visible UI element (not just the Canvas)
        foreach (var result in results)
        {
            // Skip the Canvas itself
            if (result.gameObject.GetComponent<Canvas>() != null) continue;

            // Check if it's an Image with non-zero alpha
            Image image = result.gameObject.GetComponent<Image>();
            if (image != null && image.color.a > 0.01f && image.raycastTarget) return true;

            // Check if it's Text with non-zero alpha
            TextMeshProUGUI tmpText = result.gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null && tmpText.color.a > 0.01f) return true;
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

    public void SetPC(PC pc)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return;
        this.pc = pc;
    }
    
}
