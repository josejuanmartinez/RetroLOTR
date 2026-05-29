using System;
using System.Linq;
using UnityEngine;

public class Drown : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int drowned = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.IsWaterTerrain() && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                ch.Wounded(null, 10);
                drowned++;
            }
        }

        if (drowned > 0)
            MessageDisplayNoUI.ShowMessage(null, null,
                $"Drown (ongoing): rising waters claim {drowned} character(s) on water hexes — 10 damage.",
                Color.blue);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
