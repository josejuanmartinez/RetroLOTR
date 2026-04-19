using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButterfliesAction : EventAction
{
    private const int Radius = 3;

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

            List<Hex> terrainHexes = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.swamp))
                .ToList();

            if (terrainHexes.Count == 0) return false;

            owner.AddTemporarySeenHexes(terrainHexes);
            if (owner == FindFirstObjectByType<Game>()?.player)
            {
                owner.RefreshVisibleHexesImmediate();
            }

            for (int i = 0; i < terrainHexes.Count; i++)
            {
                terrainHexes[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Butterflies: forest and swamp hexes in radius {Radius} are seen for 1 turn.",
                new Color(0.76f, 0.74f, 0.5f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.swamp));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
