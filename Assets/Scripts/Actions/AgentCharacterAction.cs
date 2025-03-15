using System;

public class AgentCharacterAction : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return c.hex.characters.Find(x => x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment()) != null &&
            (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
