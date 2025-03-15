using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string actionName;
    public Character character;

    [Header("Rendering")]
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
        character.agent += UnityEngine.Random.Range(0, 100) < agentXP ? 1 : 0; ;
        character.emmissary += UnityEngine.Random.Range(0, 100) < emmissaryXP ? 1 : 0; ;
        character.mage += UnityEngine.Random.Range(0, 100) < mageXP ? 1 : 0; ;

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
}
