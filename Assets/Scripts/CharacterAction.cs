using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : MonoBehaviour
{
    public string actionName;
    [HideInInspector] public Character character;

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
    public GameObject hoverPrefab;

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

    void Awake()
    {
        GameObject hoverInstance = Instantiate(hoverPrefab, button.transform);
        hoverInstance.GetComponent<Hover>().Initialize(actionName, Vector2.one * 40, 35, TextAlignmentOptions.Center);

        actionInitials = gameObject.name.ToUpper();
        if (actionSprite)
        {
            background.sprite = actionSprite;
        } else
        {
            background.color = actionColor;
            textUI.text = actionInitials.ToUpper();
        }

        button.gameObject.SetActive(false);

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
        if (commanderSkillRequired > 0 && character.GetCommander() < commanderSkillRequired) return false;
        if (agentSkillRequired > 0 && character.GetAgent() < agentSkillRequired) return false;
        if (emissarySkillRequired > 0 && character.GetEmmissary() < emissarySkillRequired) return false;
        if (mageSkillRequired > 0 && character.GetMage() < mageSkillRequired) return false;

        if (leatherCost > 0 && character.GetOwner().leatherAmount < leatherCost) return false;
        if (timberCost > 0 && character.GetOwner().timberAmount < timberCost) return false;
        if (mountsCost > 0 && character.GetOwner().mountsAmount < mountsCost) return false;
        if (ironCost > 0 && character.GetOwner().ironAmount < ironCost) return false;
        if (mithrilCost > 0 && character.GetOwner().mithrilAmount < mithrilCost) return false;
        if (goldCost > 0 && character.GetOwner().goldAmount < goldCost) return false;

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

        character.AddCommander(UnityEngine.Random.Range(0, 100) < commanderXP ? 1 : 0);
        character.AddAgent(UnityEngine.Random.Range(0, 100) < agentXP ? 1 : 0);
        character.AddEmmissary(UnityEngine.Random.Range(0, 100) < emmissaryXP ? 1 : 0);
        character.AddMage(UnityEngine.Random.Range(0, 100) < mageXP ? 1 : 0);

        FindFirstObjectByType<SelectedCharacterIcon>().Refresh(character);

        character.GetOwner().RemoveLeather(leatherCost);
        character.GetOwner().RemoveTimber(timberCost);
        character.GetOwner().RemoveMounts(mountsCost);
        character.GetOwner().RemoveIron(ironCost);
        character.GetOwner().RemoveMithril(mithrilCost);
        character.GetOwner().RemoveGold(goldCost);

        FindFirstObjectByType<Game>().MoveToNextCharacterToAction();

        FindFirstObjectByType<StoresManager>().RefreshStores();

        if(character.GetOwner() is not PlayableLeader) return;
        FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).Where(x => x != character.GetOwner()).ToList().ForEach(x =>
        {
            x.CheckActionConditionAnywhere(character.GetOwner(), this);
        });

        if(character.hex.GetPC() != null && character.hex.GetPC().owner is NonPlayableLeader && character.hex.GetPC().owner != character.GetOwner())
        {
            NonPlayableLeader nonPlayableLeader = character.hex.GetPC().owner as NonPlayableLeader;
            if (nonPlayableLeader == null) return;
            nonPlayableLeader.CheckActionConditionAtCapital(character.GetOwner(), this);
        }
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
