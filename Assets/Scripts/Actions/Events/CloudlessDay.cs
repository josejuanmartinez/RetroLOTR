using System;
using System.Linq;
using UnityEngine;

public class CloudlessDay : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hastened = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                hastened++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Cloudless Day (ongoing): brilliant skies lift spirits — {hastened} character(s) gain Haste.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
