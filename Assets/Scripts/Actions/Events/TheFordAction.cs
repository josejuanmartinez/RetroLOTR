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

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                if (!allies[i].IsArmyCommander())
                {
                    allies[i].Hide(1);
                }
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"The Ford: allied Hobbit/Dwarf travelers slip through the crossing, while enemies on the water are Halted.",
                new Color(0.5f, 0.74f, 0.8f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
