using System.Collections.Generic;
using System.Linq;
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
    public TextMeshPro charactersAtHexText;
    public GameObject encounter;
    public TextMeshPro encountersAtHexText;
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
    public GameObject hoverEncounterIcon;

    [Header("Data")]
    [SerializeField] private PC pc;

    public List<Army> armies;
    public List<Character> characters;
    public List<EncountersEnum> encounters;
    public List<Artifact> hiddenArtifacts;

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
        encounters = new ();
        hiddenArtifacts = new();
    }

    public void SpawnCapitalAtStart(Leader leader)
    {
        pc = new PC(leader);

        if (leader is not PlayableLeader) encounters.Add(EncountersEnum.Encounter);

        RedrawPC();

        RefreshHoverText();
    }

    public void SpawnLeaderAtStart(Leader leader)
    {
        leader.Initialize(leader, leader.biome.alignment, this, leader.characterName, true, true);
        RedrawCharacters();
        RedrawArmies();
    }

    public void SpawnOtherCharactersAtStart(Leader leader)
    {
        List<Character> otherCharaters = FindObjectsByType<Character>(FindObjectsSortMode.None).ToList().FindAll(x => x.GetOwner() == leader && x != leader);
        foreach(Character otherCharacter in otherCharaters)
        {
            if (otherCharacter is Leader) continue;
            otherCharacter.Initialize(leader, leader.biome.alignment, this, otherCharacter.characterName);
        }

        RedrawCharacters();
        RedrawArmies();
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

        bool isRevealed = !pc.isHidden || pc.hiddenButRevealed || pc.owner == FindFirstObjectByType<Game>().player || (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == FindFirstObjectByType<Game>().player.GetAlignment());

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
        hoverHex.text = $"[{v2.x},{v2.y}]<br>";

        hoverCharacterIcon.SetActive(characters.Count > 0);

        if (hoverCharacterIcon.activeSelf)
        {
            List<string> characterNames = new();
            foreach(Character character in characters)
            {
                string characterName = char.ToUpper(character.characterName[0]) + character.characterName[1..];
                characterNames.Add($"{(character.IsArmyCommander() ? $"<sprite name=\"{character.alignment}\">" : "")}{characterName}");
            }
            charactersAtHexText.text = string.Join(" ", characterNames);
        }

        hoverEncounterIcon.SetActive(encounters.Count > 0);
        if(hoverEncounterIcon.activeSelf)
        {
            List<string> encounterNames = new();
            foreach (EncountersEnum encounter in encounters) encounterNames.Add(encounter.ToString());
            encountersAtHexText.text = string.Join(" ", encounterNames);
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

        if (pc != null && pc.citySize != PCSizeEnum.NONE)
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
            bool isRevealed = !pc.isHidden || pc.hiddenButRevealed || pc.owner == FindFirstObjectByType<Game>().player || (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == FindFirstObjectByType<Game>().player.GetAlignment());

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
            LookAt();
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
