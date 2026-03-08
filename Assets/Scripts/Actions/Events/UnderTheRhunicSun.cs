using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnderTheRhunicSun : EventAction
{
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

            List<Character> easterlings = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Easterling)
                .Distinct()
                .ToList();

            if (easterlings.Count == 0) return false;

            for (int i = 0; i < easterlings.Count; i++)
            {
                easterlings[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Under the Rhunic Sun grants Courage to {easterlings.Count} Easterling unit(s).", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Easterling));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
