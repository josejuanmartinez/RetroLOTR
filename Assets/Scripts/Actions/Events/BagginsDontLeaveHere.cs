using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BagginsDontLeaveHere : EventAction
{
    private const int BaseRadius = 2;
    private const int MaxRadius = 4;

    private static bool IsAffectedHobbit(Character source, Character target)
    {
        if (source == null || target == null || target.killed || target.hex == null) return false;
        if (target.race != RacesEnum.Hobbit) return false;

        int personalRadius = Mathf.Clamp(BaseRadius + target.GetAgent(), BaseRadius, MaxRadius);
        return source.hex.GetHexesInRadius(personalRadius).Contains(target.hex);
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

            List<Character> targets = character.hex.GetHexesInRadius(MaxRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => IsAffectedHobbit(character, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Hide(1);
                targets[i].ClearStatusEffect(StatusEffectEnum.Fear);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"BagginsDontLeaveHere hides {targets.Count} Hobbit(s) and removes Fear within their personal radius (2-4).",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(MaxRadius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => IsAffectedHobbit(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
