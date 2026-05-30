using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: dark only (the_necromancer)
// 4-part: DS mages gain ArcaneInsight + DS forest agents Hidden | DS non-forest slowed 1 | FP forest emissaries gain ArcaneInsight (detecting corruption) | FP forest poisoned + despaired + DS +5% atk
public class SmokeOfDolGuldur : EventAction
{
    private static bool IsNecromancer(Character ch) => ch != null && ch.GetMage() > 0 && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.DarkServantsArmyAttackFactor = 1.05f;

        int dsInsight = 0, dsHidden = 0, dsSlowed = 0, fpSmall = 0, fpPoisoned = 0, fpDespaired = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isForest = hex.terrainType == TerrainEnum.forest;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (IsNecromancer(ch)) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsInsight++; }   // big bonus own: mages
                    if (isForest && ch.GetAgent() > 0) { ch.Hide(1); dsHidden++; }                                     // big bonus own: agents
                    if (!isForest) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; }             // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (isForest && ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpSmall++; } // small bonus other
                    if (isForest && !ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1); fpPoisoned++; ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); fpDespaired++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Smoke of Dol Guldur (ongoing): {dsInsight} dark mages insightful; {dsHidden} dark agents hidden; {fpPoisoned} FP forest chars poisoned; {fpSmall} FP emissaries detect corruption.",
            Color.gray);
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
            var victims = board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && !IsNecromancer(ch) && ch.GetAlignment() != AlignmentEnum.darkServants && !ch.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            foreach (var v in victims) v.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
            MessageDisplayNoUI.ShowMessage(character?.hex, character, $"Smoke of Dol Guldur: {victims.Count} forest units poisoned.", Color.gray);
            return victims.Count > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsNecromancer(ch) && ch.GetAlignment() != AlignmentEnum.darkServants));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
