using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RaidFromTheMountains : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsMountainOrHill(Hex hex)
    {
        if (hex == null) return false;
        return hex.terrainType == TerrainEnum.mountains || hex.terrainType == TerrainEnum.hills;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(h => h != null
                && IsMountainOrHill(h)
                && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch)));
        };

        async Task<bool> raidAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> targets = board.GetHexes()
                .Where(h => h != null && IsMountainOrHill(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            foreach (Character commander in targets)
            {
                commander.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                commander.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Raid From The Mountains grants Haste and Courage to {targets.Count} allied army commander(s) in hills or mountains.", Color.red);
            return true;
        }

        base.Initialize(c, condition, effect, raidAsync);
    }
}
