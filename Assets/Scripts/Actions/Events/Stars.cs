using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (mithrandir), dark (the_deceiver)
// 4-part FP:    Elves gain Hope + hidden Elves may act again | FP cavalry slowed 1 (gazing at stars) | DS emissaries ArcaneInsight | hidden DS revealed by starlight + DS armies -8% atk
// 4-part dark:  DS in mountains gain Hidden + DS mages ArcaneInsight | DS in open slowed 1 | FP emissaries ArcaneInsight (stars guide all) | FP in open revealed + FP armies -8% atk
public class Stars : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;

        if (env != null)
        {
            if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.92f;
            else env.FreePeopleArmyAttackFactor = 0.92f;
        }

        int ownBig = 0, ownSmallDebuff = 0, otherSmall = 0, otherBig = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isMountain = hex.terrainType == TerrainEnum.mountains;
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (caster == AlignmentEnum.freePeople)
                    {
                        if (ch.race == RacesEnum.Elf) { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); if (ch.HasStatusEffect(StatusEffectEnum.Hidden)) ch.hasActionedThisTurn = false; ownBig++; } // big bonus FP
                        Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                        if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSmallDebuff++; } // small debuff FP
                    }
                    else // dark
                    {
                        if (isMountain) { ch.Hide(1); ownBig++; }                                  // big bonus dark
                        if (ch.GetMage() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); ownBig++; }
                        if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSmallDebuff++; } // small debuff dark
                    }
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    if (caster == AlignmentEnum.freePeople && ch.HasStatusEffect(StatusEffectEnum.Hidden)) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); otherBig++; } // big debuff other (FP deck)
                    if (caster == AlignmentEnum.darkServants && isOpen) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); otherBig++; } // big debuff other (dark deck)
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Stars (ongoing): {ownBig} own characters gifted; {otherBig} enemies exposed; {otherSmall} enemy emissaries guided.",
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
            var elves = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf && ch.hex != null).Distinct().ToList();
            if (elves.Count == 0) return false;
            int revealed = 0, activated = 0;
            foreach (var elf in elves)
            {
                foreach (var adj in elf.hex.GetHexesInRadius(1).Where(h => h?.characters != null))
                    foreach (var enemy in adj.characters.Where(e => e != null && !e.killed && e.GetAlignment() != character.GetAlignment() && e.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                    { enemy.ClearStatusEffect(StatusEffectEnum.Hidden); revealed++; }
                if (elf.HasStatusEffect(StatusEffectEnum.Hidden)) { elf.hasActionedThisTurn = false; activated++; }
            }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Stars: {revealed} hidden enemies revealed; {activated} hidden Elves may act again.", Color.cyan);
            return revealed > 0 || activated > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
