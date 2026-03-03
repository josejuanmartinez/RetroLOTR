using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BannerOfElendil : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

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

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            List<Character> targets = c.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(c, ch) && IsHumanOrDunedain(ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character t in targets)
            {
                t.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                t.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Banner of Elendil inspires {targets.Count} allied Human/Dunedain unit(s): Hope + Courage (1 turn).", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;
            return c.hex.GetHexesInRadius(2)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(c, ch) && IsHumanOrDunedain(ch)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
