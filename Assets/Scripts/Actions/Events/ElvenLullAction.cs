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

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            List<Character> allies = targets
                .Where(ch => ch.GetAlignment() == character.GetAlignment() && (ch.race == RacesEnum.Elf || ch.race == RacesEnum.Hobbit))
                .ToList();

            List<Character> nazguls = targets
                .Where(ch => ch.GetAlignment() != character.GetAlignment() && ch.race == RacesEnum.Nazgul)
                .ToList();

            List<Character> halted = targets
                .Where(ch => ch.GetAlignment() != character.GetAlignment() && (ch.race == RacesEnum.Orc || ch.race == RacesEnum.Troll))
                .ToList();

            if (allies.Count == 0 && nazguls.Count == 0 && halted.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }

            for (int i = 0; i < nazguls.Count; i++)
            {
                nazguls[i].ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
            }

            for (int i = 0; i < halted.Count; i++)
            {
                halted[i].ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Elven Lull: allied Hobbit/Elf unit(s) gain Haste (1), Nazgul unit(s) are Blocked (1), and Orc/Troll unit(s) are Halted (1).",
                Color.cyan);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed
                    && ((ch.GetAlignment() == character.GetAlignment() && (ch.race == RacesEnum.Elf || ch.race == RacesEnum.Hobbit))
                        || (ch.GetAlignment() != character.GetAlignment() && (ch.race == RacesEnum.Nazgul || ch.race == RacesEnum.Orc || ch.race == RacesEnum.Troll)))));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
