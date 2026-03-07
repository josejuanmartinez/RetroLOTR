using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class GemsOfLasgalen : CharacterAction
{
    private const int Radius = 2;
    private const int HealAmount = 25;

    private static bool CanBenefitFromHeal(Character target)
    {
        return target != null && (target.health < 100 || target.HasStatusEffect(StatusEffectEnum.Poisoned));
    }

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

            bool hasElfTargets = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Elf));

            bool hasWoundedAllies = character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && CanBenefitFromHeal(ch));

            return hasElfTargets || hasWoundedAllies;
        };

        async Task<bool> gemsAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> elfTargets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Elf)
                .Distinct()
                .ToList();

            for (int i = 0; i < elfTargets.Count; i++)
            {
                elfTargets[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            List<Character> healTargets = character.hex.characters == null
                ? new List<Character>()
                : character.hex.characters
                    .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && CanBenefitFromHeal(ch))
                    .Distinct()
                    .ToList();

            Character healedTarget = null;
            if (healTargets.Count > 0)
            {
                bool isAI = !character.isPlayerControlled;
                if (!isAI)
                {
                    string selected = await SelectionDialog.Ask(
                        "Select wounded ally",
                        "Ok",
                        "Skip",
                        healTargets.Select(x => x.characterName).ToList(),
                        false,
                        SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        healedTarget = healTargets.FirstOrDefault(x => x.characterName == selected);
                    }
                }
                else
                {
                    healedTarget = healTargets.OrderByDescending(x => 100 - x.health).FirstOrDefault();
                }
            }

            if (healedTarget != null)
            {
                healedTarget.Heal(HealAmount);
            }

            if (elfTargets.Count == 0 && healedTarget == null) return false;

            string healText = healedTarget != null
                ? $" {healedTarget.characterName} heals {HealAmount} health."
                : string.Empty;
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Gems of Lasgalen grant Hope (1) to {elfTargets.Count} allied elf unit(s) in radius {Radius}.{healText}",
                Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, gemsAsync);
    }
}
