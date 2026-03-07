using System;
public class MaterialRetrievalOrActionCirithUngol : MaterialRetrievalOrAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            return GrantResources(c, ProducesEnum.iron, 1, ProducesEnum.iron, 1, "CirithUngol");
        };
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null;
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
