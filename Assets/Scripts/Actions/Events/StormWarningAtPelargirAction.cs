using System;
using System.Linq;
using UnityEngine;

public class StormWarningAtPelargirAction : EventAction
{
    private static bool IsSeaAdjacent(Hex hex)
    {
        if (hex == null) return false;
        return hex.GetHexesInRadius(1)
            .Any(h => h != null && h != hex && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()));
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || !character.IsArmyCommander() || character.GetArmy() == null) return false;
            if (!IsSeaAdjacent(character.hex)) return false;

            character.GetArmy().Recruit(TroopsTypeEnum.ws, 1);
            character.hex.RedrawCharacters();
            character.hex.RedrawArmies();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Storm Warning from Pelargir grants {character.characterName} 1 Warship.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null
                && character.hex != null
                && character.IsArmyCommander()
                && character.GetArmy() != null
                && IsSeaAdjacent(character.hex);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

