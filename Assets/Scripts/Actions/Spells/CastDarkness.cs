using System;
using UnityEngine;

public class CastDarkness: DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex hex = c.hex;
            hex.UnrevealArea(1, true, c.owner);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area obscured!", Color.red);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
