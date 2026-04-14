using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldManWillowSongAction : EventAction
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

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            List<Character> hobbits = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            if (enemies.Count == 0 && hobbits.Count == 0) return false;

            int rooted = 0;
            foreach (Character enemy in enemies)
            {
                enemy.moved = enemy.GetMaxMovement();
                if (!enemy.HasStatusEffect(StatusEffectEnum.Blocked))
                {
                    enemy.ApplyStatusEffect(StatusEffectEnum.RefusingDuels, 1);
                }
                rooted++;
            }

            for (int i = 0; i < hobbits.Count; i++)
            {
                hobbits[i].Hide(1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Old Man Willow Song: the forest roots stall {rooted} enemy unit(s) and hide nearby Hobbit(s) in the trees.",
                new Color(0.34f, 0.55f, 0.34f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
