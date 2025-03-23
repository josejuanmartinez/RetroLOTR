using System;
using System.Xml.Linq;
using UnityEngine;

public class NameAgent : AgentCommanderAction
{
    [Header("Character Prefab")]
    public GameObject characterPrefab;
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            string nextCharacterName = c.GetOwner().biome.characterNames[UnityEngine.Random.Range(0, c.GetOwner().biome.characterNames.Count)];
            c.GetOwner().biome.characterNames.Remove(nextCharacterName);
            GameObject newCharacterPrefab = Instantiate(characterPrefab, GameObject.Find("OtherCharacters").transform);
            Character character = newCharacterPrefab.GetComponent<Character>();
            character.Initialize(c.GetOwner(), c.GetAlignment(), c.hex, nextCharacterName);
            character.AddAgent(1);
            c.hex.RedrawCharacters();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner().controlledCharacters.Count < FindFirstObjectByType<Game>().maxCharactersPerPlayer && c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && (originalEffect == null || originalEffect(c));
        };
        base.Initialize(c, condition, effect);
    }
}
