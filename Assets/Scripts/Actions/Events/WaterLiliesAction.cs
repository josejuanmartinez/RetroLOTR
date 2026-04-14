using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaterLiliesAction : EventAction
{
    private const int Radius = 2;
    private const int HealAmount = 10;

    private static bool IsWaterOrShore(Hex hex)
    {
        return hex != null && (hex.terrainType == TerrainEnum.shore || hex.terrainType == TerrainEnum.shallowWater || hex.IsWaterTerrain());
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
            if (owner == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(IsWaterOrShore)
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(IsWaterOrShore)
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf || ch.race == RacesEnum.Elf))
                .Distinct()
                .ToList();

            if (enemies.Count == 0 && allies.Count == 0) return false;

            int revealed = 0;
            foreach (Character enemy in enemies)
            {
                if (enemy.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
            }

            int healed = 0;
            foreach (Character ally in allies)
            {
                int before = ally.health;
                ally.Heal(HealAmount);
                ally.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                if (ally.health > before) healed++;
            }

            if (revealed > 0) owner.AddGold(1);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Water Lilies: {revealed} hidden enemy unit(s) are revealed on the water, {healed} allied traveler(s) heal {HealAmount}, and the lilies quicken their pace.",
                new Color(0.65f, 0.8f, 0.78f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(IsWaterOrShore);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
