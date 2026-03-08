using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WhatDoesMordorCommand : EventAction
{
    private const int Radius = 1;

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

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"What Does Mordor Command grants Strengthened and Haste (1) to {allies.Count} allied unit(s) in radius {Radius}.", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null
                && character.hex != null
                && character.hex.GetHexesInRadius(Radius)
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
