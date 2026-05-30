using System;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_base)
// 4-part FP:    FP gain Hope + FP armies +8% atk | FP cavalry slowed 1 (morning mist) | DS emissaries gain ArcaneInsight | DS gain Despair + DS in open revealed + DS armies -10% atk
// neutral rule: FP Hope 1 + fear/despair cleared | DS Despair 1 + Trolls/Undead slowed
public class Dawn : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            // 1 bonus for all: fear and despair cleared (dawn breaks darkness for everyone)
            // 1 debuff for all: all chars slowed 1 (blinding morning light forces caution)
            int cleared = 0, slowed = 0;
            foreach (var ch in board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed).ToList())
            {
                ch.ClearStatusEffect(StatusEffectEnum.Fear); ch.ClearStatusEffect(StatusEffectEnum.Despair); cleared++;
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++;
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Dawn (ongoing): fear and despair cleared for {cleared} characters; {slowed} characters slowed by dawn light.", Color.yellow);
            return;
        }

        if (env != null) { env.FreePeopleArmyAttackFactor = 1.08f; env.DarkServantsArmyAttackFactor = 0.90f; }

        int fpHoped = 0, fpCavSlowed = 0, dsSmall = 0, dsDespairing = 0, dsRevealed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); fpHoped++;                     // big bonus own
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsSmall++; } // small bonus other
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); dsDespairing++;              // big debuff other
                    if (isOpen && !ch.IsImmuneToNegativeEnvironmentalCards()) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); dsRevealed++; }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Dawn (ongoing): {fpHoped} FP gain Hope (+8% atk); {dsDespairing} DS despaired (-10% atk); {dsRevealed} DS revealed in open; {dsSmall} DS emissaries read the dawn.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (ch) =>
        {
            if (originalEffect != null && !originalEffect(ch)) return false;
            if (ch == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var freePeople = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(x => x != null && !x.killed && x.GetAlignment() == AlignmentEnum.freePeople).Distinct().ToList();
            var darkServants = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(x => x != null && !x.killed && x.GetAlignment() == AlignmentEnum.darkServants).Distinct().ToList();
            if (freePeople.Count == 0) return false;
            foreach (var x in darkServants) x.ClearEncouraged();
            foreach (var x in freePeople) { x.ClearEncouraged(); x.Encourage(1); }
            MessageDisplayNoUI.ShowMessage(ch.hex, ch, $"Dawn grants Courage to {freePeople.Count} Free People; dispels Doors of Night!", Color.green);
            return true;
        };

        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            if (ch == null || ch.GetAlignment() == AlignmentEnum.darkServants) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(x => x != null && !x.killed && x.GetAlignment() == AlignmentEnum.freePeople));
        };

        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
