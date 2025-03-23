using System;

public class Fireworks: FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            if (c.hex.GetPC().owner == c.GetOwner() || (c.hex.GetPC().owner.alignment == c.GetAlignment() && c.hex.GetPC().owner.alignment != AlignmentEnum.neutral))
            {
                int loyalty = UnityEngine.Random.Range(0, 10) * c.GetMage();
                c.hex.GetPC().IncreaseLoyalty(loyalty);
            }
            else
            {
                return false;
            }
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && (c.hex.GetPC().owner == c.GetOwner() || (c.hex.GetPC().owner.alignment == c.GetAlignment() && c.hex.GetPC().owner.alignment != AlignmentEnum.neutral)) && c.artifacts.Find(x => x.providesSpell is Fireworks) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
