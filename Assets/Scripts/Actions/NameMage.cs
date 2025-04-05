using System;

public class NameMage : MageCommanderAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            string nextCharacterName = c.GetOwner().GetBiome().characterNames[UnityEngine.Random.Range(0, c.GetOwner().GetBiome().characterNames.Count)];
            c.GetOwner().GetBiome().characterNames.Remove(nextCharacterName);
            Character character = FindFirstObjectByType<CharacterInstantiator>().InstantiateCharacter();
            character.Initialize(c.GetOwner(), c.GetAlignment(), c.hex, nextCharacterName, 0, 0, 0, 1);
            c.hex.RedrawCharacters();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner().controlledCharacters.Count < FindFirstObjectByType<Game>().maxCharactersPerPlayer && c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && (originalEffect == null || originalEffect(c));
        };
        base.Initialize(c, condition, effect);
    }
}
