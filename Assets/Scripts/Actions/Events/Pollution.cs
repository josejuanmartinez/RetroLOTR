using System;
using System.Linq;
using UnityEngine;

public class Pollution : EventAction
{
    private const float PoisonChance = 0.05f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int poisoned = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (UnityEngine.Random.value < PoisonChance)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
                    poisoned++;
                }
            }
        }

        if (poisoned > 0)
            MessageDisplayNoUI.ShowMessage(null, null,
                $"Pollution (ongoing): foul miasma poisons the land — {poisoned} character(s) are Poisoned.",
                Color.green);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
