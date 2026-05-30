using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base, the_dark_eye, the_iron_crown, the_necromancer), neutral (the_white_hand)
// 4-part dark:   Orcs/dark mountain units get Hidden | own non-mountain slowed 1 | FP 10% freeze + cavalry 20% freeze | DS +5% atk
// neutral rule:  all 5% freeze (dwarves immune) | all non-mountain slowed 1
public class Snow : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            // bonus: all chars in mountains gain Hidden (blizzard covers everyone)
            // debuff: all non-Dwarf chars 5% freeze + non-mountain slowed 1
            int hidden = 0, frozen = 0, slowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (hex.terrainType == TerrainEnum.mountains) { ch.Hide(1); hidden++; }
                    if (ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (UnityEngine.Random.value < 0.05f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                        if (hex.terrainType != TerrainEnum.mountains) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++; }
                    }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Snow (ongoing): {hidden} mountain chars hidden; {frozen} frozen; {slowed} slowed.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (caster == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 1.05f;
            else env.FreePeopleArmyAttackFactor = 1.05f;
        }

        int ownHidden = 0, ownSlowed = 0, otherFrozen = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isMountain = hex.terrainType == TerrainEnum.mountains;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (isMountain && (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin || ch.race == RacesEnum.Troll))
                    { ch.Hide(1); ownHidden++; }                                        // big bonus own: dark mountain creatures hidden
                    if (!isMountain) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == other && !ch.IsImmuneToNegativeEnvironmentalCards() && ch.race != RacesEnum.Dwarf)
                {
                    bool isCavalry = ch.IsArmyCommander() && ch.GetArmy() is Army a && (a.lc > 0 || a.hc > 0);
                    float chance = isCavalry ? 0.20f : 0.10f;
                    if (UnityEngine.Random.value < chance) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); otherFrozen++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Snow (ongoing): {ownHidden} own mountain creatures hidden; {otherFrozen} enemies frozen; {ownSlowed} own units slowed.",
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
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            int frozen = 0;
            foreach (var ch in board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != GetCasterAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards() && ch.race != RacesEnum.Dwarf).ToList())
                if (UnityEngine.Random.value < 0.10f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
            MessageDisplayNoUI.ShowMessage(character?.hex, character, $"Snow falls: {frozen} enemy unit(s) frozen.", Color.cyan);
            return true;
        };
        condition = (character) => character != null;
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
