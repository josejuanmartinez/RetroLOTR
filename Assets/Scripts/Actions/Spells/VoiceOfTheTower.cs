using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VoiceOfTheTower : DarkSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            int radius = Mathf.Clamp(c.GetMage(), 1, 3);
            int turns = Mathf.Clamp(ApplySpellEffectMultiplier(c, 1 + Mathf.FloorToInt(c.GetMage() / 2f)), 1, 3);

            List<Character> enemyArmyCommanders = c.hex.GetHexesInRadius(radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.GetOwner() != c.GetOwner() &&
                             (ch.GetAlignment() == AlignmentEnum.neutral || ch.GetAlignment() != c.GetAlignment()))
                .Distinct()
                .ToList();

            if (enemyArmyCommanders.Count == 0) return false;

            for (int i = 0; i < enemyArmyCommanders.Count; i++)
            {
                enemyArmyCommanders[i].ApplyStatusEffect(StatusEffectEnum.Despair, turns);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Voice of the Tower spreads despair to {enemyArmyCommanders.Count} enemy army command(s) for {turns} turn(s).", Color.magenta);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            int radius = Mathf.Clamp(c.GetMage(), 1, 3);
            return c.hex.GetHexesInRadius(radius)
                .Any(h => h != null && h.characters != null &&
                          h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.GetOwner() != c.GetOwner() &&
                                                 (ch.GetAlignment() == AlignmentEnum.neutral || ch.GetAlignment() != c.GetAlignment())));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
