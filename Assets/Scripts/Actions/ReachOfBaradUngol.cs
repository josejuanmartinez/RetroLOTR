using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ReachOfBaradUngol : CharacterAction
{
    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsEnemy(character, ch));
        };

        async Task<bool> reachAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                enemy.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Reach of Barad-Ungol afflicts {enemies.Count} enemy unit(s) with Fear and Halted.", Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, reachAsync);
    }
}
