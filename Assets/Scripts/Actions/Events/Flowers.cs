using System;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), dark (sharkey)
// 4-part FP:    FP heal 10 HP + FP forest chars ArcaneInsight | FP cavalry slowed 1 (distracted by beauty) | DS forest/swamp emissaries ArcaneInsight | DS on wasteland/desert take 5 damage (flowers wither them)
// 4-part dark:  DS heal 10 HP + DS plains chars Encouraged | DS cavalry slowed 1 | FP emissaries gain Hope (can't suppress nature) | FP on plains halted (bloom overwhelms them)
public class Flowers : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants
            : caster == AlignmentEnum.darkServants ? AlignmentEnum.freePeople : AlignmentEnum.neutral;

        int ownHealed = 0, ownBig = 0, ownDebuff = 0, otherSmall = 0, otherBig = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isForest = hex.terrainType == TerrainEnum.forest || hex.terrainType == TerrainEnum.swamp;
            bool isPlains = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands;
            bool isWaste = hex.terrainType == TerrainEnum.wastelands || hex.terrainType == TerrainEnum.desert;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    ch.Heal(10); ownHealed++;                                                      // big bonus own: heal
                    if (caster == AlignmentEnum.freePeople && isForest) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); ownBig++; }
                    if (caster == AlignmentEnum.darkServants && isPlains) { ch.Encourage(1); ownBig++; }
                    Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                    if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownDebuff++; } // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetEmmissary() > 0)
                    {
                        if (caster == AlignmentEnum.freePeople) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; }
                        else { ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); otherSmall++; }    // small bonus other
                    }
                    if (caster == AlignmentEnum.freePeople && isWaste && !ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.Wounded(null, 5); otherBig++; }                                          // big debuff other (FP deck)
                    if (caster == AlignmentEnum.darkServants && isPlains && !ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.Halt(1); otherBig++; }                                                   // big debuff other (dark deck)
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Flowers (ongoing): {ownHealed} own chars healed 10 HP; {ownBig} own terrain buffed; {otherBig} enemies debuffed; {otherSmall} enemy emissaries touched by nature.",
            Color.green);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
