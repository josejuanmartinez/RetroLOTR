using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LoveToHalflingsLeaf : CharacterAction
{
    private const int Radius = 2;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return source.GetAlignment() == AlignmentEnum.neutral
            || target.GetAlignment() != source.GetAlignment();
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

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsEnemy(character, ch) && ch.race == RacesEnum.Maia));
        };

        async Task<bool> leafAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch) && ch.race == RacesEnum.Maia)
                .Distinct()
                .ToList();
            if (targets.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select enemy Maia",
                    "Ok",
                    "Cancel",
                    targets.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = targets.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = targets.OrderByDescending(x => x.GetMage() + x.GetCommander() + x.GetAgent() + x.GetEmmissary()).FirstOrDefault();
            }

            if (target == null) return false;

            target.Halt(1);
            target.ApplyStatusEffect(StatusEffectEnum.Blocked, 2);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{target.characterName} is subdued by Love to Halflings Leaf: Halted (1) and Blocked (2).",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, leafAsync);
    }
}
