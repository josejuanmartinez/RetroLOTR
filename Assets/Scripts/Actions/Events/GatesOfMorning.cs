using System;
using System.Linq;
using UnityEngine;

public class GatesOfMorning : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hopeful = 0, despairing = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    hopeful++;
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    despairing++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Gates of Morning (ongoing): {hopeful} free people character(s) gain Hope; {despairing} dark servant(s) gain Despair.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
