using System;
using System.Linq;
using UnityEngine;

public class RevealPC: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c.hex.GetPC() == null) return false;
            c.hex.GetPC().Reveal();
            c.hex.RefreshVisibilityRendering();
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{c.hex.GetPC()} revealed!", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.GetPC() != null && c.hex.GetPC().owner != c.GetOwner() && (c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral) && c.hex.GetPC().isHidden && !c.hex.GetPC().hiddenButRevealed; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
