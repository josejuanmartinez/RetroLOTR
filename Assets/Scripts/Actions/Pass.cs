using System;

public class Pass : CharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return originalCondition == null || originalCondition(c); 
        };
        base.Initialize(c, condition, effect);
    }
}
