using System;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_the_white)
// 4-part FP:    FP gain Haste + FP armies +8% atk | DS on open terrain revealed (bright day) | FP cavalry slowed 1 (exposed in open sun) | DS armies -10% atk + DS in open revealed
// neutral rule: all gain Haste 1
public class CloudlessDay : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            // 1 bonus for all: Haste (clear skies lift all spirits)
            // 1 debuff for all: hidden chars in open terrain revealed (nowhere to hide)
            int hastened = 0, revealed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); hastened++;
                    if (isOpen && ch.HasStatusEffect(StatusEffectEnum.Hidden)) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); revealed++; }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Cloudless Day (ongoing): {hastened} characters gain Haste; {revealed} hidden characters revealed in the open.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (caster == AlignmentEnum.freePeople) { env.FreePeopleArmyAttackFactor = 1.08f; env.DarkServantsArmyAttackFactor = 0.90f; }
            else { env.DarkServantsArmyAttackFactor = 1.08f; env.FreePeopleArmyAttackFactor = 0.90f; }
        }

        int ownHasted = 0, ownCavSlowed = 0, otherRevealed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ownHasted++;                  // big bonus own
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == other && isOpen && ch.HasStatusEffect(StatusEffectEnum.Hidden))
                { ch.ClearStatusEffect(StatusEffectEnum.Hidden); otherRevealed++; }                // big debuff other: no hiding on a cloudless day
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Cloudless Day (ongoing): {ownHasted} own units hasted; {otherRevealed} enemies revealed in the open.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
