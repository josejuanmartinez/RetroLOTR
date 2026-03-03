using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BlazonOfTheEye : CharacterAction
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

            List<Character> inArea = c.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            List<Character> allies = inArea.Where(ch => IsAllied(c, ch)).ToList();
            List<Character> enemies = inArea.Where(ch => !IsAllied(c, ch)).ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            foreach (Character ally in allies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Blazon of the Eye: {allies.Count} ally unit(s) gain Courage, {enemies.Count} enemy unit(s) gain Fear.", Color.red);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null && c.hex != null;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
