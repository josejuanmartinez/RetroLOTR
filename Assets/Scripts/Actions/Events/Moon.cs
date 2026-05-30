using System;
using System.Linq;
using UnityEngine;

// Deck: dark only (sauron_base)
// 4-part: DS encouraged + Nazgul gain Hidden | DS in open terrain slowed 1 | FP emissaries get ArcaneInsight (moon-reading) | FP gain Fear + FP armies -10% atk
public class Moon : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.FreePeopleArmyAttackFactor = 0.90f;

        int encouraged = 0, nazgulHidden = 0, dsSlowed = 0, feared = 0, fpInsight = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1); encouraged++;             // big bonus own
                    if (ch.race == RacesEnum.Nazgul) { ch.Hide(1); nazgulHidden++; }
                    if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpInsight++; } // small bonus other
                    ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1); feared++;                       // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Moon (ongoing): {encouraged} DS encouraged; {nazgulHidden} Nazgul hidden; {feared} FP feared; {fpInsight} FP emissaries read the moon.",
            new Color(0.6f, 0.6f, 1f));
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
