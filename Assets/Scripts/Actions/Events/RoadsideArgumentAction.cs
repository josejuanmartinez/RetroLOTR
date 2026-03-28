using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadsideArgumentAction : EventAction
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

            List<Character> nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int enemiesDisrupted = 0;
            int hobbitsQuickened = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (target.GetAlignment() != character.GetAlignment())
                {
                    target.ApplyStatusEffect(StatusEffectEnum.RefusingDuels, 1);
                    enemiesDisrupted++;
                }

                if (target.race == RacesEnum.Hobbit)
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                    hobbitsQuickened++;
                }
            }

            if (enemiesDisrupted == 0 && hobbitsQuickened == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Roadside Argument: {enemiesDisrupted} enemy unit(s) get Refusing Duels (1), {hobbitsQuickened} Hobbit(s) gain Haste (1).",
                Color.magenta);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
