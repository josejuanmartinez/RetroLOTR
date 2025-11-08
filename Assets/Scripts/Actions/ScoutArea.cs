using System;

public class ScoutArea : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            c.hex.RevealArea(1, true, c.GetOwner());
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c);
        };
        base.Initialize(c, condition, effect);
    }
}
