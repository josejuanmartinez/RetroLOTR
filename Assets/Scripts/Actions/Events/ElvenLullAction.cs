using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElvenLullAction : EventAction
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
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() && ch.race == RacesEnum.Elf)
                .Distinct()
                .ToList();

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && ch.race != RacesEnum.Elf)
                .Distinct()
                .ToList();

            if (allies.Count == 0 && enemies.Count == 0) return false;

            int elvenHaste = 0;
            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                elvenHaste++;
            }

            int sleeped = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
                sleeped++;
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Elven Lull: {elvenHaste} allied Elf unit(s) gain Haste (1), and {sleeped} non-elf enemy unit(s) fall asleep as Blocked (1).",
                Color.cyan);

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
