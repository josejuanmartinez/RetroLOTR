using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShutteredWatchfiresAction : EventAction
{
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Hex> ownedPcs = board.GetHexes()
                .Where(h => h != null && h.GetPC() != null && h.GetPC().owner == owner)
                .Distinct()
                .ToList();

            if (ownedPcs.Count == 0) return false;

            HashSet<Hex> revealedHexes = new();
            foreach (Hex pcHex in ownedPcs)
            {
                if (pcHex == null) continue;
                List<Hex> area = pcHex.GetHexesInRadius(Radius).Where(h => h != null).Distinct().ToList();
                pcHex.RevealArea(Radius, true, owner);
                owner.AddTemporarySeenHexes(area);
                owner.AddTemporaryScoutCenters(new[] { pcHex });
                foreach (Hex hex in area)
                {
                    revealedHexes.Add(hex);
                }
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Light the Watchfires: {revealedHexes.Count} hex(es) around your PCs are revealed for 1 turn.",
                new Color(0.75f, 0.72f, 0.55f));

            return revealedHexes.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes()
                .Any(h => h != null && h.GetPC() != null && h.GetPC().owner == owner);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
