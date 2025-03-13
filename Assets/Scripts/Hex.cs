using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.Text;
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
    public GameObject hoverTooltip;
    public GameObject hoverIcon;
    public SpriteRenderer hoverPc;
    public SpriteRenderer hoverFort;
    public SpriteRenderer hoverPort;
    public SpriteRenderer hoverHidden;
    public TextMeshPro hoverFreeArmy;
    public TextMeshPro hoverNeutralArmy;
    public TextMeshPro hoverDarkArmy;
    public TextMeshPro hoverPcName;
    public TextMeshPro hoverProduces;
    public TextMeshPro hoverHex;
    public GameObject hoverFreeArmyIcon;
    public GameObject hoverNeutralArmyIcon;
    public GameObject hoverDarkArmyIcon;
    public GameObject hoverCharacterIcon;

    [Header("Data")]
    public PC pc;
    public List<Army> armies;
    public List<Character> characters;

    private Illustrations illustrations;
    private IllustrationsSmall illustrationsSmall;

    public bool isSelected = false;

    void Awake()
    {
        pc = null;
        sprite = GetComponent<SpriteRenderer>();
        illustrations = FindFirstObjectByType<Illustrations>();
        illustrationsSmall = FindFirstObjectByType<IllustrationsSmall>();
        armies = new();
        characters = new();
    }

    public void SpawnCapitalAtStart(Leader leader, Vector2Int v2)
    {
        this.v2 = v2;

        BiomeConfig biome = leader.biome;
        bool isPlayable = leader is PlayableLeader;
        pc = new PC(leader);

        RedrawPC(!biome.startingCityIsHidden);

        RedrawCharacters();
        encounter.SetActive(!isPlayable);

        if(leader.biome.startingArmySize > 0 || leader.biome.startingWarships > 0)
        {
            Army army = new (leader, leader.biome.preferedTroopType, leader.biome.startingArmySize, leader.biome.startingWarships);
            armies.Add(army);
            leader.army = army;
        }

        characters.Add(leader);
        leader.hex = this;

        RedrawArmies();
        RefreshHoverText();
    }

    public void RedrawArmies()
    {
        bool hasFreeArmy = armies.Find(x => x.commander.alignment == AlignmentEnum.freePeople) != null;
        bool hasNeutralArmy = armies.Find(x => x.commander.alignment == AlignmentEnum.neutral) != null;
        bool hasDarkArmy = armies.Find(x => x.commander.alignment == AlignmentEnum.darkServants) != null;

        freeArmy.SetActive(hasFreeArmy);
        neutralArmy.SetActive(hasNeutralArmy);
        darkArmy.SetActive(hasDarkArmy);

        RefreshHoverText();
    }

    public void RedrawCharacters()
    {
        characterIcon.SetActive(characters.Find(x => x.army == null) != null);
        RefreshHoverText();
    }

    public void RedrawPC(bool isVisibleToCurrentPlayer)
    {
        if (isVisibleToCurrentPlayer)
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

        RefreshHoverText();
    }

    public void RefreshHoverText()
    {
        hoverHex.text = $"[{v2.x},{v2.y}]<br>";

        hoverCharacterIcon.SetActive(characters.Count > 0);

        if (hoverCharacterIcon.activeSelf)
        {
            List<string> characterNames = new();
            foreach(Character character in characters)
            {
                string characterName = char.ToUpper(character.characterName[0]) + character.characterName[1..];
                if (character.army == null)
                {
                    characterNames.Add(characterName);
                } else
                {
                    characterNames.Add($"<sprite name=\"{character.alignment}\"/>{characterName}");
                }
            }
            charactersAtHexText.text = string.Join(", ", characterNames);
        }
        hoverPcName.text = "";
        hoverProduces.text = "";

        hoverIcon.SetActive(false);

        hoverPc.gameObject.SetActive(false);
        hoverPort.gameObject.SetActive(false);
        hoverHidden.gameObject.SetActive(false);
        hoverFort.gameObject.SetActive(false);

        hoverFreeArmy.text = "";
        hoverNeutralArmy.text = "";
        hoverDarkArmy.text = "";

        hoverFreeArmyIcon.SetActive(false);
        hoverNeutralArmyIcon.SetActive(false);
        hoverDarkArmyIcon.SetActive(false);

        hoverFreeArmy.gameObject.SetActive(false);
        hoverNeutralArmy.gameObject.SetActive(false);
        hoverDarkArmy.gameObject.SetActive(false);

        if (pc != null)
        {

            if (pc.fortSize != FortSizeEnum.NONE)
            {
                string pcFortSize = pc.fortSize.ToString();
                Sprite illustration = illustrations.GetIllustrationByName(pcFortSize);
                if (illustration != null)
                {
                    hoverFort.sprite = illustration;
                    hoverFort.gameObject.SetActive(true);
                }
            }

            if (!pc.isHidden || pc.hiddenButRevealed || pc.owner == FindFirstObjectByType<Game>().currentlyPlaying)
            {
                hoverPcName.text = pc.pcName;
                hoverProduces.text = pc.GetProducesHoverText();
                
                if (pc.owner != null)
                {
                    Sprite illustrationSmall = illustrationsSmall.GetIllustrationByName(pc.owner.characterName);
                    if (illustrationSmall != null)
                    {
                        hoverIcon.GetComponent<SpriteRenderer>().sprite = illustrationSmall;
                        hoverIcon.SetActive(true);
                    }
                }

                hoverPort.gameObject.SetActive(pc.hasPort);
                hoverHidden.gameObject.SetActive(pc.hiddenButRevealed);

                if (pc.citySize != PCSizeEnum.NONE)
                {
                    string pcSize = pc.citySize.ToString();
                    Sprite illustration = illustrations.GetIllustrationByName(pcSize);
                    if (illustration != null)
                    {
                        hoverPc.sprite = illustration;
                        hoverPc.gameObject.SetActive(true);
                    }
                }
            }
        } else
        {
            hoverPcName.text = hoverHex.text;
            hoverHex.text = "";
        }

        if(armies != null && armies.Count > 0)
        {
            Army freeArmy = new(null);
            Army neutralArmy = new(null);
            Army darkArmy = new(null);

            foreach (Army army in armies)
            {
                switch (army.GetAlignment())
                {
                    case AlignmentEnum.freePeople:
                        freeArmy.Recruit(army);
                        break;
                    case AlignmentEnum.neutral:
                        neutralArmy.Recruit(army);
                        break;
                    case AlignmentEnum.darkServants:
                        darkArmy.Recruit(army);
                        break;
                }
            }


            if (freeArmy.GetSize() > 0)
            {
                hoverFreeArmyIcon.SetActive(true);
                hoverFreeArmy.gameObject.SetActive(true);
                hoverFreeArmy.text = freeArmy.GetHoverText();
            }

            if (neutralArmy.GetSize() > 0)
            {
                hoverNeutralArmyIcon.SetActive(true);
                hoverNeutralArmy.gameObject.SetActive(true);
                hoverNeutralArmy.text = neutralArmy.GetHoverText();
            }

            if (darkArmy.GetSize() > 0)
            {
                hoverDarkArmyIcon.SetActive(true);
                hoverDarkArmy.gameObject.SetActive(true);
                hoverDarkArmy.text = darkArmy.GetHoverText();
            }
        }
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
            hoverTooltip.SetActive(true);
            try
            {
                float ortoSize = Camera.main.orthographicSize;
                float factor = 6;
                hoverTooltip.transform.localScale = new Vector3(ortoSize/ factor, ortoSize/ factor, 1);
                hoverTooltip.transform.localPosition = new Vector3(
                    hoverTooltip.transform.localPosition.x,
                    0.5f * 0.1f * factor + (hoverTooltip.transform.localScale.y),
                    1
                );
            } catch(System.Exception)
            {

            }
            
        }
    }

    public void Unhover()
    {
        if(!isSelected) hoverHexFrame.SetActive(false);
        hoverTooltip.SetActive(false);
    }

    public void Select()
    {
        if (!IsHidden())
        {
            hoverHexFrame.SetActive(true);
            isSelected = true;
        }
    }

    public void Unselect()
    {
        isSelected = false;
        hoverHexFrame.SetActive(false);
        hoverTooltip.SetActive(false);
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
        if (pc == null) return false;
        return pc.owner == c;
    }

    public bool HasArmyOfLeader(Leader c)
    {
        return armies.Find(x => x.commander == c || x.commander.owner == c) != null;
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
    }

    public void Hide()
    {
        fow.SetActive(true);
    }

    public bool IsHidden()
    {
        return fow.activeSelf;
    }

    public void RefreshForChangingPLayer(Leader currentlyPlaying)
    {
        RedrawCharacters();
        hoverCharacterIcon.SetActive(characterIcon.activeSelf);
        if(hoverCharacterIcon.activeSelf) charactersAtHexText.text = 
                string.Join(", ", characters.Select(x => char.ToUpper(x.characterName[0]) + x.characterName[1..]));

        if (pc != null && pc.isHidden && pc.owner == currentlyPlaying)
        {
            camp.SetActive(pc.citySize == PCSizeEnum.camp);
            village.SetActive(pc.citySize == PCSizeEnum.village);
            town.SetActive(pc.citySize == PCSizeEnum.town);
            majorTown.SetActive(pc.citySize == PCSizeEnum.majorTown);
            city.SetActive(pc.citySize == PCSizeEnum.city);
            port.SetActive(pc.hasPort);
        } 
        else if(pc != null && pc.isHidden)
        {
            camp.SetActive(false);
            village.SetActive(false);
            town.SetActive(false);
            majorTown.SetActive(false);
            city.SetActive(false);
            port.SetActive(false);
        }
    }

    public int GetTerrainCost(Character character)
    {
        return character.IsArmyCommander() ? TerrainData.terrainCosts[terrainType] : 1;
    }

    private static bool IsPointerOverVisibleUIElement()
    {
        if (EventSystem.current == null)
            return false;

        // Set up the new Pointer Event
        PointerEventData eventData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new();

        // Raycast using the Graphics Raycaster and the Event Data
        EventSystem.current.RaycastAll(eventData, results);

        // Only return true if we hit a visible UI element (not just the Canvas)
        foreach (var result in results)
        {
            // Skip the Canvas itself
            if (result.gameObject.GetComponent<Canvas>() != null)
                continue;

            // Check if it's an Image with non-zero alpha
            Image image = result.gameObject.GetComponent<Image>();
            if (image != null && image.color.a > 0.01f && image.raycastTarget)
                return true;

            // Check if it's Text with non-zero alpha
            TMPro.TextMeshProUGUI tmpText = result.gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpText != null && tmpText.color.a > 0.01f)
                return true;
        }

        return false;
    }
}
