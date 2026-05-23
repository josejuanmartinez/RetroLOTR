using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CruelWinter : EventAction
{
    private const float FreezeChance = 0.075f;
    private const float CavalryFreezeChance = 0.15f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> mountainChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Dwarf
                && !ch.IsImmuneToNegativeEnvironmentalCards())
            .Distinct().ToList();

        int frozen = 0, slowed = 0;
        foreach (Character ch in mountainChars)
        {
            bool isCavalry = ch.IsArmyCommander() && ch.GetArmy() is Army a && (a.lc > 0 || a.hc > 0);
            float chance = isCavalry ? CavalryFreezeChance : FreezeChance;
            if (UnityEngine.Random.value < chance) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
            // All non-dwarves slowed in winter passes
            ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
            slowed++;
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Cruel Winter (ongoing): {frozen} mountain units frozen; {slowed} slowed by snow. Dwarves unaffected.",
            Color.cyan);
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

            List<Character> mountainEnemies = board.GetHexes()
                .Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards())
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
                && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
