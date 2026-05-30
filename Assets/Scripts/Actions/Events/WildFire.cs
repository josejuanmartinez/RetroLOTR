using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base, sharkey, the_necromancer), neutral (the_white_hand)
// 4-part dark:    DS Orc/Goblin in forest gain Haste (fire drives them) | own DS 5% burning chance (collateral) | FP forest emissaries gain ArcaneInsight (reading smoke signs) | enemy forest 20% burning + burning-halted
// neutral rule:   all forest chars 12% burning | burning chars halted
public class WildFire : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            // bonus: open terrain chars gain +1 movement (fire drives all to open ground)
            // debuff: forest chars 12% burning; burning chars halted
            int openBoosted = 0, ignited = 0, halted = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool isForest = hex.terrainType == TerrainEnum.forest;
                bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (isOpen) { ch.moved = Mathf.Max(0, ch.moved - 1); openBoosted++; }
                    if (isForest && !ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.Halt(1); halted++; }
                        else if (UnityEngine.Random.value < 0.12f) { ch.ApplyStatusEffect(StatusEffectEnum.Burning, 1); ignited++; }
                    }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Wild Fire (ongoing): {openBoosted} open-terrain chars move faster; {ignited} forest units burning; {halted} burning halted.", Color.red);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownHasted = 0, ownBurned = 0, otherSmall = 0, otherIgnited = 0, otherHalted = 0;

        foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ownHasted++; } // big bonus own
                    else if (!ch.IsImmuneToNegativeEnvironmentalCards() && UnityEngine.Random.value < 0.05f) { ch.ApplyStatusEffect(StatusEffectEnum.Burning, 1); ownBurned++; } // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other: emissaries read the smoke
                    if (!ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.Halt(1); otherHalted++; }
                        else if (UnityEngine.Random.value < 0.20f) { ch.ApplyStatusEffect(StatusEffectEnum.Burning, 1); otherIgnited++; } // big debuff other
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Wild Fire (ongoing): {ownHasted} own Orcs/Goblins hasted; {otherIgnited} enemies ignited; {otherHalted} burning enemies halted; {ownBurned} own caught in smoke.",
            Color.red);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;
            var targets = character.hex.GetHexesInRadius(2).Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            if (targets.Count == 0) return false;
            foreach (var t in targets) t.ApplyStatusEffect(StatusEffectEnum.Burning, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Wild Fire burns {targets.Count} enemy units on forest tiles in radius 2.", Color.red);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(2).Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
