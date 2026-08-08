using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OrcHostOfAngmar : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetTargets(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();
        return character.hex.GetHexesInRadius(Radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Orc && ch.IsArmyCommander()
                && IsAllied(character, ch) && ch.GetArmy() != null)
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            List<Character> targets = GetTargets(character);
            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Orc-host of Angmar: {targets.Count} allied Orc army commander(s) gain Haste and Courage.",
                Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetTargets(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
