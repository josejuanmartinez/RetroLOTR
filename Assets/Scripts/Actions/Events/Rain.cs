using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), dark (sauron_base), neutral (saruman_base)
// 4-part FP:    burning cleared all (universal good) + own cavalry speed preserved | own slowed 1 | enemy cavalry slowed 2 + siege 40% halt | FP armies +5% atk
// 4-part dark:  burning cleared + DS gain +5% atk | own siege 25% halt | FP cavalry slowed 2 + siege 50% halt | FP armies -10% atk
// neutral rule: burning cleared, everyone slowed 1, siege 20% halt
public class Rain : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        AlignmentEnum caster = GetCasterAlignment();
        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants
            : caster == AlignmentEnum.darkServants ? AlignmentEnum.freePeople : AlignmentEnum.neutral;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed).Distinct().ToList();

        int burningCleared = 0, ownSlowed = 0, otherSlowed = 0, siegeHalted = 0;
        foreach (Character ch in allChars)
        {
            if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.ClearStatusEffect(StatusEffectEnum.Burning); burningCleared++; }

            if (caster == AlignmentEnum.neutral)
            {
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++;
                if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && a.ca > 0 && UnityEngine.Random.value < 0.20f) { ch.Halt(1); siegeHalted++; } }
                continue;
            }

            bool isOwn = ch.GetAlignment() == caster;
            bool isOther = ch.GetAlignment() == other;
            Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;

            if (isOwn)
            {
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++;                  // small debuff own: everyone slowed 1
                if (army != null && army.ca > 0 && UnityEngine.Random.value < (caster == AlignmentEnum.darkServants ? 0.25f : 0.15f))
                { ch.Halt(1); siegeHalted++; }                                                          // own siege occasionally halted
            }
            else if (isOther)
            {
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); otherSlowed++;
                if (army != null && (army.lc > 0 || army.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); otherSlowed++; } // big debuff other: cavalry extra slowed
                if (army != null && army.ca > 0 && UnityEngine.Random.value < (caster == AlignmentEnum.freePeople ? 0.40f : 0.50f))
                { ch.Halt(1); siegeHalted++; }                                                          // big debuff other: siege heavily penalised
            }
        }

        // Combat modifiers
        if (env != null && caster != AlignmentEnum.neutral)
        {
            if (caster == AlignmentEnum.freePeople) env.FreePeopleArmyAttackFactor = 1.05f;
            else env.DarkServantsArmyAttackFactor = 1.05f;
            if (other == AlignmentEnum.freePeople) env.FreePeopleArmyAttackFactor = 0.90f;
            else if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.90f;
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Rain (ongoing): {burningCleared} Burning cleared; {ownSlowed} own slowed; {otherSlowed} enemy slowed; {siegeHalted} siege halted.",
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
            if (character == null || character.hex == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            int cleared = 0;
            foreach (var ch in board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed).ToList())
                if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.ClearStatusEffect(StatusEffectEnum.Burning); cleared++; }
            var slowed = character.hex.GetHexesInRadius(2).Where(h => h != null && h.characters != null).SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed).Distinct().ToList();
            foreach (var ch in slowed) ch.moved = Mathf.Min(ch.moved + 3, ch.GetMaxMovement());
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rain: {cleared} Burning cleared; {slowed.Count} units slowed in radius 2.", Color.cyan);
            return true;
        };
        condition = (character) => character != null && character.hex != null;
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
