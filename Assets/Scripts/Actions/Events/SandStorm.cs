using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base, shadow_of_the_east, the_deceiver), neutral (of_many_colours)
// 4-part dark:    Southron/Easterling encouraged + get Strengthened | own non-Southron in desert slowed 1 | FP emissaries gain ArcaneInsight (desert winds carry secrets) | non-Southron desert units halted + cavalry 33% freeze
// neutral rule:   Southrons encouraged | all non-Southron desert halted
public class SandStorm : EventAction
{
    private static bool IsDesert(Hex h) => h != null && (h.terrainType == TerrainEnum.desert || h.terrainType == TerrainEnum.wastelands);
    private static bool IsDesertRace(Character ch) => ch.race == RacesEnum.Southron || ch.race == RacesEnum.Easterling;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        AlignmentEnum other = caster == AlignmentEnum.darkServants ? AlignmentEnum.freePeople
            : caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.neutral;

        int southronBuff = 0, ownSlowed = 0, otherSmall = 0, halted = 0, frozen = 0, blocked = 0;

        foreach (var (ch, hex) in board.GetHexes().Where(h => IsDesert(h) && h.characters != null)
            .SelectMany(h => h.characters.Select(ch => (ch, h))).Where(t => t.ch != null && !t.ch.killed))
        {
            bool isSouthron = IsDesertRace(ch);

            if (isSouthron)
            {
                ch.Encourage(1); southronBuff++;                                               // big bonus own: Southron home terrain
                if (caster != AlignmentEnum.neutral) ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                continue;
            }

            if (!ch.IsImmuneToNegativeEnvironmentalCards())
            {
                if (caster != AlignmentEnum.neutral && ch.GetAlignment() == caster)
                { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; continue; } // small debuff own

                if (caster != AlignmentEnum.neutral && ch.GetAlignment() == other && ch.GetEmmissary() > 0)
                { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; }         // small bonus other

                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); halted++;                 // big debuff: slowed
                Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;
                if (army != null)
                {
                    if ((army.lc > 0 || army.hc > 0) && UnityEngine.Random.value < 0.33f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                    if (army.ca > 0 && UnityEngine.Random.value < 0.25f) { ch.ApplyStatusEffect(StatusEffectEnum.Blocked, 1); blocked++; }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Sand Storm (ongoing): {southronBuff} Southrons strengthened; {halted} others slowed; {frozen} cavalry frozen; {blocked} siege blocked; {otherSmall} enemy emissaries guided.",
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
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var targets = board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.desert && h.characters != null)
                .SelectMany(h => h.characters).Where(x => x != null && !x.killed && !IsDesertRace(x) && !x.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            if (targets.Count == 0) return false;
            foreach (var t in targets) t.Halt();
            MessageDisplayNoUI.ShowMessage(ch.hex, ch, $"Sand Storm halts {targets.Count} non-Southron units on desert tiles.", Color.yellow);
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.desert && h.characters != null && h.characters.Any(x => x != null && !x.killed && !IsDesertRace(x) && !x.IsImmuneToNegativeEnvironmentalCards()));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
