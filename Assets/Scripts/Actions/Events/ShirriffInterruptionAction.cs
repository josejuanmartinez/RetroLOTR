using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShirriffsAndBoundersAction : EventAction
{
    private static string NormalizeRegion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static List<Hex> GetShireHexes(Board board)
    {
        if (board == null || board.hexes == null) return new List<Hex>();

        return board.hexes.Values
            .Where(hex =>
            {
                if (hex == null) return false;
                string region = hex.GetLandRegion();
                return !string.IsNullOrWhiteSpace(region) && NormalizeRegion(region) == "theshire";
            })
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
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            List<Hex> shireHexes = GetShireHexes(board);
            if (shireHexes.Count == 0) return false;

            for (int i = 0; i < shireHexes.Count; i++)
            {
                shireHexes[i].RevealMapOnlyArea(1, false, false);
            }

            owner.AddTemporarySeenHexes(shireHexes);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Shirriffs and Bounders: all hexes of the Shire are seen for 1 turn.",
                Color.yellow);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            return GetShireHexes(board).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
