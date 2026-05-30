using System;
using System.Linq;
using UnityEngine;

// Deck alignments: dark (sauron_base, sharkey), neutral (of_many_colours, the_white_hand)
// 4-part when dark:    DS armies +8% atk | enemy mages get ArcaneInsight | own open-terrain units slowed 1 | FP armies -15% atk
// neutral rule:        all armies -8% atk | hidden chars extend Hidden 1
public class Clouds : EventAction
{
    public override void ApplyOngoingEffect()
    {
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            if (env != null) env.GlobalArmyAttackFactor = 0.92f;
            Board nb = FindFirstObjectByType<Board>();
            int extended = 0;
            if (nb != null)
                foreach (var hex in nb.GetHexes().Where(h => h != null && h.characters != null))
                    foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                    { ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1); extended++; }
            MessageDisplayNoUI.ShowMessage(null, null, $"Clouds (ongoing): all armies -8% attack; {extended} hidden character(s) stay concealed.", Color.grey);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (caster == AlignmentEnum.freePeople) { env.FreePeopleArmyAttackFactor = 1.08f; env.DarkServantsArmyAttackFactor = 0.85f; }
            else { env.DarkServantsArmyAttackFactor = 1.08f; env.FreePeopleArmyAttackFactor = 0.85f; }
        }

        Board b = FindFirstObjectByType<Board>();
        int mageInsight = 0, ownSlowed = 0;
        if (b != null)
        {
            foreach (var hex in b.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (ch.GetAlignment() == other && ch.GetMage() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); mageInsight++; }
                    if (ch.GetAlignment() == caster && isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Clouds (ongoing): {caster} armies +8% atk; {other} armies -15% atk; {mageInsight} enemy mages inspired; {ownSlowed} own units slowed in the open.",
            Color.grey);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
