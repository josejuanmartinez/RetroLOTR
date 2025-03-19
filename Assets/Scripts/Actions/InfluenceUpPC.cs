using System;

public class InfluenceUpPC : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;

            c.hex.pc.loyalty += UnityEngine.Random.Range(0, 10) * c.emmissary;
            c.hex.pc.loyalty = Math.Min(100, c.hex.pc.loyalty);
            if(c.hex.pc.loyalty >= 50 && c.hex.encounterEnum == EncountersEnum.LowLoyalty) c.hex.encounterEnum = EncountersEnum.NONE;
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.pc != null && c.hex.pc.loyalty < 100 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
