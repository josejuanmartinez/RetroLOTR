using System;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), dark (sauron_base)
// 4-part FP:    own gain Hidden + own agents ArcaneInsight | own cavalry slowed 1 | enemy agents get ArcaneInsight (darkness teaches all) | enemy non-agent chars in open revealed + slowed 1
// 4-part dark:  own gain Hidden + own agents ArcaneInsight | own cavalry slowed 1 | FP agents get ArcaneInsight | FP non-agent chars in open revealed + slowed 1
public class NewMoon : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants
            : caster == AlignmentEnum.darkServants ? AlignmentEnum.freePeople : AlignmentEnum.neutral;

        int ownHidden = 0, ownAgentInsight = 0, ownCavSlowed = 0, otherAgentInsight = 0, otherRevealed = 0, otherSlowed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster || caster == AlignmentEnum.neutral)
                {
                    ch.Hide(1); ownHidden++;                                                       // big bonus own: everyone hidden
                    if (ch.GetAgent() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); ownAgentInsight++; }
                    Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (army != null && (army.lc > 0 || army.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownCavSlowed++; } // small debuff own
                }
                if (caster != AlignmentEnum.neutral && ch.GetAlignment() == other)
                {
                    if (ch.GetAgent() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherAgentInsight++; } // small bonus other
                    else if (isOpen) { ch.ClearStatusEffect(StatusEffectEnum.Hidden); ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); otherRevealed++; otherSlowed++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"New Moon (ongoing): {ownHidden} own hidden; {ownAgentInsight} own agents insightful; {otherRevealed} enemies revealed in open; {otherAgentInsight} enemy agents sense the dark.",
            Color.black);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
