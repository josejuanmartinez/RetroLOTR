using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: neutral (saruman_the_white) but FP-themed
// 4-part (treat as FP): FP agents Hidden + ArcaneInsight + mages deep ArcaneInsight | FP cavalry slowed 1 | DS emissaries ArcaneInsight | DS agents 20% revealed + DS armies -8% atk
public class StarlightDispatch : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        // Neutral rule: all agents Hidden+ArcaneInsight (stars guide every scout) | all cavalry slowed (night march is slow)
        if (caster == AlignmentEnum.neutral)
        {
            int agentHidden = 0, cavSlowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (ch.GetAgent() > 0) { ch.Hide(1); ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); agentHidden++; }
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); cavSlowed++; }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Starlight Dispatch (ongoing): {agentHidden} agents hidden and insightful; {cavSlowed} cavalry slowed by night march.", Color.cyan);
            return;
        }

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.FreePeopleArmyAttackFactor = 1.05f; env.DarkServantsArmyAttackFactor = 0.92f; }

        int fpAgentHidden = 0, fpMageInsight = 0, fpCavSlowed = 0, dsSmall = 0, dsRevealed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (ch.GetAgent() > 0) { ch.Hide(1); ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpAgentHidden++; } // big bonus own
                    if (ch.GetMage() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 2); fpMageInsight++; }
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); fpCavSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); dsSmall++; } // small bonus other
                    if (ch.GetAgent() > 0 && !ch.IsImmuneToNegativeEnvironmentalCards() && UnityEngine.Random.value < 0.20f)
                    { ch.ClearStatusEffect(StatusEffectEnum.Hidden); dsRevealed++; }                // big debuff other: starlight exposes dark agents
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Starlight Dispatch (ongoing): {fpAgentHidden} FP agents hidden+insightful; {fpMageInsight} mages gain deep insight; {dsRevealed} DS agents revealed; {dsSmall} DS emissaries guided.",
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
            if (character == null || character.IsArmyCommander()) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var capitalHex = board.GetHexes().Find(h => h?.GetPC() != null && h.GetPC().owner == character.GetOwner() && h.GetPC().isCapital);
            if (capitalHex == null || capitalHex == character.hex) return false;
            board.MoveCharacterOneHex(character, character.hex, capitalHex, true);
            MessageDisplayNoUI.ShowMessage(capitalHex, character, $"Starlight Dispatch: {character.characterName} returns to capital.", Color.cyan);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.IsArmyCommander()) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h?.GetPC() != null && h.GetPC().owner == character.GetOwner() && h.GetPC().isCapital && h != character.hex);
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
