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
            Leader owner = c.GetOwner();
            Game game = FindFirstObjectByType<Game>();
            bool applyGlobalEffects = owner != null && game != null && owner == game.player;
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex targetHex = radiusHexes[i];
                if (targetHex == null) continue;
                if (applyGlobalEffects)
                {
                    targetHex.MarkDarknessByPlayer();
                    targetHex.ClearScoutingAll();
                }
                PC pc = targetHex.GetPC();
                AlignmentEnum ownerAlignment = owner != null ? owner.GetAlignment() : AlignmentEnum.neutral;
                AlignmentEnum pcAlignment = pc != null && pc.owner != null ? pc.owner.GetAlignment() : AlignmentEnum.neutral;
                bool sameAlignment = pcAlignment == ownerAlignment && pcAlignment != AlignmentEnum.neutral;
                if (applyGlobalEffects && pc != null && (pc.owner == owner || sameAlignment))
                {
                    pc.SetTemporaryHidden(2);
                    targetHex.RedrawPC();
                }
            }
            if (applyGlobalEffects)
            {
                hex.ObscureArea(radius, true, c.owner);
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    Hex targetHex = radiusHexes[i];
                    if (targetHex == null) continue;
                    targetHex.RefreshVisibilityRendering();
                }
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area obscured!", Color.red);
            }
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

