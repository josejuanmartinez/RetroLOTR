using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Sun : EventAction
{
    private static bool IsManOrHobbit(RacesEnum r) =>
        r == RacesEnum.Common || r == RacesEnum.Dunedain || r == RacesEnum.Hobbit
        || r == RacesEnum.Southron || r == RacesEnum.Easterling;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int encouraged = 0, halted = 0, despaired = 0;
        foreach (Character ch in allChars)
        {
            if (IsManOrHobbit(ch.race))
            {
                ch.Encourage(1);
                // Cavalry commanders ride better in sunshine
                Army army = ch.IsArmyCommander() ? ch.GetArmy() : null;
                if (army != null && (army.lc > 0 || army.hc > 0))
                    ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                encouraged++;
            }
            else if (ch.race == RacesEnum.Troll)
            {
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
                halted++;
            }
            else if (ch.race == RacesEnum.Undead)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                despaired++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Sun (ongoing): {encouraged} Men/Hobbits encouraged; {halted} Trolls halted by sunlight; {despaired} Undead despaired.",
            Color.yellow);
    }

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
