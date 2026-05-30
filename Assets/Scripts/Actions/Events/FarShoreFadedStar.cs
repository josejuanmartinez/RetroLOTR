using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: neutral (saruman_the_white) but FP-themed
// 4-part (treat as FP): FP near border + naval: Hope + Encouraged | FP cavalry slowed 1 | DS emissaries ArcaneInsight | DS near border halted + FP armies +5%
public class FarShoreFadedStar : EventAction
{
    private const int BorderDistance = 5;

    private static bool IsNearBorder(Hex hex, Board board)
    {
        if (hex == null || board?.hexes == null) return false;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var h in board.hexes.Values) { if (h == null) continue; minX = Mathf.Min(minX, h.v2.x); maxX = Mathf.Max(maxX, h.v2.x); minY = Mathf.Min(minY, h.v2.y); maxY = Mathf.Max(maxY, h.v2.y); }
        return hex.v2.x <= minX + BorderDistance || hex.v2.x >= maxX - BorderDistance || hex.v2.y <= minY + BorderDistance || hex.v2.y >= maxY - BorderDistance;
    }

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        // Neutral rule: all border chars Encouraged (the star guides all wanderers) | all cavalry slowed (long marches exhaust horses)
        if (caster == AlignmentEnum.neutral)
        {
            int borderBoosted = 0, cavSlowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool nearBorder = IsNearBorder(hex, board);
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (nearBorder) { ch.Encourage(1); borderBoosted++; }
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); cavSlowed++; }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Far Shore, Faded Star (ongoing): {borderBoosted} border chars encouraged; {cavSlowed} cavalry slowed.", new Color(0.6f, 0.7f, 0.9f));
            return;
        }

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.FreePeopleArmyAttackFactor = 1.05f; }

        int fpBoosted = 0, fpNaval = 0, fpCavSlowed = 0, dsSmall = 0, dsHalted = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool nearBorder = IsNearBorder(hex, board);
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                bool isNaval = ch.IsArmyCommander() && ch.GetArmy() is Army a && a.ws > 0;
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (nearBorder || isNaval) { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); ch.Encourage(1); if (isNaval) fpNaval++; else fpBoosted++; } // big bonus own
                    Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (army != null && (army.lc > 0 || army.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsSmall++; } // small bonus other
                    if (nearBorder && !ch.IsImmuneToNegativeEnvironmentalCards()) { ch.Halt(1); dsHalted++; }           // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Far Shore, Faded Star (ongoing): {fpBoosted} FP border units hoped+encouraged; {fpNaval} naval buffed; {dsHalted} DS near border halted; {dsSmall} DS emissaries guided.",
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
            if (board?.hexes == null) return false;
            var targets = board.hexes.Values.Where(h => h != null && h.characters != null && IsNearBorder(h, board))
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople).Distinct().ToList();
            if (targets.Count == 0) return false;
            foreach (var t in targets) { t.ApplyStatusEffect(StatusEffectEnum.Hope, 3); t.ApplyStatusEffect(StatusEffectEnum.Encouraged, 3); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Far Shore, Faded Star: {targets.Count} allied border units gain Hope and Encouraged for 3 turns.", new Color(0.6f, 0.7f, 0.9f));
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board?.hexes != null && board.hexes.Values.Any(h => h != null && h.characters != null && IsNearBorder(h, board) && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
