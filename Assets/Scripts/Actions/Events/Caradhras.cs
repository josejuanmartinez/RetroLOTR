using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (tharkun), dark (the_iron_crown), neutral (of_many_colours, the_white_hand)
// 4-part FP:    own mountain Dwarves: Hope + encouraged | own non-dwarf mountain slowed 1 | enemy mountain emissaries: ArcaneInsight | enemy mountain 30% freeze + all commanders halted
// 4-part dark:  own Orc/Troll/Goblin mountain: Hidden + Haste | own mountain non-Orc slowed 1 | FP mountain emissaries ArcaneInsight | FP mountain 40% freeze + halted
// neutral rule: all non-Dwarf mountain 20% freeze; all commanders halted
public class Caradhras : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            int frozen = 0, halted = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards()).ToList())
                {
                    if (UnityEngine.Random.value < 0.20f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                    if (ch.IsArmyCommander()) { ch.Halt(1); halted++; }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Caradhras (ongoing): {frozen} mountain units frozen; {halted} commanders halted. Dwarves unaffected.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownBig = 0, ownDebuff = 0, otherSmall = 0, otherBig = 0;

        foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (caster == AlignmentEnum.freePeople && ch.race == RacesEnum.Dwarf) { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); ch.Encourage(1); ownBig++; continue; } // big bonus FP
                    if (caster == AlignmentEnum.darkServants && (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin || ch.race == RacesEnum.Troll))
                    { ch.Hide(1); ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ownBig++; continue; }                   // big bonus dark
                    if (ch.race != RacesEnum.Dwarf) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownDebuff++; } // small debuff own
                }
                else if (ch.GetAlignment() == other && !ch.IsImmuneToNegativeEnvironmentalCards())
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; }  // small bonus other
                    float chance = caster == AlignmentEnum.freePeople ? 0.30f : 0.40f;
                    if (ch.race != RacesEnum.Dwarf || caster == AlignmentEnum.darkServants)
                    {
                        if (UnityEngine.Random.value < chance) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); otherBig++; }
                        if (ch.IsArmyCommander()) ch.Halt(1);
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Caradhras (ongoing): {ownBig} own mountain units gifted; {otherBig} enemies frozen; {ownDebuff} own slowed; {otherSmall} enemy emissaries inspired.",
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
            if (ch == null || ch.hex == null) return false;
            var enemies = ch.hex.GetHexesInRadius(5).Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters).Where(x => x != null && !x.killed && x.GetAlignment() != ch.GetAlignment()).Distinct().ToList();
            if (enemies.Count == 0) return false;
            foreach (var e in enemies) e.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
            MessageDisplayNoUI.ShowMessage(ch.hex, ch, $"Caradhras freezes {enemies.Count} enemy mountain units in radius 5.", Color.cyan);
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            if (ch == null || ch.hex == null) return false;
            return ch.hex.GetHexesInRadius(5).Any(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null && h.characters.Any(x => x != null && !x.killed && x.GetAlignment() != ch.GetAlignment()));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
