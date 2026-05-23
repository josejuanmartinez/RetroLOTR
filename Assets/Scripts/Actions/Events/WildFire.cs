using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WildFire : EventAction
{
    private const int Radius = 2;
    private const float BurningChance = 0.15f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> forestChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && !ch.IsImmuneToNegativeEnvironmentalCards())
            .Distinct().ToList();

        int ignited = 0, halted = 0;
        foreach (Character ch in forestChars)
        {
            if (ch.HasStatusEffect(StatusEffectEnum.Burning))
            {
                // Already burning units can't move
                ch.Halt(1);
                halted++;
            }
            else if (UnityEngine.Random.value < BurningChance)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Burning, 1);
                ignited++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Wild Fire (ongoing): {ignited} forest units ignited; {halted} already-burning units halted.",
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

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].ApplyStatusEffect(StatusEffectEnum.Burning, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"WildFire burns {targets.Count} enemy unit(s) on forest tiles in radius {Radius}.", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.terrainType == TerrainEnum.forest
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
