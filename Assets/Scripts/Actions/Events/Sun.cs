using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_base, saruman_the_white)
// 4-part FP:    Men/Hobbits/Dunedain encouraged + cavalry haste | own Undead slowed 1 | Southron/Easterling small move bonus (sun is their home) | Trolls halted + Undead despaired + DS armies -10% atk
// neutral rule: all Men/Hobbits haste 1 | Trolls halted
public class Sun : EventAction
{
    private static bool IsManOrHobbit(RacesEnum r) =>
        r == RacesEnum.Common || r == RacesEnum.Dunedain || r == RacesEnum.Hobbit;
    private static bool IsDesertRace(RacesEnum r) =>
        r == RacesEnum.Southron || r == RacesEnum.Easterling;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed).Distinct().ToList();

        if (caster == AlignmentEnum.neutral)
        {
            int hastened = 0, halted = 0;
            foreach (var ch in allChars)
            {
                if (IsManOrHobbit(ch.race)) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); hastened++; }
                if (ch.race == RacesEnum.Troll) { ch.Halt(1); halted++; }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Sun (ongoing): {hastened} Men/Hobbits hasted; {halted} Trolls halted.", Color.yellow);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.90f;
            else env.FreePeopleArmyAttackFactor = 0.90f;
        }

        int ownEncouraged = 0, ownCavHasted = 0, ownUndeadSlowed = 0, enemyHalted = 0, enemyDespaired = 0, desertBonus = 0;
        foreach (var ch in allChars)
        {
            if (ch.GetAlignment() == caster)
            {
                if (IsManOrHobbit(ch.race)) { ch.Encourage(1); ownEncouraged++; }                         // big bonus own: Men/Hobbit/Dunedain encouraged
                Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;
                if (army != null && (army.lc > 0 || army.hc > 0)) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ownCavHasted++; }
                if (ch.race == RacesEnum.Undead) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownUndeadSlowed++; } // small debuff own: undead weakened by sun
            }
            else if (ch.GetAlignment() == other)
            {
                if (IsDesertRace(ch.race)) { ch.moved = Mathf.Max(0, ch.moved - 1); desertBonus++; }     // small bonus other: desert races thrive
                if (ch.race == RacesEnum.Troll) { ch.Halt(1); enemyHalted++; }                            // big debuff other: Trolls halted
                if (ch.race == RacesEnum.Undead) { ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); enemyDespaired++; } // big debuff: undead despaired
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Sun (ongoing): {ownEncouraged} own Men/Hobbits encouraged; {ownCavHasted} cavalry hasted; {enemyHalted} Trolls halted; {enemyDespaired} Undead despaired.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (ch) =>
        {
            if (originalEffect != null && !originalEffect(ch)) return false;
            if (ch == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var humans = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(x => x != null && !x.killed && (x.race == RacesEnum.Common || x.race == RacesEnum.Dunedain || x.race == RacesEnum.Hobbit)).Distinct().ToList();
            var trolls = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(x => x != null && !x.killed && x.race == RacesEnum.Troll).Distinct().ToList();
            foreach (var x in humans) x.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            foreach (var x in trolls) x.Halt();
            MessageDisplayNoUI.ShowMessage(ch.hex, ch, $"Sun: {humans.Count} Humans/Hobbits encouraged; {trolls.Count} Trolls halted.", Color.yellow);
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(x => x != null && !x.killed && (x.race == RacesEnum.Common || x.race == RacesEnum.Dunedain || x.race == RacesEnum.Hobbit || x.race == RacesEnum.Troll)));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
