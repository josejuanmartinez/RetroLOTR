using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MathomHouse : EventAction
{
    private const int Radius = 2;
    private const int GoldGain = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.GetOwner() == null) return false;

            List<Character> hobbits = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            if (hobbits.Count == 0) return false;

            for (int i = 0; i < hobbits.Count; i++)
            {
                hobbits[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            character.GetOwner().AddGold(GoldGain);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Mathom House grants Hope (1) to {hobbits.Count} Hobbit(s) in radius {Radius} and +{GoldGain} Gold.", Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.GetOwner() == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
