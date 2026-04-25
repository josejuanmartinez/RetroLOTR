using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RalliedMen : CharacterAction
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
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Common);
        };

        async Task<bool> ralliedAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> targets = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Common)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character t in targets)
            {
                t.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rallied Men grants Strengthened (1) to {targets.Count} allied Human unit(s) in this hex.", Color.white);
            return true;
        }

        base.Initialize(c, condition, effect, ralliedAsync);
    }
}
