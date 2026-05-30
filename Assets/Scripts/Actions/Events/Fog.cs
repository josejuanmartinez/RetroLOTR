using System;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base, sharkey), neutral (of_many_colours, the_white_hand)
// 4-part dark:    own in concealing terrain → Hidden | own open slowed 1 | enemy open slowed 1 | enemy 35% halt
// neutral rule:   all 20% halt | hidden chars extend 1
public class Fog : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            int halted = 0, extended = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (ch.HasStatusEffect(StatusEffectEnum.Hidden)) { ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1); extended++; }
                    if (UnityEngine.Random.value < 0.20f) { ch.ApplyStatusEffect(StatusEffectEnum.Halted, 1); halted++; }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Fog (ongoing): {halted} halted; {extended} hidden extended.", Color.grey);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownHidden = 0, ownSlowed = 0, otherSlowed = 0, otherHalted = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool conceal = hex.terrainType == TerrainEnum.forest || hex.terrainType == TerrainEnum.swamp || hex.terrainType == TerrainEnum.mountains;
            bool open = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands || hex.terrainType == TerrainEnum.wastelands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (conceal) { ch.Hide(1); ownHidden++; }                                                          // big bonus own
                    if (open) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; }               // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (open) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); otherSlowed++; }             // small bonus other (minor)
                    if (UnityEngine.Random.value < 0.35f) { ch.ApplyStatusEffect(StatusEffectEnum.Halted, 1); otherHalted++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Fog (ongoing): {ownHidden} own hidden in shadow; {otherHalted} enemies halted; {otherSlowed}/{ownSlowed} open units slowed.",
            Color.grey);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
