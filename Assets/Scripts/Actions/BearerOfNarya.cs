using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BearerOfNarya : CharacterAction
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

            List<Character> allies = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            int fearCleared = 0;
            int despairCleared = 0;
            int encouragedCount = 0;

            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i].HasStatusEffect(StatusEffectEnum.Fear))
                {
                    allies[i].ClearStatusEffect(StatusEffectEnum.Fear);
                    fearCleared++;
                }
                if (allies[i].HasStatusEffect(StatusEffectEnum.Despair))
                {
                    allies[i].ClearStatusEffect(StatusEffectEnum.Despair);
                    despairCleared++;
                }
                allies[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                encouragedCount++;
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Bearer of Narya clears Fear from {fearCleared} and Despair from {despairCleared}, granting Encouraged to {encouragedCount} allied unit(s).",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
