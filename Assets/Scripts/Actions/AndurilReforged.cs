using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AndurilReforged : CommanderAction
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
            if (character == null || character.hex == null || character.killed) return false;
            return character.hex.characters.Any(x => x != null && !x.killed && IsAllied(character, x));
        };

        async Task<bool> reforgedAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(x => x != null && !x.killed && IsAllied(character, x))
                .Distinct()
                .ToList();
            if (allies.Count < 1) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied character",
                    "Ok",
                    "Cancel",
                    allies.Select(x => x.characterName).ToList(),
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = allies.Find(x => x.characterName == selected);
            }
            else
            {
                target = allies
                    .OrderByDescending(x => x.HasStatusEffect(StatusEffectEnum.Fear) ? 1 : 0)
                    .ThenByDescending(x => x.GetCommander())
                    .FirstOrDefault();
            }

            if (target == null) return false;

            target.AddCommander(1);
            target.ClearStatusEffect(StatusEffectEnum.Fear);
            target.Encourage(2);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{target.characterName} is empowered: +1 Commander, Fear removed, Courage (2).",
                Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, reforgedAsync);
    }
}
