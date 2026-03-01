using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ABookOfLostMagic : EventAction
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

            List<Hex> area = c.hex.GetHexesInRadius(2);
            int revealed = 0;
            for (int i = 0; i < area.Count; i++)
            {
                Hex h = area[i];
                if (h == null || h.hiddenArtifacts == null || h.hiddenArtifacts.Count == 0) continue;
                h.RevealArtifact();
                revealed++;
            }

            c.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"A Book of Lost Magic reveals {revealed} artifact site(s). Mage +1 for 1 turn.", Color.magenta);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null && c.hex != null;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
