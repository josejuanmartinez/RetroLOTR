using System;
using System.Threading.Tasks;
using UnityEngine;

public class RefuseDuel : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && !character.IsRefusingDuels();
        };

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            character.RefuseDuels(1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{character.characterName} refuses duels for the next turn.", Color.yellow);
            return true;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
