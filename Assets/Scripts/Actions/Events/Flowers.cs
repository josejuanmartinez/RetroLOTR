using System;
using System.Linq;
using UnityEngine;

public class Flowers : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int healed = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                ch.Heal(5);
                healed++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Flowers (ongoing): spring blooms restore vitality — {healed} character(s) heal 5 HP.",
            Color.green);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
