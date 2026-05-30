using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_the_white, stormcrow)
// 4-part: FP Humans/Dunedain encouraged+hope + cavalry haste + FP armies +8% atk | FP mages slowed 1 (light distracts study) | DS emissaries ArcaneInsight | DS in open despaired + DS armies -12% atk
public class FirstLightOnTheThirdDay : EventAction
{
    private const int Radius = 3;
    private static bool IsHumanOrDunedain(Character ch) => ch != null && (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.FreePeopleArmyAttackFactor = 1.08f; env.DarkServantsArmyAttackFactor = 0.88f; }

        int encouraged = 0, cavalryHasted = 0, fpMageSlowed = 0, dsSmall = 0, despaired = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (IsHumanOrDunedain(ch) && ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.Encourage(1); ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); encouraged++;  // big bonus own
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); cavalryHasted++; }
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople && ch.GetMage() > 0)
                { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpMageSlowed++; }        // small debuff own: mages distracted by the light
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsSmall++; } // small bonus other
                    if (isOpen && !ch.IsImmuneToNegativeEnvironmentalCards()) { ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); despaired++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"First Light (ongoing): {encouraged} Humans/Dunedain encouraged+hope (+8% atk); {cavalryHasted} cavalry hasted; {despaired} DS in open despaired (-12% atk); {dsSmall} DS emissaries guided.",
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
            var targets = character.hex.GetHexesInRadius(Radius).Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople && IsHumanOrDunedain(ch)).Distinct().ToList();
            if (targets.Count == 0) return false;
            foreach (var t in targets) { t.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1); t.ApplyStatusEffect(StatusEffectEnum.Hope, 1); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"First Light: {targets.Count} Humans/Dunedain in radius {Radius} gain Courage and Hope.", Color.yellow);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(Radius).Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople && IsHumanOrDunedain(ch)));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
