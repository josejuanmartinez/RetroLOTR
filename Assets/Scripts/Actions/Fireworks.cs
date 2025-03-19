using System;

public class Fireworks: FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;
            if (c.hex.pc.owner == c.GetOwner() || (c.hex.pc.owner.alignment == c.GetAlignment() && c.hex.pc.owner.alignment != AlignmentEnum.neutral))
            {
                c.hex.pc.loyalty += UnityEngine.Random.Range(0, 10) * c.mage;
                c.hex.pc.loyalty = Math.Min(100, c.hex.pc.loyalty);
                if (c.hex.pc.loyalty >= 50 && c.hex.encounterEnum == EncountersEnum.LowLoyalty) c.hex.encounterEnum = EncountersEnum.NONE;
            }
            else
            {
                return false;
            }
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.pc != null && (c.hex.pc.owner == c.GetOwner() || (c.hex.pc.owner.alignment == c.GetAlignment() && c.hex.pc.owner.alignment != AlignmentEnum.neutral)) && c.artifacts.Find(x => x.providesSpell is Fireworks) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
