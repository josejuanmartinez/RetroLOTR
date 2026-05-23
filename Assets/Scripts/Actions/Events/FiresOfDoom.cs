using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FiresOfDoom : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<(Character ch, AlignmentEnum al)> wastelandChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.wastelands && h.characters != null)
            .SelectMany(h => h.characters.Select(ch => (ch, al: ch?.GetAlignment() ?? AlignmentEnum.neutral)))
            .Where(t => t.ch != null && !t.ch.killed)
            .Distinct().ToList();

        int ignited = 0, despaired = 0, allied = 0;
        foreach (var (ch, al) in wastelandChars)
        {
            if (al == AlignmentEnum.darkServants)
            {
                // Dark servants thrive in Mordor's wastes
                ch.Encourage(1);
                allied++;
            }
            else if (!ch.IsImmuneToNegativeEnvironmentalCards())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Burning, 1);
                if (ch.HasStatusEffect(StatusEffectEnum.Burning)) { ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1); despaired++; }
                ignited++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Fires of Doom (ongoing): {ignited} enemies burning on wastelands; {despaired} despaired; {allied} dark servants encouraged.",
            Color.red);
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
                .Where(h => h != null && h.terrainType == TerrainEnum.wastelands && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment() && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].ApplyStatusEffect(StatusEffectEnum.Burning, 1);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Fires of Doom ignites {targets.Count} enemy unit(s) on wastelands.", Color.red);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null
                && h.terrainType == TerrainEnum.wastelands
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
