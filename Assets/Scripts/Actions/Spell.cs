using System;

// It is not necessarily MageAction as you can have an artifact and not be a mage!
public class Spell : CharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return c.artifacts.Count > 0 && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
