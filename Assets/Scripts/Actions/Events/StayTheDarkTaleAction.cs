using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Encounters are placed directly on the board now (Hex._pendingEncounters), not drawn into a
// hand — "closing the book on a looming tale" means dismissing one you've already found rather
// than swapping it out of a hand you no longer have. Only ever removes an encounter at a hex
// this leader has actually discovered (visibleHexes + IsHexSeen) — you can't set aside a shadow
// you haven't found yet.
public class StayTheDarkTaleAction : EventAction
{

    private static List<Hex> FindDiscoveredPendingEncounterHexes(Character character)
    {
        Board board = Board.Instance;
        Leader leader = character?.GetOwner();
        if (board == null || leader == null || character.hex == null) return new List<Hex>();

        Hex from = character.hex;
        return board.GetHexes()
            .Where(h => h != null && h.HasPendingEncounters && leader.visibleHexes.Contains(h) && h.IsHexSeen())
            .OrderBy(h => Vector2.Distance(from.v2, h.v2))
            .ToList();
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

            List<Hex> hexes = FindDiscoveredPendingEncounterHexes(character);
            if (hexes.Count == 0) return false;

            Hex target = hexes[0];
            CardData dismissed = target.TakeFirstPendingEncounter();
            if (dismissed == null) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Stay the Dark Tale: the shadow of \"{dismissed.name}\" passes, unmet.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && FindDiscoveredPendingEncounterHexes(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
