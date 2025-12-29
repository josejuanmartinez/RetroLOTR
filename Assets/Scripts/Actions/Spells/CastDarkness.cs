using System;
using System.Collections.Generic;
using UnityEngine;

public class CastDarkness: DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Hex hex = c.hex;
            int radius = 1;
            List<Hex> radiusHexes = hex.GetHexesInRadius(radius);
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex targetHex = radiusHexes[i];
                if (targetHex == null) continue;
                targetHex.ClearScoutingAll();
                PC pc = targetHex.GetPC();
                AlignmentEnum ownerAlignment = c.GetOwner() != null ? c.GetOwner().GetAlignment() : AlignmentEnum.neutral;
                AlignmentEnum pcAlignment = pc != null && pc.owner != null ? pc.owner.GetAlignment() : AlignmentEnum.neutral;
                bool sameAlignment = pcAlignment == ownerAlignment && pcAlignment != AlignmentEnum.neutral;
                if (pc != null && (pc.owner == c.GetOwner() || sameAlignment))
                {
                    pc.SetTemporaryHidden(2);
                    targetHex.RedrawPC();
                }
            }
            hex.ObscureArea(radius, true, c.owner);
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex targetHex = radiusHexes[i];
                if (targetHex == null) continue;
                targetHex.RefreshVisibilityRendering();
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area obscured!", Color.red);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return true; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

