using System;

public class WizardLaugh: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            if (c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral)
            {
                c.hex.GetPC().DecreaseLoyalty(UnityEngine.Random.Range(0, 10) * c.GetMage(), c.GetOwner());
                c.hex.GetPC().CheckLowLoyalty(c.GetOwner());
            }
            else
            {
                return false;
            }
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && ( c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral) && c.artifacts.Find(x => x.providesSpell is WizardLaugh) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
