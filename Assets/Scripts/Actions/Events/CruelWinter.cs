using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CruelWinter : EventAction
{
    private const float FreezeChance = 0.075f;

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

            List<Character> mountainEnemies = board.GetHexes()
                .Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment())
                .Distinct()
                .ToList();

            if (mountainEnemies.Count == 0) return false;

            int frozen = 0;
            for (int i = 0; i < mountainEnemies.Count; i++)
            {
                if (UnityEngine.Random.value <= FreezeChance)
                {
                    mountainEnemies[i].ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                    frozen++;
                }
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Cruel Winter sweeps all mountains: {frozen}/{mountainEnemies.Count} enemy unit(s) frozen (7.5%).", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null
                && h.terrainType == TerrainEnum.mountains
                && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment()));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
