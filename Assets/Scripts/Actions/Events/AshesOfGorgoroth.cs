using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AshesOfGorgoroth : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int darkFeared = 0, despaired = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isWasteland = hex.terrainType == TerrainEnum.wastelands;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.ClearStatusEffect(StatusEffectEnum.Fear);
                    darkFeared++;
                }
                else if (isWasteland && !ch.IsImmuneToNegativeEnvironmentalCards())
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    despaired++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ashes of Gorgoroth (ongoing): {darkFeared} dark servants lose Fear; {despaired} enemies despaired on wasteland tiles.",
            Color.red);
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

            List<Character> enemies = character.hex.GetHexesInRadius(3)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()
                    && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct().ToList();

            List<Character> darkAllies = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.darkServants)
                .Distinct().ToList();

            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            foreach (Character ally in darkAllies) ally.ClearStatusEffect(StatusEffectEnum.Fear);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Ashes of Gorgoroth: {enemies.Count} enemies despaired in radius 3; {darkAllies.Count} dark allies lose Fear.",
                Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetAlignment() == AlignmentEnum.darkServants;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
