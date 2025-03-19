using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string actionName;
    [HideInInspector]
    public Character character;

    [Header("Rendering")]
    [HideInInspector]
    public string actionInitials;
    public Sprite actionSprite;
    public Color actionColor;

    [Header("Game Objects")]
    public Image background;
    public Button button;
    public TextMeshProUGUI textUI;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;

    [Header("Required skill")]
    public int difficulty = 0;
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;

    [Header("Cost")]
    public int leatherCost;
    public int mountsCost;
    public int timberCost;
    public int ironCost;
    public int mithrilCost;
    public int goldCost;

    [Header("XP")]
    public int commanderXP;
    public int agentXP;
    public int emmissaryXP;
    public int mageXP;

    [Header("Reward")]
    public int reward = 1;

    [Header("Availability")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> condition;

    [Header("Effect")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> effect;

    private bool isHovering = false;
    private RectTransform rectTransform;

    void Awake()
    {
        actionInitials = gameObject.name.ToUpper();
        rectTransform = GetComponent<RectTransform>();
        tooltipText.text = actionName;
        if (actionSprite)
        {
            background.sprite = actionSprite;
        } else
        {
            background.color = actionColor;
            textUI.text = actionInitials.ToUpper();
        }

        button.gameObject.SetActive(false);
        tooltipPanel.SetActive(false);

    }

    void Update()
    {
        // Only perform this check if the tooltip is currently showing
        if (tooltipPanel.activeSelf)
        {
            // Use a small delay before checking to ensure events have time to process
            if (!IsPointerOverUIObject(rectTransform) && !isHovering)
            {
                tooltipPanel.SetActive(false);
            }
        }
    }

    // More reliable method to check if pointer is over UI element
    private bool IsPointerOverUIObject(RectTransform rectTransform)
    {
        Vector2 localMousePosition = rectTransform.InverseTransformPoint(Input.mousePosition);
        return rectTransform.rect.Contains(localMousePosition);
    }

    public virtual void Initialize(Character character, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalCondition = condition;
        this.character = character;
        this.condition = (character) => { return IsAvailable() && (originalCondition == null || originalCondition(character)); };
        this.effect = effect;
        button.gameObject.SetActive(this.condition(character));
    }

    public void Reset()
    {
        character = null;
        condition = null;
        effect = null;
        button.gameObject.SetActive(false);
    }

    public bool IsAvailable()
    {
        if (character.hasActionedThisTurn) return false;
        if (character.commander < commanderSkillRequired) return false;
        if (character.agent < agentSkillRequired) return false;
        if (character.emmissary < emissarySkillRequired) return false;
        if (character.mage < mageSkillRequired) return false;

        if (character.GetOwner().leatherAmount < leatherCost) return false;
        if (character.GetOwner().timberAmount < timberCost) return false;
        if (character.GetOwner().mountsAmount < mountsCost) return false;
        if (character.GetOwner().ironAmount < ironCost) return false;
        if (character.GetOwner().mithrilAmount < mithrilCost) return false;
        if (character.GetOwner().goldAmount < goldCost) return false;

        return true;
    }
    public void Execute()
    {
        character.hasActionedThisTurn = true;
        FindFirstObjectByType<ActionsManager>().Refresh(character);


        if (UnityEngine.Random.Range(0, 100) < difficulty || !effect(character))
        {
            MessageDisplay.ShowMessage($"{actionName} failed", Color.red);
            return;
        }

        character.commander += UnityEngine.Random.Range(0, 100) < commanderXP ? 1 : 0;
        character.commander = Math.Max(5, character.commander);
        character.agent += UnityEngine.Random.Range(0, 100) < agentXP ? 1 : 0; ;
        character.agent = Math.Max(5, character.agent);
        character.emmissary += UnityEngine.Random.Range(0, 100) < emmissaryXP ? 1 : 0; ;
        character.emmissary = Math.Max(5, character.emmissary);
        character.mage += UnityEngine.Random.Range(0, 100) < mageXP ? 1 : 0; ;
        character.mage = Math.Max(5, character.mage);

        FindFirstObjectByType<SelectedCharacterIcon>().Refresh(character);

        character.GetOwner().leatherAmount -= leatherCost;
        character.GetOwner().timberAmount -= timberCost;
        character.GetOwner().mountsAmount -= mountsCost;
        character.GetOwner().ironAmount -= ironCost;
        character.GetOwner().mithrilAmount -= mithrilCost;
        character.GetOwner().goldAmount -= goldCost;

        FindFirstObjectByType<StoresManager>().RefreshStores();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        tooltipPanel.SetActive(false);
    }

    protected Character FindTarget(Character assassin)
    {
        Character target = FindNonNeutralCharactersNoLeader(assassin);
        if (target) return target;
        target = FindCharactersNoLeaders(assassin);
        if (target) return target;
        target = FindNonNeutralCharacters(assassin);
        if (target) return target;
        target = FindCharacters(assassin);
        return target;
    }
    protected Character FindNonNeutralCharactersNoLeader(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral &&
            x is not PlayableLeader
        );
    }
    protected Character FindCharactersNoLeaders(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment()) && (x is not PlayableLeader)
        );
    }

    protected Character FindNonNeutralCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral
        );
    }

    protected Character FindCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }

    protected Army FindEnemyArmy(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
        if(commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }

    protected Army FindEnemyArmyNotNeutral(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        Character commander = c.hex.characters.Find(
            x => x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != AlignmentEnum.neutral && x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }
}
