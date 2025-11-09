using System;
using UnityEngine;

public class RevealPC: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            c.hex.GetPC().hiddenButRevealed = true; 
            MessageDisplay.ShowMessage($"The spell hiding {c.hex.GetPC()} has been lifted", Color.green);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.hex.GetPC() != null && c.hex.GetPC().owner != c.GetOwner() && (c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral) && c.hex.GetPC().isHidden && !c.hex.GetPC().hiddenButRevealed && c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
