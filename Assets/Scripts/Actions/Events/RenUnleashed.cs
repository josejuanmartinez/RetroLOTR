using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RenUnleashed : EventAction
{
    private static bool IsDarkServant(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int scorched = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed
                && !IsDarkServant(ch) && ch.HasStatusEffect(StatusEffectEnum.Burning)).ToList())
            {
                ch.Wounded(null, 5);
                scorched++;
            }
        }

        if (scorched > 0)
            MessageDisplayNoUI.ShowMessage(null, null,
                $"Ren Unleashed (ongoing): fire damage doubled — {scorched} burning enemy(ies) take 5 extra damage.",
                Color.red);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
