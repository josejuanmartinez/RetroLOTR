using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FuryOfUlmo : EventAction
{
    private static bool IsNavyOrEmbarked(Character ch)
    {
        if (ch == null || ch.killed) return false;
        if (ch.isEmbarked) return true;

        if (ch.IsArmyCommander())
        {
            Army army = ch.GetArmy();
            if (army != null && army.ws > 0) return true;
        }

        return false;
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

            List<Character> targets = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(IsNavyOrEmbarked)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Halt();
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Fury of Ulmo halts {targets.Count} navy/embarked unit(s).", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(IsNavyOrEmbarked));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
