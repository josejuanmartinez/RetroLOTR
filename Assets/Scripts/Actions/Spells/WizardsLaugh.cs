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
            PC pc = c.hex.GetPC();
            if (pc == null) return false;

            if (pc.owner.GetAlignment() != c.GetAlignment() || pc.owner.GetAlignment() == AlignmentEnum.neutral)
            {
                int loyaltyLoss = UnityEngine.Random.Range(0, 3) * c.GetMage();
                loyaltyLoss = Math.Max(1, ApplySpellEffectMultiplier(c, loyaltyLoss));
                pc.DecreaseLoyalty(loyaltyLoss, c);
            }
            else return false;
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            AlignmentEnum pcAlignment = pc.owner.GetAlignment();
            return pcAlignment != c.GetAlignment() || pcAlignment == AlignmentEnum.neutral;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
