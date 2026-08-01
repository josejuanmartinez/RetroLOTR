using System.Collections.Generic;
using UnityEngine;

// When a character is selected, sequentially pulses a fluid HexPathRenderer route from the
// character's hex to each reachable hex that has a playable opportunity (situation) card -
// one route at a time (see HexPathRenderer.StartOpportunityHintCycle) rather than marking every
// candidate hex at once.
public static class OpportunityHexHinter
{
    private static HexPathRenderer activePathRenderer;

    public static void Refresh(Character character)
    {
        ClearAll();
        if (character == null || character.killed || character.hex == null) return;

        Game game = Object.FindFirstObjectByType<Game>();
        if (game == null || game.player == null || character.GetOwner() != game.player) return;
        if (character.GetOwner() is not PlayableLeader leader) return;

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : Object.FindFirstObjectByType<DeckManager>();
        HexPathRenderer pathRenderer = Object.FindFirstObjectByType<HexPathRenderer>();
        if (deckManager == null || pathRenderer == null) return;

        var targets = new List<Vector2Int>();
        foreach (Hex hex in pathRenderer.FindAllHexesInRemainingRange(character))
        {
            if (hex == null || hex.v2 == character.hex.v2) continue;
            if (deckManager.GetSituationCards(leader, character, hex).Count > 0)
            {
                targets.Add(hex.v2);
            }
        }

        if (targets.Count == 0) return;

        activePathRenderer = pathRenderer;
        pathRenderer.StartOpportunityHintCycle(character, targets);
    }

    public static void ClearAll()
    {
        activePathRenderer?.StopOpportunityHintCycle();
        activePathRenderer = null;
    }
}
