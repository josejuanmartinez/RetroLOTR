using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (tharkun), dark (the_dark_eye, the_iron_crown, the_necromancer), neutral (of_many_colours, the_white_hand, sharkey)
// 4-part FP:    own mountain Dwarves: Hope | own mountain non-dwarf 5% freeze | enemy mountain 15% freeze + cavalry 25% freeze + slowed 1 | DS armies -8% atk
// 4-part dark:  own Orcs/Goblins in mountain: Strengthened | own mountain non-Orc slowed 1 | FP cavalry 20% freeze | FP armies -8% atk + FP mountain halted
// neutral rule: all mountain non-Dwarf 7.5% freeze; all mountain units slowed 1
public class CruelWinter : EventAction
{
    private const float NeutralFreezeChance = 0.075f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            int frozen = 0, slowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards()).ToList())
                {
                    bool isCav = ch.IsArmyCommander() && ch.GetArmy() is Army a && (a.lc > 0 || a.hc > 0);
                    if (UnityEngine.Random.value < (isCav ? 0.15f : NeutralFreezeChance)) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++;
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Cruel Winter (ongoing): {frozen} mountain units frozen; {slowed} slowed. Dwarves unaffected.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.92f;
            else env.FreePeopleArmyAttackFactor = 0.92f;
        }

        int ownBig = 0, ownDebuff = 0, otherFrozen = 0, otherHalted = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (caster == AlignmentEnum.freePeople && ch.race == RacesEnum.Dwarf) { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); ownBig++; continue; }
                    if (caster == AlignmentEnum.darkServants && (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin)) { ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1); ownBig++; continue; }
                    if (!ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (UnityEngine.Random.value < 0.05f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); }
                        ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownDebuff++;    // small debuff own
                    }
                }
                else if (ch.GetAlignment() == other && !ch.IsImmuneToNegativeEnvironmentalCards())
                {
                    bool isCav = ch.IsArmyCommander() && ch.GetArmy() is Army a && (a.lc > 0 || a.hc > 0);
                    float chance = isCav ? 0.25f : 0.15f;
                    if (UnityEngine.Random.value < chance) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); otherFrozen++; }
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
                    if (caster == AlignmentEnum.darkServants && ch.IsArmyCommander()) { ch.Halt(1); otherHalted++; }     // extra penalty for dark deck
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Cruel Winter (ongoing): {ownBig} own mountain units gifted; {otherFrozen} enemies frozen; {otherHalted} enemy commanders halted; {ownDebuff} own slowed.",
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
            var enemies = board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters).Where(x => x != null && !x.killed && x.GetAlignment() != ch.GetAlignment() && !x.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            int frozen = 0;
            foreach (var e in enemies) if (UnityEngine.Random.value < 0.075f) { e.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
            MessageDisplayNoUI.ShowMessage(ch?.hex, ch, $"Cruel Winter: {frozen}/{enemies.Count} enemy mountain units frozen.", Color.cyan);
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null && h.characters.Any(x => x != null && !x.killed && x.GetAlignment() != ch.GetAlignment() && !x.IsImmuneToNegativeEnvironmentalCards()));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
