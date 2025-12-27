using System;

public class EmmissaryCommanderAction : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Diplomatic;

    public override bool IsRoleEligible(Character character)
    {
        return character != null && (character.GetCommander() > 0 || character.GetEmmissary() > 0);
    }

    public override bool ShouldShowWhenUnavailable()
    {
        return true;
    }

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return (c.GetCommander() > 0 || c.GetEmmissary() > 0); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

