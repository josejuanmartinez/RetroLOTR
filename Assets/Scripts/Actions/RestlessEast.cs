using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RestlessEast : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.GetOwner() == character.GetOwner()));
        };

        async Task<bool> restlessAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> alliedCommanders = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.hex != null && ch.GetOwner() == character.GetOwner())
                .Distinct()
                .OrderByDescending(ch => ch.hex.v2.x)
                .ThenByDescending(ch => ch.hex.v2.y)
                .Take(2)
                .ToList();

            if (alliedCommanders.Count == 0) return false;

            foreach (Character commander in alliedCommanders)
            {
                Army army = commander.GetArmy();
                if (army == null) continue;
                army.hc += 1;
                army.lc += 1;
                commander.hex?.RedrawArmies();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Restless East grants +1 HC and +1 LC to {alliedCommanders.Count} easternmost allied army commander(s).", Color.red);
            return true;
        }

        base.Initialize(c, condition, effect, restlessAsync);
    }
}
