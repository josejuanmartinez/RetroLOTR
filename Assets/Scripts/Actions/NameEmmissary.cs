using System;
using UnityEngine;

public class NameEmmissary : EmmissaryCommanderAction
{
    [Header("Character Prefab")]
    public GameObject characterPrefab;
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            GameObject newCharacterPrefab = Instantiate(characterPrefab, GameObject.Find("OtherCharacters").transform);
            Character character = newCharacterPrefab.GetComponent<Character>();
            character.Initialize(c.GetOwner(), c.GetAlignment(), c.hex, false, "Emmissary");
            character.AddEmmissary(1);
            c.hex.RedrawCharacters();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner().controlledCharacters.Count < FindFirstObjectByType<Game>().maxCharactersPerPlayer && c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && (originalEffect == null || originalEffect(c));
        };
        base.Initialize(c, condition, effect);
    }
}
