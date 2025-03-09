using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : MonoBehaviour
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

    [Header("Required skill")]
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
    public int emissaryXP;
    public int mageXP;

    [Header("Reward")]
    public int reward = 1;

    [Header("Availability")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> condition;

    [Header("Effect")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> effect;

    void Awake()
    {
        if (actionSprite)
        {
            background.sprite = actionSprite;
        } else
        {
            background.color = actionColor;
            textUI.text = actionInitials.ToUpper();
        }
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
        bool result = effect(character);
        if(result)
        {
            character.hasActionedThisTurn = true;

            character.commander += commanderXP;
            character.agent += agentXP;
            character.emmissary += emissaryXP;
            character.mage += mageXP;

            character.GetOwner().leatherAmount -= leatherCost;
            character.GetOwner().timberAmount -= timberCost;
            character.GetOwner().mountsAmount -= mountsCost;
            character.GetOwner().ironAmount -= ironCost;
            character.GetOwner().mithrilAmount -= mithrilCost;
            character.GetOwner().goldAmount -= goldCost;
            FindFirstObjectByType<StoresManager>().RefreshStores();

        }
        else
        {
            Debug.LogError("Unable to execute order");
        }
    }

}
