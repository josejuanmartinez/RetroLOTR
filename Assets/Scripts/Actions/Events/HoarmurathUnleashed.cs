using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HoarmurathUnleashed : EventAction
{
    private static bool IsDarkServant(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null)
        {
            env.FrozenMovementExtraPenalty = 3;
            env.FrozenCombatAttackFactor = 0.80f;
            env.FrozenCombatDefenseExtraFactor = 0.78f;
        }

        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int extended = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed
                && !IsDarkServant(ch) && ch.HasStatusEffect(StatusEffectEnum.Frozen)).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                extended++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Hoarmurath Unleashed (ongoing): {extended} frozen enemy(ies) held in permafrost — movement -8, attack -20%, defense -30%.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
