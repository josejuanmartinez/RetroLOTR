using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ALightInDarkPlaces : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            List<Character> allies = c.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(c, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            foreach (Character ally in allies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                ally.ClearStatusEffect(StatusEffectEnum.Fear);
                ally.ClearStatusEffect(StatusEffectEnum.Despair);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"A Light in Dark Places grants Hope to {allies.Count} allied unit(s) and dispels Fear/Despair.", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;
            return c.hex.GetHexesInRadius(2)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(c, ch)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
