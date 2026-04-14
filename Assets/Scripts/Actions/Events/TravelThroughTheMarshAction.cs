using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TravelThroughTheMarshAction : EventAction
{
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Hex> marsh = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.swamp || h.terrainType == TerrainEnum.shallowWater))
                .ToList();

            List<Character> enemies = marsh
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            List<Character> hobbits = marsh
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            if (enemies.Count == 0 && hobbits.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            for (int i = 0; i < hobbits.Count; i++)
            {
                hobbits[i].Hide(1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Travel through the Marsh: {enemies.Count} enemy unit(s) are bogged down by Poisoned and Halted, while {hobbits.Count} Hobbit(s) slip unseen through the mire.",
                new Color(0.45f, 0.6f, 0.45f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && (h.terrainType == TerrainEnum.swamp || h.terrainType == TerrainEnum.shallowWater));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
