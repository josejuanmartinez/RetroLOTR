using System;

public class AgentAction : CharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return c.GetAgent() > 0 && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
