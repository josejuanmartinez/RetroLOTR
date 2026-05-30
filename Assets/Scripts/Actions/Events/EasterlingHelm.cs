using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EasterlingHelm : EventAction
{
    public override void ApplyOngoingEffect() { }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> easterlingCommanders = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander()
                    && (ch.race == RacesEnum.Easterling || ch.race == RacesEnum.Southron))
                .OrderByDescending(ch => ch.GetArmy()?.lc + ch.GetArmy()?.hc ?? 0)
                .Take(2).ToList();

            foreach (Character ch in easterlingCommanders)
            {
                Army army = ch.GetArmy();
                if (army != null) { army.hc++; army.lc++; }
            }
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Easterling Resolve: the 2 strongest Easterling cavalry armies gain +1 HC and +1 LC.",
                Color.yellow);
            return easterlingCommanders.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander()
                    && (ch.race == RacesEnum.Easterling || ch.race == RacesEnum.Southron)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
