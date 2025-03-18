using System;

public class FortifyPC : CommanderPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;
            PC pc = c.hex.pc;
            if (pc.owner != c.GetOwner()) return false;
            pc.IncreaseFort();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.hex.pc != null && c.hex.pc.fortSize < FortSizeEnum.citadel && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
