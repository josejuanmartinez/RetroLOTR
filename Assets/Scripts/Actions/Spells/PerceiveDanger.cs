using System;
using System.Linq;
using UnityEngine;

public class PerceiveDanger : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            // Find nearest enemy army hex
            Hex target = board.hexes.Values
                .Where(h => h.armies.Any(a => a != null && a.commander != null && a.commander.GetOwner() != c.GetOwner() && a.commander.GetAlignment() != c.GetAlignment()))
                .OrderBy(h => Vector2.Distance(c.hex.v2, h.v2))
                .FirstOrDefault();

            if (target == null) return false;

            int radius = Mathf.Max(1, ApplySpellEffectMultiplier(c, Mathf.Max(1, c.GetMage() / 3)));
            target.RevealArea(radius, true, c.GetOwner());
            var radiusHexes = target.GetHexesInRadius(radius);
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex hex = radiusHexes[i];
                if (hex == null) continue;
                hex.RefreshVisibilityRendering();
            }
            target.LookAt();
            MessageDisplayNoUI.ShowMessage(target, c, $"Danger perceived nearby!", Color.cyan);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.GetOwner() == FindFirstObjectByType<Game>().player; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
