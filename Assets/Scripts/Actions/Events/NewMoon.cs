using System;
using System.Linq;
using UnityEngine;

public class NewMoon : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hidden = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                hidden++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"New Moon (ongoing): moonless night shrouds the land — {hidden} character(s) are Hidden.",
            Color.black);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
