using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class WellEquipedArmy : EventAction
{
    private const int Radius = 1;

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
            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch)
                        && ch.GetArmy() != null && ch.GetArmy().ma > 0));
        };

        async Task<bool> wellEquipedAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> commanders = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch)
                    && ch.GetArmy() != null && ch.GetArmy().ma > 0)
                .Distinct()
                .ToList();

            if (commanders.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied army commander",
                    "Ok",
                    "Cancel",
                    commanders.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = commanders.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = commanders.FirstOrDefault();
            }

            if (target == null || target.GetArmy() == null) return false;

            Army army = target.GetArmy();
            army.ma = Math.Max(0, army.ma - 1);
            army.hi++;
            target.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Well-equipped Army: {target.characterName}'s Men-at-arms upgraded to Heavy Infantry; army gains Strengthened.",
                Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, wellEquipedAsync);
    }
}
