using System;
using UnityEngine;

public class MaterialRetrievalSoutherngondor : MaterialRetrieval
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.GetOwner() == null) return false;
            c.GetOwner().AddMounts(2);
            c.GetOwner().AddTimber(1);
            c.GetOwner().AddGold(2);
            MessageDisplayNoUI.ShowMessage(c.hex, c, "SouthernGondor: +2 mounts, 1 timber, 2 gold", Color.yellow);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null && c.GetOwner() != null;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
