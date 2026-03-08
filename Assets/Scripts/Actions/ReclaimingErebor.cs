using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ReclaimingErebor : CharacterAction
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

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dwarf);
        };

        async Task<bool> reclaimAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dwarf)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character t in targets)
            {
                t.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                t.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Reclaiming Erebor grants Hope and Strengthened to {targets.Count} allied Dwarf unit(s).", Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, reclaimAsync);
    }
}
