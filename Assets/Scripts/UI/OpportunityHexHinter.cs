using System.Collections.Generic;
using UnityEngine;

// When a character is selected, hints (via FrameColors.SetHint) which reachable hexes the
// character could move to this turn and have an opportunity (situation) card available.
public static class OpportunityHexHinter
{
    private static readonly List<Hex> hinted = new();

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

        foreach (Hex hex in pathRenderer.FindAllHexesInRemainingRange(character))
        {
            if (hex == null || hex.framesColors == null) continue;
            if (deckManager.GetSituationCards(leader, character, hex).Count > 0)
            {
                hex.framesColors.SetHint(true);
                hinted.Add(hex);
            }
        }
    }

    public static void ClearAll()
    {
        for (int i = 0; i < hinted.Count; i++)
        {
            hinted[i]?.framesColors?.SetHint(false);
        }
        hinted.Clear();
    }
}
