using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButterfliesAction : EventAction
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

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Elf))
                .Distinct()
                .ToList();

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                allies[i].ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            }

            int revealed = 0;
            foreach (Character enemy in enemies)
            {
                if (enemy.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Butterflies: {allies.Count} Hobbit/Elf ally unit(s) drift into Hope and Hidden, and {revealed} hidden enemy unit(s) are exposed among the trees.",
                new Color(0.76f, 0.74f, 0.5f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
