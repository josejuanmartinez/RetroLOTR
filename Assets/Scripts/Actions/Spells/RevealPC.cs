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
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            pc.Reveal();
            c.hex.RefreshVisibilityRendering();
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{pc.pcName} revealed!", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null || pc.owner == null) return false;
            return pc.owner != c.GetOwner()
                && (pc.owner.GetAlignment() != c.GetAlignment() || pc.owner.GetAlignment() == AlignmentEnum.neutral)
                && pc.isHidden
                && !pc.hiddenButRevealed;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
