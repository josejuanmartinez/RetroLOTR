using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaterLiliesAction : EventAction
{
    private const int Radius = 5;
    private static readonly StatusEffectEnum[] NegativeStatusEffects =
    {
        StatusEffectEnum.Halted,
        StatusEffectEnum.RefusingDuels,
        StatusEffectEnum.Poisoned,
        StatusEffectEnum.Burning,
        StatusEffectEnum.Frozen,
        StatusEffectEnum.Blocked,
        StatusEffectEnum.Despair,
        StatusEffectEnum.Fear,
        StatusEffectEnum.MorgulTouch
    };

    private static bool IsWaterOrShore(Hex hex)
    {
        return hex != null && (hex.terrainType == TerrainEnum.shore || hex.terrainType == TerrainEnum.shallowWater || hex.IsWaterTerrain());
    }

    private static bool IsCloseToWater(Hex hex)
    {
        return hex != null && hex.GetHexesInRadius(1).Any(IsWaterOrShore);
    }

    private static int CleanseNegativeStatusEffects(Character target)
    {
        if (target == null || target.killed) return 0;

        int removed = 0;
        foreach (StatusEffectEnum effect in NegativeStatusEffects)
        {
            if (!target.HasStatusEffect(effect)) continue;
            target.ClearStatusEffect(effect);
            removed++;
        }

        return removed;
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
                .Where(IsCloseToWater)
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int cleansedUnits = 0;
            foreach (Character target in targets)
            {
                int removed = CleanseNegativeStatusEffects(target);
                if (removed <= 0) continue;
                cleansedUnits++;
            }

            if (cleansedUnits == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"River Lillies: {cleansedUnits} unit(s) close to the water are cleansed of their darker burdens.",
                new Color(0.65f, 0.8f, 0.78f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(IsCloseToWater);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
