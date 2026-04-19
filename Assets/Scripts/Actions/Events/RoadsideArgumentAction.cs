using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SackvilleBagginsGrudgeAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> hobbits = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            if (hobbits.Count == 0) return false;

            for (int i = 0; i < hobbits.Count; i++)
            {
                hobbits[i].ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Sackville-Baggins Grudge: {hobbits.Count} Hobbit unit(s) in the hex are Blocked <sprite name=\"blocked\"> (1).",
                Color.magenta);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
