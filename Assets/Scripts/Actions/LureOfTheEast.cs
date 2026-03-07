using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LureOfTheEast : CharacterAction
{
    private const int Radius = 2;

    private static bool IsEligibleTarget(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        if (target.race != RacesEnum.Maia) return false;

        AlignmentEnum alignment = target.GetAlignment();
        if (alignment != AlignmentEnum.freePeople && alignment != AlignmentEnum.neutral) return false;

        if (target.GetOwner() == source.GetOwner()) return false;
        return source.GetAlignment() == AlignmentEnum.neutral || alignment != source.GetAlignment();
    }

    private static List<Hex> GetEastmostLandHexes(Board board)
    {
        if (board == null) return new List<Hex>();

        List<Hex> landHexes = board.GetHexes()
            .Where(h => h != null && !h.IsWaterTerrain())
            .ToList();
        if (landHexes.Count == 0) return landHexes;

        int maxX = landHexes.Max(h => h.v2.x);
        return landHexes.Where(h => h.v2.x == maxX).ToList();
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
            if (board == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && IsEligibleTarget(character, ch))
                .Distinct()
                .ToList();
            if (targets.Count == 0) return false;

            List<Hex> eastmostLandHexes = GetEastmostLandHexes(board);
            if (eastmostLandHexes.Count == 0) return false;

            int movedCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                Hex destination = eastmostLandHexes[UnityEngine.Random.Range(0, eastmostLandHexes.Count)];
                if (destination == null || target.hex == null) continue;

                board.MoveCharacterOneHex(target, target.hex, destination, true, false);
                target.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
                movedCount++;
            }

            if (movedCount == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Lure of the East draws {movedCount} enemy Maia to the eastern edge and Blocks them for 1 turn.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && IsEligibleTarget(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
