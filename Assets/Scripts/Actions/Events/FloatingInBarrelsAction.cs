using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FloatingInBarrelsAction : EventAction
{
    private const int Radius = 5;

    private static float HexDistance(Vector2Int a, Vector2Int b)
    {
        Vector3 ac = OffsetToCube(a);
        Vector3 bc = OffsetToCube(b);
        return Mathf.Max(
            Mathf.Abs(ac.x - bc.x),
            Mathf.Abs(ac.y - bc.y),
            Mathf.Abs(ac.z - bc.z));
    }

    private static Vector3 OffsetToCube(Vector2Int hex)
    {
        int x = hex.x;
        int z = hex.y - (hex.x - (hex.x & 1)) / 2;
        int y = -x - z;
        return new Vector3(x, y, z);
    }

    private static Hex FindNearestLandHex(Board board, Hex fromHex)
    {
        if (board == null || fromHex == null || board.hexes == null) return null;

        Hex best = null;
        float bestDistance = float.MaxValue;
        foreach (Hex candidate in board.hexes.Values)
        {
            if (candidate == null || candidate.IsWaterTerrain()) continue;
            if (candidate.characters != null && candidate.characters.Count > 0) continue;

            float distance = HexDistance(fromHex.v2, candidate.v2);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
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
                .Where(h => h != null && h.IsWaterTerrain() && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int moved = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                Hex destination = FindNearestLandHex(board, target.hex);

                if (destination != null)
                {
                    board.MoveCharacterOneHex(target, target.hex, destination, true, false);
                    moved++;
                    target.Hide(1);
                }
            }

            if (moved == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Floating in Barrels: {moved} Hobbit/Dwarf unit(s) drift ashore from the sea and slip into Hidden (1).",
                new Color(0.54f, 0.66f, 0.75f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.IsWaterTerrain() && h.characters != null &&
                    h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                        (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
