using System;
using System.Linq;
using UnityEngine;

public class Fog : EventAction
{
    private const float HaltChance = 0.25f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int halted = 0, extended = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                    extended++;
                }

                if (UnityEngine.Random.value < HaltChance)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    halted++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Fog (ongoing): thick mist disorients — {halted} character(s) halted; {extended} hidden character(s) stay concealed.",
            Color.grey);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
