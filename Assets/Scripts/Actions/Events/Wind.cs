using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Wind : EventAction
{
    private static bool IsFlying(Character ch) =>
        ch.race == RacesEnum.Eagle || ch.race == RacesEnum.Nazgul || ch.race == RacesEnum.Dragon
        || (ch.GetArmy() != null && ch.GetArmy().GetAbilityTroopCount(ArmySpecialAbilityEnum.Flying) > 0);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> flyers = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsFlying(ch))
            .Distinct().ToList();

        foreach (Character ch in flyers)
            ch.moved = Mathf.Max(0, ch.moved - 4);

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Wind (ongoing): {flyers.Count} flying creature(s) gain +4 movement.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> alliedFlyers = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsFlying(ch)
                    && ch.GetAlignment() == character.GetAlignment())
                .Distinct().ToList();

            foreach (Character ch in alliedFlyers)
                ch.moved = Mathf.Max(0, ch.moved - 4);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Wind: {alliedFlyers.Count} allied flying creature(s) gain +4 movement this turn.",
                Color.cyan);
            return alliedFlyers.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && IsFlying(ch)
                    && ch.GetAlignment() == character?.GetAlignment()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
