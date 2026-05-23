using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RestlessEast : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> commanders = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander()
                && (ch.race == RacesEnum.Easterling || ch.race == RacesEnum.Southron))
            .Distinct().ToList();

        int encouraged = 0, hasted = 0;
        foreach (Character ch in commanders)
        {
            Army army = ch.GetArmy();
            if (army == null) continue;
            ch.Encourage(1);
            encouraged++;
            // Cavalry armies of the East ride with great speed
            if (army.lc > 0 || army.hc > 0)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                hasted++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Restless East (ongoing): {encouraged} Easterling/Southron army commanders encouraged; {hasted} cavalry commanders hasted.",
            Color.yellow);
    }

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
                $"Restless East: the 2 strongest Easterling cavalry armies gain +1 HC and +1 LC.",
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
