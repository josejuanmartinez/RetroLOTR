using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WizardsFire : FreeSpell
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            int radius = Mathf.Max(1, Mathf.FloorToInt(c.GetMage() / 2f));
            List<Hex> areaHexes = c.hex.GetHexesInRadius(radius);

            List<Character> enemies = areaHexes
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ApplyStatusEffect(StatusEffectEnum.Burning, 1);
            }

            for (int i = 0; i < areaHexes.Count; i++)
            {
                areaHexes[i]?.PlayFireParticles();
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Wizard's Fire burns {enemies.Count} enemy unit(s) in radius {radius}.", Color.red);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;

            int radius = Mathf.Max(1, Mathf.FloorToInt(c.GetMage() / 2f));
            List<Hex> areaHexes = c.hex.GetHexesInRadius(radius);
            return areaHexes.Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment()));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
