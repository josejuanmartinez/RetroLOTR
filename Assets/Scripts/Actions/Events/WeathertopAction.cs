using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeathertopAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> units = character.hex.characters
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (units.Count == 0) return false;

            foreach (Character unit in units)
            {
                unit.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                unit.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                unit.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                unit.moved = unit.GetMaxMovement();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Weathertop: all units in the hill gain Strengthened, Fortified, and Halted, and lose all movement.",
                new Color(0.68f, 0.7f, 0.44f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.terrainType == TerrainEnum.hills && character.hex.characters != null && character.hex.characters.Any(ch => ch != null && !ch.killed);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
