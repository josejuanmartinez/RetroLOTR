using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FarShoreFadedStar : EventAction
{
    private const int BorderDistance = 5;
    private const int Duration = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsNearBorder(Hex hex, Board board)
    {
        if (hex == null || board == null || board.hexes == null) return false;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var h in board.hexes.Values)
        {
            if (h == null) continue;
            minX = Mathf.Min(minX, h.v2.x);
            maxX = Mathf.Max(maxX, h.v2.x);
            minY = Mathf.Min(minY, h.v2.y);
            maxY = Mathf.Max(maxY, h.v2.y);
        }

        return hex.v2.x <= minX + BorderDistance
            || hex.v2.x >= maxX - BorderDistance
            || hex.v2.y <= minY + BorderDistance
            || hex.v2.y >= maxY - BorderDistance;
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

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            List<Character> targets = board.hexes.Values
                .Where(h => h != null && h.characters != null && IsNearBorder(h, board))
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Hope, Duration);
                target.ApplyStatusEffect(StatusEffectEnum.Encouraged, Duration);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Far Shore, Faded Star: {targets.Count} allied unit(s) near the border gain Hope and Courage for {Duration} turns.", new Color(0.6f, 0.7f, 0.9f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            return board.hexes.Values
                .Where(h => h != null && h.characters != null && IsNearBorder(h, board))
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
