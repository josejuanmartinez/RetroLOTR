using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BehindNoldoShields : CharacterAction
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

            List<Character> elves = c.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(c, ch) && ch.race == RacesEnum.Elf)
                .Distinct()
                .ToList();

            if (elves.Count == 0) return false;

            foreach (Character elf in elves)
            {
                elf.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                elf.ClearStatusEffect(StatusEffectEnum.Fear);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Behind Noldo Shields fortifies {elves.Count} allied elf unit(s).", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            return c.hex.GetHexesInRadius(1)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(c, ch) && ch.race == RacesEnum.Elf));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
