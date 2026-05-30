using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: FP only (mithrandir)
// 4-part: FP gain Hope + fear cleared in open | FP cavalry slowed 1 (light slows cautious cavalry) | DS emissaries ArcaneInsight | DS in open despaired + revealed + FP armies +8% atk
public class ALightInDarkPlaces : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.FreePeopleArmyAttackFactor = 1.08f; }

        int hoped = 0, fearCleared = 0, fpCavSlowed = 0, dsSmall = 0, darkDespaired = 0, darkRevealed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.hills;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); hoped++;               // big bonus own
                    if (isOpen) { ch.ClearStatusEffect(StatusEffectEnum.Fear); fearCleared++; }
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsSmall++; } // small bonus other
                    if (isOpen && !ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); darkDespaired++; ch.ClearStatusEffect(StatusEffectEnum.Hidden); darkRevealed++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"A Light in Dark Places (ongoing): {hoped} FP hope (+8% atk); {fearCleared} fears cleared; {darkDespaired} DS in open despaired+revealed; {dsSmall} DS emissaries sense the light.",
            Color.yellow);
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
            var allies = character.hex.GetHexesInRadius(2).Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople).Distinct().ToList();
            foreach (var a in allies) { a.ApplyStatusEffect(StatusEffectEnum.Hope, 1); a.ClearStatusEffect(StatusEffectEnum.Fear); a.ClearStatusEffect(StatusEffectEnum.Despair); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"A Light in Dark Places: {allies.Count} allied units in radius 2 gain Hope and lose Fear/Despair.", Color.yellow);
            return allies.Count > 0;
        };
        condition = (character) => character != null && character.hex != null;
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
