using System;
using UnityEngine;

public class ConjureMounts: DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            int mounts = Math.Clamp(UnityEngine.Random.Range(0, 1 * c.GetMage()), 1, 3);
            c.GetOwner().AddMounts(mounts);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
