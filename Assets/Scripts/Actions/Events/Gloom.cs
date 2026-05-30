using System;
using System.Linq;
using UnityEngine;

// Deck: dark only (sauron_base)
// 4-part: DS encouraged + DS armies +10% atk | DS in open slowed 1 (darkness makes them sluggish in the field) | FP emissaries ArcaneInsight (seeing through darkness) | FP despaired + feared + FP armies -15% atk
public class Gloom : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.DarkServantsArmyAttackFactor = 1.10f; env.FreePeopleArmyAttackFactor = 0.85f; }

        int dsEncouraged = 0, dsSlowed = 0, fpSmall = 0, fpDespairing = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1); dsEncouraged++;           // big bonus own
                    if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpSmall++; } // small bonus other
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1); fpDespairing++; // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Gloom (ongoing): {dsEncouraged} DS encouraged (+10% atk); {fpDespairing} FP despaired+feared (-15% atk); {fpSmall} FP emissaries see through the dark; {dsSlowed} DS slowed in open.",
            Color.magenta);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
