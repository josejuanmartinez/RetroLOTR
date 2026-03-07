using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GoingOnAnAdventure : EventAction
{
    private const int Radius = 2;

    private static bool IsAffected(Character character)
    {
        if (character == null || character.killed) return false;
        return character.race == RacesEnum.Hobbit || character.race == RacesEnum.Dwarf;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(IsAffected)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Going On An Adventure grants Courage to {targets.Count} Hobbit/Dwarf unit(s) in radius {Radius}.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(IsAffected));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
