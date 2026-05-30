using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (tharkun), dark (the_iron_crown), neutral (the_white_hand)
// 4-part FP:    Dwarves navigate freely + FP mountain units only 10% freeze | FP mountain non-dwarf slowed 1 | enemy emissaries in mountain gain ArcaneInsight | enemy mountain 30% freeze + halted + cavalry loses 1 unit 25%
// 4-part dark:  Orcs/Trolls in mountain gain Hidden | own mountain non-Orc slowed 1 | FP dwarves also affected (no immunity when dark plays) | FP mountain 35% freeze + halted
// neutral rule: all non-Dwarf mountain slowed 1; 25% freeze; cavalry 25% lose 1 unit
public class FrozenPasses : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            int slowed = 0, frozen = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards()).ToList())
                {
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++;
                    if (UnityEngine.Random.value < 0.25f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                    if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && (a.lc > 0 || a.hc > 0) && UnityEngine.Random.value < 0.25f) { if (a.lc > 0) a.lc = Mathf.Max(0, a.lc - 1); else a.hc = Mathf.Max(0, a.hc - 1); } }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Frozen Passes (ongoing): {slowed} mountain units slowed; {frozen} frozen. Dwarves navigate freely.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownBig = 0, ownDebuff = 0, otherSmall = 0, otherBig = 0;

        foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                bool isDwarfOrImmune = ch.race == RacesEnum.Dwarf || ch.IsImmuneToNegativeEnvironmentalCards();

                if (ch.GetAlignment() == caster)
                {
                    if (caster == AlignmentEnum.freePeople && (ch.race == RacesEnum.Dwarf)) continue; // Dwarves unaffected when FP plays
                    if (caster == AlignmentEnum.darkServants && (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin || ch.race == RacesEnum.Troll))
                    { ch.Hide(1); ownBig++; continue; }                                             // big bonus dark: mountain creatures hidden
                    if (caster == AlignmentEnum.freePeople && UnityEngine.Random.value < 0.10f)
                    { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); }                           // own FP: lower freeze chance
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownDebuff++;           // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    if (!isDwarfOrImmune || caster == AlignmentEnum.darkServants)
                    {
                        float chance = caster == AlignmentEnum.freePeople ? 0.30f : 0.35f;
                        if (UnityEngine.Random.value < chance) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); otherBig++; }
                        ch.Halt(1);
                        Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                        if (a != null && (a.lc > 0 || a.hc > 0) && UnityEngine.Random.value < 0.25f) { if (a.lc > 0) a.lc = Mathf.Max(0, a.lc - 1); else a.hc = Mathf.Max(0, a.hc - 1); }
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Frozen Passes (ongoing): {ownBig} own mountain units buffed; {otherBig} enemies frozen; {ownDebuff} own slowed; {otherSmall} enemy emissaries gain insight.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var enemies = board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            foreach (var e in enemies) { e.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); e.Halt(1); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Frozen Passes: {enemies.Count} enemy mountain units frozen and halted.", Color.cyan);
            return enemies.Count > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character?.GetAlignment() && ch.race != RacesEnum.Dwarf));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
