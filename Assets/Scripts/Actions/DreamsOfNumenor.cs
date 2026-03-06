using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DreamsOfNumenor : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsSeaAdjacentHex(Hex hex)
    {
        if (hex == null) return false;
        return hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> targets = board.GetHexes()
                .Where(h => h != null && IsSeaAdjacentHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(c, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character t in targets)
            {
                t.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                t.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dreams of Númenor blesses {targets.Count} allied unit(s) near the sea with Haste and Courage.", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && IsSeaAdjacentHex(h) && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(c, ch)));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
