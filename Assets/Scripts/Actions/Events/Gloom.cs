using System;
using System.Linq;
using UnityEngine;

public class Gloom : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int despairing = 0, encouraged = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                    encouraged++;
                }
                else
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    despairing++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Gloom (ongoing): creeping darkness saps all hope — {despairing} character(s) gain Despair and Fear; {encouraged} dark servant(s) gain Encouraged.",
            Color.magenta);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
