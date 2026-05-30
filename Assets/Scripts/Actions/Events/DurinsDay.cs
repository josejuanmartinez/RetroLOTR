using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: FP only (tharkun)
// 4-part: Dwarves Hope + mountain Dwarves encouraged | Dwarves on plains slowed 1 (not their terrain) | enemy mountain emissaries ArcaneInsight | non-Dwarf mountain slowed + enemies -8% atk
public class DurinsDay : EventAction
{
    private const int ArtifactRadius = 3;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.DarkServantsArmyAttackFactor = 0.92f;

        int hopeGranted = 0, mountainBoosted = 0, dwarvesSlowed = 0, otherSmall = 0, enemiesSlowed = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isMountain = hex.terrainType == TerrainEnum.mountains;
            bool isPlains = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.race == RacesEnum.Dwarf)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1); hopeGranted++;                // big bonus own
                    if (isMountain) { ch.Encourage(1); mountainBoosted++; }
                    if (isPlains) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dwarvesSlowed++; } // small debuff own: dwarves aren't plains fighters
                }
                else if (isMountain)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    if (!ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); enemiesSlowed++; } // big debuff other
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Durin's Day (ongoing): {hopeGranted} Dwarves gain Hope; {mountainBoosted} mountain Dwarves encouraged; {enemiesSlowed} non-Dwarf mountain units slowed; {otherSmall} enemy emissaries find insight.",
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
            if (character == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var dwarves = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf).Distinct().ToList();
            if (dwarves.Count == 0) return false;
            int artifactsRevealed = 0;
            if (character.hex != null) { foreach (var h in character.hex.GetHexesInRadius(ArtifactRadius)) { if (h?.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0) { h.RevealArtifact(); artifactsRevealed++; break; } } }
            int activated = 0;
            var nearDwarves = dwarves.Where(d => d.hex != null && (character.hex == null || character.hex.GetHexesInRadius(3).Contains(d.hex))).ToList();
            foreach (var d in nearDwarves) { d.hasActionedThisTurn = false; d.ApplyStatusEffect(StatusEffectEnum.Hope, 1); activated++; }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Durin's Day: {artifactsRevealed} artifact site(s) revealed; {activated} Dwarves act again and gain Hope.", Color.yellow);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
