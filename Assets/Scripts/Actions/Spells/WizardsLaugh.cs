using System;

public class WizardLaugh: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null)
            {
                foreach(Character character in c.hex.characters)
                {
                    if ( character.GetOwner() == c.GetOwner() || (c.alignment == character.alignment && character.alignment != AlignmentEnum.neutral)) character.doubledBy.Clear();
                }
            } else if (c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral)
            {
                c.hex.GetPC().DecreaseLoyalty(UnityEngine.Random.Range(0, 3) * c.GetMage(), c);
            }
            else return false;
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return ((c.hex.characters.Find(x => x.GetOwner() == c.GetOwner() || (c.alignment == x.alignment && x.alignment != AlignmentEnum.neutral))!= null) ||  (c.hex.GetPC() != null && ( c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral))) && c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
