using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TreeParty : EventAction
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
            if (c.race != RacesEnum.Hobbit) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            int radius = 5;
            List<Hex> area = c.hex.GetHexesInRadius(radius);

            List<Character> humansHobbitsAndDwarves = area
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed &&
                    (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain || ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            if (humansHobbitsAndDwarves.Count == 0) return false;

            for (int i = 0; i < humansHobbitsAndDwarves.Count; i++)
            {
                humansHobbitsAndDwarves[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Tree Party (radius {radius}) grants Courage to {humansHobbitsAndDwarves.Count} Human/Hobbit/Dwarf unit(s)", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;
            if (c.race != RacesEnum.Hobbit) return false;
            int radius = 5;
            return c.hex.GetHexesInRadius(radius).Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed &&
                (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain || ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
