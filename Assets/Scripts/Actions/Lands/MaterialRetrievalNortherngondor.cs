using System;
using UnityEngine;

public class MaterialRetrievalNortherngondor : MaterialRetrieval
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
            c.GetOwner().AddLeather(1);
            c.GetOwner().AddMounts(1);
            c.GetOwner().AddTimber(1);
            c.GetOwner().AddIron(1);
            c.GetOwner().AddGold(1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, "NorthernGondor: +1 leather, 1 mounts, 1 timber, 1 iron, 1 gold", Color.yellow);
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
