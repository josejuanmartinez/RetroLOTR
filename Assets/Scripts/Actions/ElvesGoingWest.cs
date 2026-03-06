using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElvesGoingWest : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            List<Character> elves = c.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf)
                .Distinct()
                .ToList();

            if (elves.Count == 0) return false;

            foreach (Character elf in elves)
            {
                elf.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Elves Going West: {elves.Count} elf unit(s) gain Despair (1) in radius 2.", Color.magenta);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;
            return c.hex.GetHexesInRadius(2)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
