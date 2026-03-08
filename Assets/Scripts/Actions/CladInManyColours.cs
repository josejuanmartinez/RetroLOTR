using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class CladInManyColours : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> inArea = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (inArea.Count == 0) return false;

            List<Character> allies = inArea.Where(ch => IsAllied(character, ch)).ToList();
            List<Character> enemies = inArea.Where(ch => !IsAllied(character, ch)).ToList();

            foreach (Character ally in allies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                ally.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            }

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Clad in Many Colours empowers {allies.Count} ally unit(s); {enemies.Count} enemy unit(s) gain Despair.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
