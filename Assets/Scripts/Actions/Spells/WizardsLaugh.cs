using System;
using System.Linq;

public class WizardLaugh: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c.hex.GetPC() == null)
            {
                foreach(Character character in c.hex.characters)
                {
                    if ( character.GetOwner() == c.GetOwner() || (c.alignment == character.alignment && character.alignment != AlignmentEnum.neutral)) character.doubledBy.Clear();
                }
            } else if (c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral)
            {
                int loyaltyLoss = UnityEngine.Random.Range(0, 3) * c.GetMage();
                loyaltyLoss = Math.Max(0, ApplySpellEffectMultiplier(c, loyaltyLoss));
                c.hex.GetPC().DecreaseLoyalty(loyaltyLoss, c);
            }
            else return false;
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return ((c.hex.characters.Find(x => x.GetOwner() == c.GetOwner() || (c.alignment == x.alignment && x.alignment != AlignmentEnum.neutral))!= null) ||  (c.hex.GetPC() != null && ( c.hex.GetPC().owner.GetAlignment() != c.GetAlignment() || c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral)));
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
