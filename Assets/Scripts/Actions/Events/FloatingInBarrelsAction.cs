using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FloatingInBarrelsAction : EventAction
{
    private const int Radius = 2;
    private const int HealAmount = 10;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.shore || h.terrainType == TerrainEnum.shallowWater || h.IsWaterTerrain()) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .Take(2)
                .ToList();

            if (targets.Count == 0) return false;

            int moved = 0;
            int hidden = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                List<Hex> destinations = character.hex.GetHexesInRadius(1)
                    .Where(h => h != null && h != target.hex && (h.characters == null || h.characters.Count == 0))
                    .ToList();

                if (destinations.Count > 0)
                {
                    board.MoveCharacterOneHex(target, target.hex, destinations[UnityEngine.Random.Range(0, destinations.Count)], true, false);
                    moved++;
                    target.Heal(HealAmount);
                    target.Hide(1);
                    hidden++;
                }
            }

            if (moved == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Floating in Barrels: {moved} Hobbit unit(s) bob safely with the current, heal {HealAmount}, and vanish into the river mist.",
                new Color(0.54f, 0.66f, 0.75f));

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
