using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class BannerOfPelenorFields : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool HasCavalry(Character commander)
    {
        if (commander == null || !commander.IsArmyCommander()) return false;
        Army army = commander.GetArmy();
        if (army == null) return false;
        return (army.lc + army.hc) > 0;
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

        async Task<bool> bannerAsync(Character character)
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
                target = alliedCommanders
                    .OrderByDescending(x => HasCavalry(x) ? 1 : 0)
                    .FirstOrDefault();
            }

            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 2);
            bool cavalryBoosted = HasCavalry(target);
            if (cavalryBoosted)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }

            string hasteText = cavalryBoosted ? " and Haste (1)" : string.Empty;
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{target.characterName} rallies beneath the Banner of Pelenor Fields: Courage (2){hasteText}.",
                Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, bannerAsync);
    }
}
