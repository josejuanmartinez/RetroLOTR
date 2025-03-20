using System;

public class InfluenceUpPC : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;

            c.hex.GetPC().loyalty += UnityEngine.Random.Range(0, 10) * c.GetEmmissary();
            c.hex.GetPC().loyalty = Math.Min(100, c.hex.GetPC().loyalty);
            if (c.hex.GetPC().loyalty >= 50 && c.hex.encounters.Contains(EncountersEnum.Disloyal)) c.hex.encounters.Remove(EncountersEnum.Disloyal);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.hex.GetPC().loyalty < 100 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
