using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShirriffInterruptionAction : EventAction
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

            List<Character> nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int enemiesHalted = 0;
            int alliesHidden = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (target.GetAlignment() != character.GetAlignment() && !target.IsArmyCommander())
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    enemiesHalted++;
                }
                else if (target.GetAlignment() == character.GetAlignment() && (target.race == RacesEnum.Hobbit || target.race == RacesEnum.Common))
                {
                    target.Hide(1);
                    alliesHidden++;
                }
            }

            if (enemiesHalted == 0 && alliesHidden == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Shirriff Interruption: {enemiesHalted} enemy non-army unit(s) are Halted (1), {alliesHidden} allied Hobbit/Human unit(s) become Hidden (1).",
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
