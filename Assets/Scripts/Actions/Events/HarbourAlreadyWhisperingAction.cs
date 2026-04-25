using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HarbourAlreadyWhisperingAction : EventAction
{
    private static bool IsWaterAdjacent(Hex hex)
    {
        if (hex == null) return false;
        return hex.GetHexesInRadius(1)
            .Any(h => h != null && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()));
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

            PC pc = character.hex.GetPC();
            if (pc == null || pc.owner == null) return false;
            if (pc.owner == character.GetOwner()) return false;
            if (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == character.GetAlignment()) return false;
            if (pc.loyalty <= 0) return false;
            if (!IsWaterAdjacent(character.hex)) return false;

            pc.DecreaseLoyalty(10, character);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Harbour Already Whispering: {pc.pcName} loses 10 loyalty beside the water.", Color.gray);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.GetOwner() == null) return false;
            PC pc = character.hex.GetPC();
            if (pc == null || pc.owner == null) return false;
            if (pc.owner == character.GetOwner()) return false;
            if (pc.owner.GetAlignment() != AlignmentEnum.neutral && pc.owner.GetAlignment() == character.GetAlignment()) return false;
            return pc.loyalty > 0 && IsWaterAdjacent(character.hex);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
