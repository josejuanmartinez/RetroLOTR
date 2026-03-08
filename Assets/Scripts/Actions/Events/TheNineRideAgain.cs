using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheNineRideAgain : EventAction
{
    private const int Duration = 3;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> nazguls = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul)
                .Distinct()
                .ToList();

            if (nazguls.Count == 0) return false;

            for (int i = 0; i < nazguls.Count; i++)
            {
                nazguls[i].ApplyStatusEffect(StatusEffectEnum.Haste, Duration);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Nine Ride Again grants Haste to {nazguls.Count} Nazgul unit(s) for {Duration} turns.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
