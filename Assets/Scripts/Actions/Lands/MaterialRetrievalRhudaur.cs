using System;
using UnityEngine;

public class MaterialRetrievalRhudaur : MaterialRetrieval
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
            c.GetOwner().AddTimber(2);
            c.GetOwner().AddIron(1);
            c.GetOwner().AddGold(1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, "Rhudaur: +1 leather, 2 timber, 1 iron, 1 gold", Color.yellow);
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
