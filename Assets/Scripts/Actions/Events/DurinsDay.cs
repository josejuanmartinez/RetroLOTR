using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DurinsDay : EventAction
{
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

            List<Character> dwarves = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf)
                .Distinct()
                .ToList();

            if (dwarves.Count == 0) return false;

            for (int i = 0; i < dwarves.Count; i++)
            {
                dwarves[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Durin's Day grants Hope to {dwarves.Count} dwarf unit(s) for 1 turn.", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
