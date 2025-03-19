using System;

public class WizardLaugh: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.pc == null) return false;
            if (c.hex.pc.owner is not NonPlayableLeader || c.hex.pc.owner.GetAlignment() != c.GetAlignment() || c.hex.pc.owner.GetAlignment() == AlignmentEnum.neutral)
            {
                c.hex.pc.loyalty -= UnityEngine.Random.Range(0, 10) * c.mage;
                c.hex.pc.loyalty = Math.Max(0, c.hex.pc.loyalty);
                c.hex.pc.CheckLowLoyalty(c.GetOwner());
            }
            else
            {
                return false;
            }
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.pc != null && (c.hex.pc.owner is not NonPlayableLeader || c.hex.pc.owner.GetAlignment() != c.GetAlignment() || c.hex.pc.owner.GetAlignment() == AlignmentEnum.neutral) && c.artifacts.Find(x => x.providesSpell is WizardLaugh) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
