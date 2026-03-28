using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BlackSailsRumouredAction : EventAction
{
    private static bool IsSeaHex(Hex hex)
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
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> enemySeaUnits = board.GetHexes()
                .Where(h => h != null && IsSeaHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetOwner() != owner && ch.GetAlignment() != character.GetAlignment() && ch.hex != null)
                .OrderBy(ch => Vector2.Distance(character.hex.v2, ch.hex.v2))
                .Take(3)
                .ToList();

            if (enemySeaUnits.Count == 0) return false;

            HashSet<Hex> revealed = new HashSet<Hex>();
            foreach (Character target in enemySeaUnits)
            {
                if (target.hex == null || revealed.Contains(target.hex)) continue;
                target.hex.RevealArea(0, true, owner);
                owner.AddTemporarySeenHexes(new[] { target.hex });
                target.hex.RefreshVisibilityRendering();
                revealed.Add(target.hex);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Black Sails Rumoured: revealed {revealed.Count} nearest enemy sea hex(es).",
                Color.gray);

            return revealed.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.GetOwner() == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes()
                .Where(h => h != null && IsSeaHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.GetOwner() != character.GetOwner() && ch.GetAlignment() != character.GetAlignment() && ch.hex != null);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

