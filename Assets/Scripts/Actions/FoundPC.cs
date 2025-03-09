using System;

public class FoundPC : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PC pc = new (c.GetOwner(), "Kakota", PCSizeEnum.camp, FortSizeEnum.NONE, false, false, c.hex.terrainType);
            c.hex.pc = pc;

            c.hex.RedrawPC(true);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.hex.pc == null && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
