using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TravelInSafetyAction : EventAction
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

            Board board = FindFirstObjectByType<Board>();
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            Hex capitalHex = board.GetHexes()
                .FirstOrDefault(x => x != null && x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);

            if (capitalHex == null) return false;

            List<Character> targets = character.hex.characters
                .Where(ch => ch != null && !ch.killed)
                .ToList();

            if (targets.Count == 0) return false;

            int movedCount = 0;
            foreach (Character target in targets)
            {
                if (target.hex == null) continue;
                board.MoveCharacterOneHex(target, target.hex, capitalHex, true, false);
                movedCount++;
            }

            if (movedCount == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Travel in Safety: {movedCount} character(s) are carried to the capital.",
                new Color(0.74f, 0.68f, 0.44f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            Hex capitalHex = board.GetHexes()
                .FirstOrDefault(x => x != null && x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);

            return capitalHex != null && character.hex.characters != null && character.hex.characters.Any(ch => ch != null && !ch.killed);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
