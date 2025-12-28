using System;
using System.Linq;
using UnityEngine;

// It is not necessarily MageAction as you can have an artifact and not be a mage!
public class Spell : CharacterAction
{
    protected int mageLevelRequirementWithoutArtifact;

    protected override AdvisorType DefaultAdvisorType => AdvisorType.Magic;

    public override bool IsRoleEligible(Character character)
    {
        if (character == null) return false;
        return character.GetMage() > 0 || IsProvidedByArtifact();
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
        mageLevelRequirementWithoutArtifact = Math.Max(1, mageSkillRequired);
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
        mageSkillRequired = providedByArtifact ? 0 : mageLevelRequirementWithoutArtifact;
        base.Initialize(c, condition, effect, asyncEffect);
    }

    protected bool HasSpellArtifact(Character c)
    {
        return GetSpellArtifact(c) != null;
    }

    private Artifact GetSpellArtifact(Character c)
    {
        if (c == null) return null;
        if (c.artifacts == null) return null;
        string baseName = NormalizeSpellName(actionName);
        if (string.IsNullOrWhiteSpace(baseName)) return null;
        return c.artifacts.FirstOrDefault(a =>
            a != null &&
            !string.IsNullOrEmpty(a.providesSpell) &&
            NormalizeSpellName(a.providesSpell) == baseName);
    }

    protected float GetSpellEffectMultiplier(Character character)
    {
        bool hasArtifact = HasSpellArtifact(character);
        bool canCastAsMage = character.GetMage() >= mageLevelRequirementWithoutArtifact;
        return hasArtifact && canCastAsMage ? 1.5f : 1f;
    }

    protected int ApplySpellEffectMultiplier(Character character, int baseValue)
    {
        return Mathf.RoundToInt(baseValue * GetSpellEffectMultiplier(character));
    }

    protected float ApplySpellEffectMultiplier(Character character, float baseValue)
    {
        return baseValue * GetSpellEffectMultiplier(character);
    }

    protected override string BuildHoverText()
    {
        string text = base.BuildHoverText();
        Artifact artifact = GetSpellArtifact(character);
        if (artifact == null) return text;
        return $"{text}<br><size=80%><color=red>This action is only available as long as you hold {artifact.artifactName}</color></size>";
    }
}
