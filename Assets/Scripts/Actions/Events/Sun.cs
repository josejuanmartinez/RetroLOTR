using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Sun : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> humansAndHobbits = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed &&
                    (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain || ch.race == RacesEnum.Hobbit))
                .Distinct()
                .ToList();

            List<Character> trolls = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Troll)
                .Distinct()
                .ToList();

            if (humansAndHobbits.Count == 0 && trolls.Count == 0) return false;

            for (int i = 0; i < humansAndHobbits.Count; i++)
            {
                humansAndHobbits[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            for (int i = 0; i < trolls.Count; i++)
            {
                trolls[i].Halt();
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Sun grants Courage to {humansAndHobbits.Count} Human/Hobbit unit(s) and halts {trolls.Count} troll unit(s).", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed &&
                (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain || ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Troll)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
