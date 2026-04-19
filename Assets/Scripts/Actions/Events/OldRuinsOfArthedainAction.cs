using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldRuinsOfArthedainAction : EventAction
{
    private static readonly string[] TargetRegions = { "Arthedain", "Cardolan", "Rhudaur" };

    private static string NormalizeRegion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static List<Hex> ChooseRandomHexesInRegion(Board board, string region, int count)
    {
        if (board == null || board.hexes == null || string.IsNullOrWhiteSpace(region) || count <= 0)
            return new List<Hex>();

        string normalizedRegion = NormalizeRegion(region);
        List<Hex> candidates = board.hexes.Values
            .Where(hex =>
            {
                if (hex == null) return false;
                string hexRegion = hex.GetLandRegion();
                if (string.IsNullOrWhiteSpace(hexRegion)) return false;
                return NormalizeRegion(hexRegion) == normalizedRegion;
            })
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        if (candidates.Count == 0) return new List<Hex>();
        return candidates.Take(Mathf.Min(count, candidates.Count)).ToList();
    }

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
            Board board = FindFirstObjectByType<Board>();
            if (owner == null || board == null) return false;

            List<Hex> revealedHexes = new();
            for (int i = 0; i < TargetRegions.Length; i++)
            {
                List<Hex> chosenHexes = ChooseRandomHexesInRegion(board, TargetRegions[i], 2);
                if (chosenHexes.Count == 0) continue;

                foreach (Hex chosen in chosenHexes)
                {
                    chosen.RevealMapOnlyArea(0, false, false);
                }

                owner.AddTemporarySeenHexes(chosenHexes);
                revealedHexes.AddRange(chosenHexes);
            }

            if (revealedHexes.Count == 0) return false;

            for (int i = 0; i < revealedHexes.Count; i++)
            {
                revealedHexes[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Old Ruins of Arthedain: Arthedain, Cardolan, and Rhudaur each reveal 2 hexes for 1 turn.",
                new Color(0.64f, 0.64f, 0.45f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return TargetRegions.Any(region => ChooseRandomHexesInRegion(board, region, 1).Count > 0);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
