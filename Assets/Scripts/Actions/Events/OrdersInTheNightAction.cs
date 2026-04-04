using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class OrdersInTheNightAction : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanLike(Character ch)
    {
        if (ch == null) return false;
        return ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain;
    }

    private static List<Character> GetEligibleTargets(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();

        return character.hex.GetHexesInRadius(Radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsHumanLike(ch) && IsAllied(character, ch))
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetEligibleTargets(character).Count > 0;
        };

        async Task<bool> ordersAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> targets = GetEligibleTargets(character);
            if (targets.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Choose allied Human/Dunedain for night orders",
                    "Order",
                    "Cancel",
                    targets.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = targets.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = targets
                    .OrderByDescending(x => x.GetMovementLeft())
                    .ThenByDescending(x => x.GetAgent() + x.GetCommander() + x.GetEmmissary())
                    .FirstOrDefault();
            }

            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            target.moved = 0;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Orders in the Night: {target.characterName} becomes Hidden (1) and may move again this turn.",
                new Color(0.6f, 0.68f, 0.9f));

            return true;
        }

        base.Initialize(c, condition, effect, ordersAsync);
    }
}
