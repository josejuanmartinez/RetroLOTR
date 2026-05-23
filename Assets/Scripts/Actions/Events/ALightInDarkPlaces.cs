using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ALightInDarkPlaces : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hoped = 0, fearCleared = 0, darkDespaired = 0, darkRevealed = 0;
        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpenTerrain = hex.terrainType == TerrainEnum.plains
                || hex.terrainType == TerrainEnum.grasslands
                || hex.terrainType == TerrainEnum.hills;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    hoped++;
                    if (isOpenTerrain) { ch.ClearStatusEffect(StatusEffectEnum.Fear); fearCleared++; }
                }
                else if (isOpenTerrain && ch.GetAlignment() == AlignmentEnum.darkServants && !ch.IsImmuneToNegativeEnvironmentalCards())
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    darkDespaired++;
                    ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                    darkRevealed++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"A Light in Dark Places (ongoing): {hoped} Free People gain Hope; {fearCleared} lose Fear in open ground; {darkDespaired} dark servants in the open despaired and {darkRevealed} revealed.",
            Color.yellow);
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

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> allies = character.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople)
                .Distinct().ToList();

            foreach (Character ally in allies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                ally.ClearStatusEffect(StatusEffectEnum.Fear);
                ally.ClearStatusEffect(StatusEffectEnum.Despair);
            }
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"A Light in Dark Places: {allies.Count} allied units in radius 2 gain Hope and lose Fear/Despair.",
                Color.yellow);
            return allies.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
