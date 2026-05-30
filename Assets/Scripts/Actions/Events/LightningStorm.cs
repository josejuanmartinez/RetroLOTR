using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: dark only (the_deceiver)
// 4-part: DS in concealing terrain gain Hidden | DS in exposed terrain 5% struck (lightning finds everyone) | FP mages gain ArcaneInsight (lightning-reading) | FP in exposed terrain 15% struck + cavalry loss + FP armies -10% atk
public class LightningStorm : EventAction
{
    private static readonly HashSet<TerrainEnum> ExposedTerrain = new() { TerrainEnum.plains, TerrainEnum.wastelands, TerrainEnum.desert, TerrainEnum.shore };
    private static readonly HashSet<TerrainEnum> ConcealTerrain = new() { TerrainEnum.forest, TerrainEnum.mountains, TerrainEnum.swamp };

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.FreePeopleArmyAttackFactor = 0.90f;

        int dsHidden = 0, dsStruck = 0, fpSmall = 0, fpStruck = 0, revealed = 0, cavLost = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isExposed = ExposedTerrain.Contains(hex.terrainType);
            bool isConceal = ConcealTerrain.Contains(hex.terrainType);
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (isConceal) { ch.Hide(1); dsHidden++; }                                     // big bonus own
                    if (isExposed && UnityEngine.Random.value < 0.05f) { ch.Wounded(null, 8); dsStruck++; } // small debuff own: lightning finds even dark chars
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (ch.GetMage() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpSmall++; } // small bonus other
                    if (isExposed && !ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (ch.HasStatusEffect(StatusEffectEnum.Hidden)) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); revealed++; }
                        if (UnityEngine.Random.value < 0.15f) { ch.Wounded(null, 15); fpStruck++; } // big debuff other
                        Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                        if (a != null && (a.lc > 0 || a.hc > 0) && UnityEngine.Random.value < 0.15f) { if (a.lc > 0) a.lc = Mathf.Max(0, a.lc - 1); else a.hc = Mathf.Max(0, a.hc - 1); cavLost++; }
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Lightning Storm (ongoing): {dsHidden} DS hidden in shelter; {fpStruck} FP struck for 15; {revealed} FP revealed; {cavLost} FP cavalry lost; {fpSmall} FP mages read the storm.",
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
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var enemies = board.GetHexes().Where(h => h != null && ExposedTerrain.Contains(h.terrainType) && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()).Distinct().ToList();
            int struck = 0, revealed = 0;
            foreach (var e in enemies) { e.Wounded(null, 15); struck++; if (e.HasStatusEffect(StatusEffectEnum.Hidden)) { e.ClearStatusEffect(StatusEffectEnum.Hidden); revealed++; } }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Lightning Storm: {struck} enemies struck 15 HP; {revealed} revealed.", Color.yellow);
            return struck > 0 || revealed > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && ExposedTerrain.Contains(h.terrainType) && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character?.GetAlignment()));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
