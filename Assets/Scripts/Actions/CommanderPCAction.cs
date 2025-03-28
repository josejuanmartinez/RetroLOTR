using System;
using UnityEngine;

public class CommanderPCAction : CommanderAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
