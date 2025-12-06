using System;
using System.Linq;

// It is not necessarily MageAction as you can have an artifact and not be a mage!
public class Spell : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Magic;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            bool hasSpellArtifact = HasSpellArtifact(c);
            return hasSpellArtifact || c.GetMage() > 0; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        // Require at least mage level 1 unless provided by artifact
        bool providedByArtifact = HasSpellArtifact(c);
        mageSkillRequired = providedByArtifact ? 0 : Math.Max(1, mageSkillRequired);
        base.Initialize(c, condition, effect, asyncEffect);
    }

    protected bool HasSpellArtifact(Character c)
    {
        return c.artifacts.Any(a => a != null && !string.IsNullOrEmpty(a.providesSpell) && a.providesSpell.Equals(actionName, StringComparison.OrdinalIgnoreCase));
    }
}
