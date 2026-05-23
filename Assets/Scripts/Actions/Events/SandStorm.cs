using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SandStorm : EventAction
{
    private static bool IsDesert(Hex h) =>
        h != null && (h.terrainType == TerrainEnum.desert || h.terrainType == TerrainEnum.wastelands);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<(Character ch, Hex hex)> desertChars = board.GetHexes()
            .Where(h => IsDesert(h) && h.characters != null)
            .SelectMany(h => h.characters.Select(ch => (ch, h)))
            .Where(t => t.ch != null && !t.ch.killed && !t.ch.IsImmuneToNegativeEnvironmentalCards())
            .Distinct().ToList();

        int halted = 0, frozen = 0, siegeBlocked = 0, southronBuff = 0;
        foreach (var (ch, hex) in desertChars)
        {
            bool isSouthron = ch.race == RacesEnum.Southron || ch.race == RacesEnum.Easterling;
            if (isSouthron)
            {
                // Home terrain advantage: Southrons thrive in the sand
                ch.Encourage(1);
                southronBuff++;
                continue;
            }
            // All desert units slowed by sand
            ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
            halted++;
            if (ch.IsArmyCommander())
            {
                Army army = ch.GetArmy();
                if (army == null) continue;
                // Horses panic in sandstorm — 33% chance to Freeze
                if ((army.lc > 0 || army.hc > 0) && UnityEngine.Random.value < 0.33f) { ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1); frozen++; }
                // Siege engines clog in sand — 25% chance to Block
                if (army.ca > 0 && UnityEngine.Random.value < 0.25f) { ch.ApplyStatusEffect(StatusEffectEnum.Blocked, 1); siegeBlocked++; }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Sand Storm (ongoing): {halted} non-Southron desert units slowed; {frozen} cavalry frozen; {siegeBlocked} siege blocked; {southronBuff} Southrons encouraged.",
            Color.yellow);
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
                .Where(h => h != null && h.terrainType == TerrainEnum.desert && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Southron && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Halt();
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Sand Storm halts {targets.Count} non-Southron unit(s) on desert tiles.", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.desert && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race != RacesEnum.Southron && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
