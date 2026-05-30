using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_base, saruman_the_white), dark (the_deceiver)
// 4-part FP:    own naval +1 warship (Ulmo aids FP) | own naval slowed 1 | enemy naval halted | enemy coastal slowed 2 + FP coastal slowed 1
// 4-part dark:  own naval +1 warship | own naval slowed 1 | enemy naval halted | enemy coastal slowed 2
// neutral rule: all naval halted + slowed 1 | coastal chars slowed 1
public class FuryOfUlmo : EventAction
{
    private static bool IsNaval(Character ch)
    {
        if (ch == null || ch.killed) return false;
        if (ch.isEmbarked) return true;
        if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && a.ws > 0) return true; }
        return false;
    }

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            // bonus: Burning cleared from all naval (storm extinguishes fire)
            // debuff: all naval halted + slowed; coastal slowed
            int burningCleared = 0, navalHalted = 0, coastalSlowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool isCoastal = hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain();
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (IsNaval(ch))
                    {
                        if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.ClearStatusEffect(StatusEffectEnum.Burning); burningCleared++; }
                        ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ch.Halt(1); navalHalted++;
                    }
                    else if (isCoastal) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); coastalSlowed++; }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Fury of Ulmo (ongoing): {burningCleared} Burning cleared; {navalHalted} naval halted; {coastalSlowed} coastal slowed.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownNavalBuff = 0, ownSlowed = 0, enemyHalted = 0, enemyCoastSlowed = 0;

        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isCoastal = hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain();
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster && IsNaval(ch))
                {
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null) { a.ws++; ownNavalBuff++; }                                     // big bonus own: warship growth
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++;           // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (IsNaval(ch)) { ch.Halt(1); enemyHalted++; }                               // big debuff other: naval halted
                    else if (isCoastal) { ch.moved = Mathf.Min(ch.moved + 2, ch.GetMaxMovement()); enemyCoastSlowed++; } // big debuff other: coastal slowed 2
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Fury of Ulmo (ongoing): {ownNavalBuff} own warships grow; {enemyHalted} enemy naval halted; {enemyCoastSlowed} enemy coastal slowed; {ownSlowed} own naval slowed.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (ch) =>
        {
            if (originalEffect != null && !originalEffect(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var targets = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters).Where(IsNaval).Distinct().ToList();
            if (targets.Count == 0) return false;
            int cleared = 0;
            foreach (var t in targets) { if (t.HasStatusEffect(StatusEffectEnum.Burning)) { t.ClearStatusEffect(StatusEffectEnum.Burning); cleared++; } t.Halt(); }
            MessageDisplayNoUI.ShowMessage(ch.hex, ch, $"Fury of Ulmo halts {targets.Count} naval units; extinguishes {cleared} Burning.", Color.cyan);
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(IsNaval));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
