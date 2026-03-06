using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DreadOfTheNoldor : EventAction
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

            List<Character> elvesInRadius = character.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf)
                .Distinct()
                .ToList();

            List<Character> enemyElvesInHex = character.hex.characters
                .Where(ch => ch != null
                    && !ch.killed
                    && ch.race == RacesEnum.Elf
                    && ch.GetOwner() != character.GetOwner()
                    && (character.GetAlignment() == AlignmentEnum.neutral || ch.GetAlignment() != character.GetAlignment()))
                .Distinct()
                .ToList();

            if (elvesInRadius.Count == 0 && enemyElvesInHex.Count == 0) return false;

            for (int i = 0; i < elvesInRadius.Count; i++)
            {
                elvesInRadius[i].ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            }

            for (int i = 0; i < enemyElvesInHex.Count; i++)
            {
                enemyElvesInHex[i].ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Dread of the Noldor: {elvesInRadius.Count} elf unit(s) gain Despair (1); {enemyElvesInHex.Count} enemy elf unit(s) in the hex gain Fear (1).",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(2)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
