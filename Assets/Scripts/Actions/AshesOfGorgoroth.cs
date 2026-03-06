using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AshesOfGorgoroth : MageAction
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

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            int radius = Mathf.Clamp(character.GetMage(), 1, 3);
            List<Character> inArea = character.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            List<Character> allies = inArea.Where(ch => IsAllied(character, ch)).ToList();
            List<Character> enemies = inArea.Where(ch => !IsAllied(character, ch)).ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            }

            int fearCleared = 0;
            foreach (Character ally in allies)
            {
                if (ally.HasStatusEffect(StatusEffectEnum.Fear))
                {
                    ally.ClearStatusEffect(StatusEffectEnum.Fear);
                    fearCleared++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Ashes of Gorgoroth: {enemies.Count} enemy unit(s) gain Despair (1) and Fear is removed from {fearCleared} allied unit(s) in radius {radius}.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.GetMage() > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
