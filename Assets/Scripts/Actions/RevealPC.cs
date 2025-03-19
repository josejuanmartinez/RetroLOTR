using System;

public class RevealPC: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;
            c.hex.pc.isHidden = false;
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.hex.pc != null && c.hex.pc.isHidden && c.hex.pc.owner.GetAlignment() != c.GetAlignment() && c.artifacts.Find(x => x.providesSpell is RevealPC) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
