using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class NorthmenHelm : CharacterAction
{
    private const int LoyaltyBonus = 5;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanOrDunedain(Character ch) =>
        ch != null && (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain);

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;
            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch));
        };

        async Task<bool> helmAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch))
                .Distinct()
                .ToList();
            if (allies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied Human or Dunedain character",
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

            target.GainDuelSupremacy(1);
            target.RefuseDuels(1);

            // Find nearest allied PC and give loyalty bonus
            Board board = FindFirstObjectByType<Board>();
            if (board != null)
            {
                Leader owner = character.GetOwner();
                Hex nearestPCHex = board.GetHexes()
                    .Where(h => h != null && h.GetPC() != null && h.characters != null
                        && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch)))
                    .OrderBy(h => Vector2Int.Distance(character.hex.v2, h.v2))
                    .FirstOrDefault();

                if (nearestPCHex != null)
                {
                    nearestPCHex.GetPC()?.IncreaseLoyalty(LoyaltyBonus, target);
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{target.characterName} stands firm: auto-wins next duel, refuses further duels, and nearest allied PC gains {LoyaltyBonus} loyalty.",
                Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, helmAsync);
    }
}
