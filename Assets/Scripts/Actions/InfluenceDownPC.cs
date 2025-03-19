using System;

public class InfluenceDownPC : EmmissaryEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;
            c.hex.pc.loyalty -= UnityEngine.Random.Range(0, 10) * c.emmissary;
            c.hex.pc.loyalty = Math.Max(0, c.hex.pc.loyalty);
            c.hex.pc.CheckLowLoyalty(c.GetOwner());
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.pc != null && c.hex.pc.loyalty > 0 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
