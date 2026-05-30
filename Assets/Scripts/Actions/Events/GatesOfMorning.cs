using System;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_the_white)
// 4-part FP:    FP gain Hope + FP armies +8% atk | DS emissaries ArcaneInsight (dawn reveals truth) | FP cavalry slowed 1 (morning mist) | DS gain Despair + DS armies -12% atk
// neutral rule: FP Hope 1 | DS Despair 1 (symmetric)
public class GatesOfMorning : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        int hopeful = 0, despairing = 0, dsInsight = 0, fpCavSlowed = 0;

        if (caster == AlignmentEnum.neutral)
        {
            // 1 bonus for all: all chars gain Hope (morning lifts all hearts)
            // 1 debuff for all: all chars slowed 1 (the dawn light blinds tired eyes)
            int hoped = 0, slowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); hoped++;
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++;
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Gates of Morning (ongoing): {hoped} characters gain Hope; {slowed} slowed by dawn.", Color.yellow);
            return;
        }

        if (env != null) { env.FreePeopleArmyAttackFactor = 1.08f; env.DarkServantsArmyAttackFactor = 0.88f; }

        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); hopeful++;                     // big bonus own
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsInsight++; } // small bonus other
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); despairing++;                // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Gates of Morning (ongoing): {hopeful} FP gain Hope (+8% atk); {despairing} DS despaired (-12% atk); {dsInsight} DS emissaries read the dawn; {fpCavSlowed} FP cavalry slowed by morning mist.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
