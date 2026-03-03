using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class BlackNumenoreanArmour : CharacterAction
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
            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch));
        };

        async Task<bool> armourAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> alliedCommanders = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch))
                .Distinct()
                .ToList();
            if (alliedCommanders.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied army commander",
                    "Ok",
                    "Cancel",
                    alliedCommanders.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = alliedCommanders.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = alliedCommanders.FirstOrDefault();
            }

            if (target == null) return false;

            target.ClearStatusEffect(StatusEffectEnum.Halted);
            target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);

            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch))
                .Distinct()
                .ToList();
            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{target.characterName} is fortified by Black Númenórean Armour. {enemies.Count} enemy unit(s) gain Fear.", Color.red);
            return true;
        }

        base.Initialize(c, condition, effect, armourAsync);
    }
}
