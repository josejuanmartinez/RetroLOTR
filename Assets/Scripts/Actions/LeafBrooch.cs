using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LeafBrooch : CharacterAction
{
    private const int HiddenTurns = 3;
    private const int StrengthenedTurns = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

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
            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        async Task<bool> broochAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

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
                target = allies.FirstOrDefault(ch => !ch.HasStatusEffect(StatusEffectEnum.Hidden))
                    ?? allies.FirstOrDefault();
            }

            if (target == null) return false;

            target.Hide(HiddenTurns);
            target.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenedTurns);

            // If target is an Elf, reveal hidden enemies in adjacent hexes
            int revealedCount = 0;
            if (target.race == RacesEnum.Elf && target.hex != null)
            {
                foreach (Hex h in target.hex.GetHexesInRadius(1))
                {
                    if (h?.characters == null) continue;
                    foreach (Character enemy in h.characters.Where(ch => ch != null && !ch.killed
                        && IsEnemy(target, ch) && ch.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                    {
                        enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                        revealedCount++;
                    }
                }
            }

            string elfMsg = revealedCount > 0 ? $" Elven sight reveals {revealedCount} nearby hidden enemy(ies)." : "";
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{target.characterName} receives the Leaf Brooch: Hidden ({HiddenTurns}) and Strengthened ({StrengthenedTurns}).{elfMsg}",
                Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, broochAsync);
    }
}
