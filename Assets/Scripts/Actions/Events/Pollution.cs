using System;
using System.Linq;
using UnityEngine;

// Deck: dark only (sauron_base)
// 4-part: DS armies +8% atk (corruption fuels power) | DS in river/swamp slowed 1 | FP emissaries ArcaneInsight (detecting the taint) | FP 10% poisoned + FP on swamp get Despair + FP armies -12% atk
public class Pollution : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) { env.DarkServantsArmyAttackFactor = 1.08f; env.FreePeopleArmyAttackFactor = 0.88f; }

        int dsSlowed = 0, fpSmall = 0, fpPoisoned = 0, fpDespaired = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isWetland = hex.terrainType == TerrainEnum.swamp || hex.IsWaterTerrain();
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (isWetland) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; }             // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpSmall++; } // small bonus other
                    if (!ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        if (UnityEngine.Random.value < 0.10f) { ch.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1); fpPoisoned++; } // big debuff other
                        if (isWetland) { ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); fpDespaired++; }
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Pollution (ongoing): DS +8% atk; FP -12% atk; {fpPoisoned} FP poisoned; {fpDespaired} FP in wetlands despaired; {fpSmall} FP emissaries detect the taint.",
            Color.green);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
