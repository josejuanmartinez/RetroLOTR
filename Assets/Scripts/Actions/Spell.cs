using System;

public class Spell : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Magic;

    // Artifact spell-boosting was removed, but spell implementations still call
    // through this helper. Keep it as a pass-through so those actions compile
    // and preserve their base values.
    protected int ApplySpellEffectMultiplier(Character caster, int value)
    {
        return value;
    }

    protected float ApplySpellEffectMultiplier(Character caster, float value)
    {
        return value;
    }

    public override bool IsRoleEligible(Character character)
    {
        if (character == null) return false;
        return character.GetMage() > 0;
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
        effect = (c) => {
            return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.GetMage() > 0;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
