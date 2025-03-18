using System;

public class EmmissaryEnemyPCAction : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return c.hex.pc != null && c.hex.pc.owner != c.GetOwner() && (c.hex.pc.owner.GetAlignment() != c.GetAlignment() || c.hex.pc.owner.GetAlignment() == AlignmentEnum.neutral) &&  (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
