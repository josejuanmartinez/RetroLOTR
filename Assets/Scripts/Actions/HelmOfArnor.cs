using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class HelmOfArnor : CharacterAction
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
            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        async Task<bool> helmAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied character",
                    "Ok",
                    "Cancel",
                    allies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = allies.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = allies.FirstOrDefault();
            }

            if (target == null) return false;

            string choice = "Strengthened";
            if (!isAI)
            {
                choice = await SelectionDialog.Ask(
                    "Choose spoils",
                    "Ok",
                    "Cancel",
                    new List<string> { "Strengthened", "Fortified" },
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(choice)) return false;
            }

            owner.AddIron(2, false);
            owner.AddSteel(1, false);
            owner.AddLeather(1, false);

            if (string.Equals(choice, "Fortified", StringComparison.OrdinalIgnoreCase))
            {
                target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"War spoils yield 2 iron, 1 steel, 1 leather, and {target.characterName} gains Fortified <sprite name=\"fortified\"> (1 turn).", Color.cyan);
            }
            else
            {
                target.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"War spoils yield 2 iron, 1 steel, 1 leather, and {target.characterName} gains Strengthened <sprite name=\"strengthened\"> (1 turn).", Color.cyan);
            }

            return true;
        }

        base.Initialize(c, condition, effect, helmAsync);
    }
}
