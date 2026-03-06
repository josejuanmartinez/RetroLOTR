using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FirstLightOnTheThirdDay : EventAction
{
    private const int Radius = 3;

    private static bool IsHumanOrDunedain(Character ch)
    {
        if (ch == null) return false;
        return ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain;
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
                .Where(ch => ch != null
                    && !ch.killed
                    && ch.GetAlignment() == AlignmentEnum.freePeople
                    && IsHumanOrDunedain(ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                targets[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"First Light On The Third Day inspires {targets.Count} Human/Dunedain unit(s) in radius {Radius}: Courage and Hope (1).", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(Radius).Any(h => h != null
                && h.characters != null
                && h.characters.Any(ch => ch != null
                    && !ch.killed
                    && ch.GetAlignment() == AlignmentEnum.freePeople
                    && IsHumanOrDunedain(ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
