using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DragonBreath : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            List<Hex> area = c.hex.GetHexesInRadius(1);
            List<Character> enemies = area
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters.Select(ch => new { Hex = h, Character = ch }))
                .Where(x => x.Character != null && !x.Character.killed && !IsAllied(c, x.Character))
                .Select(x => x.Character)
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int fearedOnForest = 0;
            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Burning, 1);
                if (enemy.hex != null && enemy.hex.terrainType == TerrainEnum.forest)
                {
                    enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    fearedOnForest++;
                }
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dragon Breath scorches {enemies.Count} enemy unit(s); {fearedOnForest} on forests also gain Fear.", Color.red);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            return c.hex.GetHexesInRadius(1)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(c, ch)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
