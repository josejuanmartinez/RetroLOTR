using System;
using System.Linq;
using UnityEngine;

public class ConjureMounts: DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            int mounts = Math.Clamp(UnityEngine.Random.Range(0, 1 * c.GetMage()), 1, 3);
            mounts = Math.Max(1, ApplySpellEffectMultiplier(c, mounts));
            c.GetOwner().AddMounts(mounts);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c.hex == null) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            return pc.owner.GetAlignment() == c.GetOwner().GetAlignment() && (c.GetOwner() == pc.owner || pc.owner.GetAlignment() != AlignmentEnum.neutral);
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
