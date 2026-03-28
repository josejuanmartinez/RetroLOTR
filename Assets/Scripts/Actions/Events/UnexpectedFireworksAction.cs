using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnexpectedFireworksAction : EventAction
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

            var targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int enemiesFeared = 0;
            int hobbitsInspired = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target.race == RacesEnum.Hobbit)
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    hobbitsInspired++;
                }

                if (target.GetAlignment() != character.GetAlignment())
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    enemiesFeared++;
                }
            }

            if (enemiesFeared == 0 && hobbitsInspired == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Unexpected Fireworks: {enemiesFeared} enemy unit(s) gain Fear (1), {hobbitsInspired} Hobbit(s) gain Hope (1) in radius {Radius}.",
                Color.yellow);

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
