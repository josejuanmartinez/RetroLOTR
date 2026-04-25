using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WhiteHandAtTheFordsAction : EventAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsRiverOrShoreHex(Hex hex)
    {
        if (hex == null) return false;
        return hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain();
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

            List<Character> alliedAtCrossings = board.GetHexes()
                .Where(h => h != null && IsRiverOrShoreHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            List<Character> enemyAtCrossings = board.GetHexes()
                .Where(h => h != null && IsRiverOrShoreHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (alliedAtCrossings.Count == 0 && enemyAtCrossings.Count == 0) return false;

            foreach (Character target in alliedAtCrossings)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            }

            foreach (Character target in enemyAtCrossings)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character != null ? character.hex : null,
                character,
                $"Protect the Fords: {alliedAtCrossings.Count} allied unit(s) on shore/water crossings gain Fortified (1), and {enemyAtCrossings.Count} enemy unit(s) on shore/water crossings are Halted (1).",
                Color.yellow);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(h => h != null && IsRiverOrShoreHex(h) && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
