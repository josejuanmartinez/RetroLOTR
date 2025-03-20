using System;

public class InfluenceDownPC : EmmissaryEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            c.hex.GetPC().loyalty -= UnityEngine.Random.Range(0, 10) * c.GetEmmissary();
            c.hex.GetPC().loyalty = Math.Max(0, c.hex.GetPC().loyalty);
            c.hex.GetPC().CheckLowLoyalty(c.GetOwner());
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.hex.GetPC().loyalty > 0 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
