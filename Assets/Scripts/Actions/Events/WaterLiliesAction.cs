using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaterLiliesAction : EventAction
{
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> hobbits = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            if (hobbits.Count == 0) return false;

            int restored = 0;
            foreach (Character hobbit in hobbits)
            {
                hobbit.hasActionedThisTurn = false;
                hobbit.moved = 0;
                if (hobbit.HasStatusEffect(StatusEffectEnum.Blocked))
                {
                    hobbit.ClearStatusEffect(StatusEffectEnum.Blocked);
                }
                restored++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"River Lillies: {restored} Hobbit(s) by the water shake off their weariness and are free to act and move again.",
                new Color(0.65f, 0.8f, 0.78f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

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
