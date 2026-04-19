using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UncautiousSupperAction : EventAction
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

            var nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int hobbitsAffected = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (target.race == RacesEnum.Hobbit)
                {
                    target.ClearStatusEffect(StatusEffectEnum.Hidden);
                    target.Wounded(character.GetOwner(), 15);
                    hobbitsAffected++;
                }
            }

            if (hobbitsAffected == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Uncautious Supper: {hobbitsAffected} Hobbit unit(s) lose Hidden <sprite name=\"hidden\"> and take 15 damage.",
                Color.cyan);

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
