using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MurazorUnleashed : EventAction
{
    private static string NormalizeRegion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static readonly HashSet<string> NorthernRegions = new()
    {
        "angmar", "arthedain", "cardolan", "rhudaur"
    };

    private static bool IsInNorthernKingdoms(Hex hex) =>
        hex != null && NorthernRegions.Contains(NormalizeRegion(hex.GetLandRegion()));

    private static bool IsInTargetHex(Hex hex) =>
        hex != null && (IsInNorthernKingdoms(hex) || hex.terrainType == TerrainEnum.mountains);

    private static bool IsDarkServant(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int fortified = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && IsInTargetHex(h) && h.characters != null))
        {
            bool contested = hex.characters.Any(ch => ch != null && !ch.killed && !IsDarkServant(ch));
            if (!contested) continue;

            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsDarkServant(ch)).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                fortified++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Murazor Unleashed (ongoing): {fortified} dark servant commander(s) fighting in the northern kingdoms or mountains gain Fortified.",
            Color.magenta);
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

            List<Character> targetCommanders = board.GetHexes()
                .Where(h => h != null && IsInTargetHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsDarkServant(ch))
                .Distinct()
                .ToList();

            int commandersBoosted = 0;
            foreach (Character ch in targetCommanders)
            {
                ch.AddCommander(1);
                ch.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                commandersBoosted++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Murazor Unleashed: {commandersBoosted} dark servant commander(s) in the northern kingdoms or mountains gain +1 Commander and Fortified.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && IsInTargetHex(h) && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsDarkServant(ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
