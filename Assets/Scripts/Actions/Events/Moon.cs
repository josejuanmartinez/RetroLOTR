using System;
using System.Linq;
using UnityEngine;

public class Moon : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int encouraged = 0, feared = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                    encouraged++;
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    feared++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Moon (ongoing): the pale moon stirs dark things — {encouraged} dark servant(s) gain Encouraged; {feared} free people character(s) gain Fear.",
            new Color(0.6f, 0.6f, 1f));
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
