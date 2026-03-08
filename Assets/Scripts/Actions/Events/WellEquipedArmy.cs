using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WellEquipedArmy : EventAction
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

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> commanders = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (commanders.Count == 0) return false;

            for (int i = 0; i < commanders.Count; i++)
            {
                commanders[i].ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Well-equipped Army grants Fortified (1) to {commanders.Count} allied army commander(s) in radius {Radius}.", Color.yellow);
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
                    .Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
