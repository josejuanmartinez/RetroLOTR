using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheFordAction : EventAction
{
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null && h.GetHexesInRadius(1).Any(n => n != null && (n.terrainType == TerrainEnum.shore || n.terrainType == TerrainEnum.shallowWater || n.IsWaterTerrain())))
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++) enemies[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"The Ford: enemies near the water are Halted <sprite name=\"halted\"> for 1 turn.",
                new Color(0.5f, 0.74f, 0.8f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.GetHexesInRadius(1).Any(n => n != null && (n.terrainType == TerrainEnum.shore || n.terrainType == TerrainEnum.shallowWater || n.IsWaterTerrain())) && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
