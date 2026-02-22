using System;

public class EventAction : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.None;

    public override bool IsRoleEligible(Character character)
    {
        return character != null;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) => originalEffect == null || originalEffect(c);
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
