using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: dark (sauron_base, sharkey), neutral (of_many_colours, the_white_hand)
// 4-part dark:    DS in concealing terrain Hidden + DS agents Hidden | DS in open slowed 1 | FP agents ArcaneInsight | FP lose all scouting + FP armies -10% atk
// neutral rule:   all in concealing terrain Hidden 1 | all open chars lose scouting
public class Twilight : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        foreach (var hex in board.GetHexes().Where(h => h != null)) hex.Obscure();  // always obscure everything

        if (caster == AlignmentEnum.neutral)
        {
            int hidden = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool conceal = hex.terrainType == TerrainEnum.forest || hex.terrainType == TerrainEnum.swamp || hex.terrainType == TerrainEnum.mountains;
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                    if (conceal) { ch.Hide(1); hidden++; }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Twilight (ongoing): visibility reduced everywhere; {hidden} chars hidden in concealing terrain.", Color.gray);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.90f;
            else env.FreePeopleArmyAttackFactor = 0.90f;
        }

        int ownHidden = 0, ownSlowed = 0, otherSmall = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool conceal = hex.terrainType == TerrainEnum.forest || hex.terrainType == TerrainEnum.swamp || hex.terrainType == TerrainEnum.mountains;
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (conceal || ch.GetAgent() > 0) { ch.Hide(1); ownHidden++; }                 // big bonus own
                    if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetAgent() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    // big debuff other: scouting lost (via Obscure above) + army attack penalty
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Twilight (ongoing): {ownHidden} own hidden; {other} armies -10% atk; {otherSmall} enemy agents adapt to the dark; {ownSlowed} own open units slowed.",
            Color.gray);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;
            Leader owner = character.GetOwner();
            Game game = FindFirstObjectByType<Game>();
            if (owner == null || game == null || owner != game.player) return false;
            var radiusHexes = character.hex.GetHexesInRadius(2);
            int hiddenPcs = 0;
            foreach (var hex in radiusHexes)
            {
                if (hex == null) continue;
                hex.MarkDarknessByPlayer(); hex.ClearScoutingAll();
                PC pc = hex.GetPCData();
                if (pc?.owner != null)
                {
                    bool sameAlign = pc.owner.GetAlignment() == owner.GetAlignment() && owner.GetAlignment() != AlignmentEnum.neutral;
                    if (pc.owner == owner || sameAlign) { pc.SetTemporaryHidden(2); hex.RedrawPC(); hiddenPcs++; }
                }
            }
            character.hex.ObscureArea(2, true, owner);
            foreach (var hex in radiusHexes) hex?.RefreshVisibilityRendering();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Twilight obscures radius 2 and hides {hiddenPcs} allied PC(s).", Color.magenta);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            Leader owner = character.GetOwner();
            Game game = FindFirstObjectByType<Game>();
            return owner != null && game != null && owner == game.player;
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
