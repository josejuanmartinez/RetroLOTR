using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class HiddenInHides : CharacterAction
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
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsEnemy(character, ch));
        };

        async Task<bool> hideAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            character.ApplyStatusEffect(StatusEffectEnum.Hidden, 2);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Hidden in Hides halts {enemies.Count} enemy unit(s), and {character.characterName} becomes Hidden (2).",
                Color.gray);

            return true;
        }

        base.Initialize(c, condition, effect, hideAsync);
    }
}
