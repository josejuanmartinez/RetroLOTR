using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldForestTurnaboutAction : EventAction
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
            if (character.hex.terrainType != TerrainEnum.forest) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment())
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Old Forest Turnabout: the forest hides the allies with Hidden <sprite name=\"hidden\"> and quickens them with Haste <sprite name=\"haste\">.",
                new Color(0.35f, 0.65f, 0.35f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            if (character.hex.terrainType != TerrainEnum.forest) return false;
            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment());
        };
        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
