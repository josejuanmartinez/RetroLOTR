using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldManWillowSongAction : EventAction
{
    private const int Radius = 1;

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

            foreach (Character hobbit in hobbits)
            {
                hobbit.ApplyStatusEffect(StatusEffectEnum.Blocked, 2);
                hobbit.ApplyStatusEffect(StatusEffectEnum.Halted, 2);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Old Man Willow Song: {hobbits.Count} Hobbit(s) in the nearby glade are tangled in root and shadow, Blocked and Halted for 2 turns.",
                new Color(0.34f, 0.55f, 0.34f));

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
