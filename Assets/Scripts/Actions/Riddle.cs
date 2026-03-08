using System;
using System.Threading.Tasks;
using UnityEngine;

public class RiddleAction : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && !character.killed;
        };

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            character.ClearStatusEffect(StatusEffectEnum.RefusingDuels);
            character.GainDuelSupremacy(1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{character.characterName} answers every challenge and will win any duel forced on them this turn.", Color.cyan);
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
