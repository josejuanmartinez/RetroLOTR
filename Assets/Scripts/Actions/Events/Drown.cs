using System;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base), neutral (of_many_colours)
// 4-part dark:    DS shore chars +1 move (surge) + DS +5% atk | own DS on water slowed 1 | FP naval 20% lose 1 warship | all water chars 15 dmg
// neutral rule:   all water chars 10 dmg | shore chars slowed 1
public class Drown : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            // bonus: Burning cleared from all chars (rising waters extinguish fires)
            // debuff: water chars take 10 damage
            int burningCleared = 0, drowned = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.ClearStatusEffect(StatusEffectEnum.Burning); burningCleared++; }
                    if (hex.IsWaterTerrain()) { ch.Wounded(null, 10); drowned++; }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Drown (ongoing): {burningCleared} Burning cleared; {drowned} water chars take 10 damage.", Color.blue);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (caster == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 1.05f;
            else env.FreePeopleArmyAttackFactor = 1.05f;
        }

        int drownedAll = 0, ownSurge = 0, ownSlowed = 0, otherShipLost = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isWater = hex.IsWaterTerrain();
            bool isShore = hex.terrainType == TerrainEnum.shore;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (isWater) { ch.Wounded(null, 15); drownedAll++; }                            // universal: water is deadly

                if (ch.GetAlignment() == caster && isShore)
                { ch.moved = Mathf.Max(0, ch.moved - 1); ownSurge++; }                          // big bonus own: shore advance
                if (ch.GetAlignment() == caster && isWater)
                { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; }        // small debuff own: even own naval suffers

                if (ch.GetAlignment() == other && ch.IsArmyCommander())
                {
                    Army army = ch.GetArmy();
                    if (army != null && army.ws > 0 && UnityEngine.Random.value < 0.20f)
                    { army.ws = Mathf.Max(0, army.ws - 1); otherShipLost++; }                   // big debuff other: enemy ships lost
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Drown (ongoing): {drownedAll} water chars take 15 dmg; {ownSurge} own shore units surge; {otherShipLost} enemy warships lost.",
            Color.blue);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
