using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FordOfBruinen : EventAction
{
    private const int Radius = 2;

    private static bool IsShoreOrWater(Hex hex)
    {
        return hex != null && (hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain());
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

            AlignmentEnum ownerAlignment = character.GetAlignment();
            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(IsShoreOrWater)
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int alliedInspired = 0;
            int alliedHidden = 0;
            int enemiesHalted = 0;
            int burningCleared = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target.HasStatusEffect(StatusEffectEnum.Burning))
                {
                    target.ClearStatusEffect(StatusEffectEnum.Burning);
                    burningCleared++;
                }
                if (target.GetAlignment() == ownerAlignment)
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                    alliedInspired++;

                    if (!target.IsArmyCommander())
                    {
                        target.Hide(1);
                        alliedHidden++;
                    }
                }
                else
                {
                    target.Halt(1);
                    enemiesHalted++;
                }
            }

            if (alliedInspired == 0 && enemiesHalted == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Ford of Bruinen inspires {alliedInspired} allied unit(s), hides {alliedHidden} allied non-army character(s), halts {enemiesHalted} enemy unit(s), and removes Burning from {burningCleared} unit(s) on shore/water tiles in radius {Radius}.",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Where(IsShoreOrWater)
                .Any(h => h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
