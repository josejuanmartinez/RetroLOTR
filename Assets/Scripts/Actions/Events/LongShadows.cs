using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LongShadows : EventAction
{
    private static bool IsBeastRace(RacesEnum race)
    {
        return race == RacesEnum.Troll
            || race == RacesEnum.Goblin
            || race == RacesEnum.Spider
            || race == RacesEnum.Dragon
            || race == RacesEnum.Undead;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> beasts = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsBeastRace(ch.race))
                .Distinct()
                .ToList();

            if (beasts.Count == 0) return false;

            for (int i = 0; i < beasts.Count; i++)
            {
                beasts[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Long Shadows grants Courage to {beasts.Count} beast unit(s) for 1 turn.", Color.gray);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsBeastRace(ch.race)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
