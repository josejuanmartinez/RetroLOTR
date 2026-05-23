using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Caradhras : EventAction
{
    private const float MountainFreezeChance = 0.20f;
    private const float CavalryFreezeChance = 0.40f;

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

        int frozen = 0, halted = 0;
        foreach (Character ch in mountainChars)
        {
            bool isCavalry = ch.IsArmyCommander() && ch.GetArmy() is Army a && (a.lc > 0 || a.hc > 0);
            float chance = isCavalry ? CavalryFreezeChance : MountainFreezeChance;
            if (UnityEngine.Random.value < chance)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                frozen++;
            }
            // Non-dwarf commanders always halted in Caradhras
            if (ch.IsArmyCommander())
            {
                ch.Halt(1);
                halted++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Caradhras (ongoing): {frozen} mountain units frozen; {halted} army commanders halted. Dwarves are unaffected.",
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
            if (c == null || c.hex == null) return false;

            int radius = 5;
            List<Hex> mountainArea = c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.mountains)
                .ToList();

            List<Character> enemies = mountainArea
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Caradhras freezes {enemies.Count} enemy unit(s) on mountain tiles in radius {radius}.", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            int radius = 5;
            return c.hex.GetHexesInRadius(radius)
                .Any(h => h != null
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
