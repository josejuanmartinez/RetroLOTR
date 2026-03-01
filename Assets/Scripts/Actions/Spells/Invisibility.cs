using System;
using UnityEngine;

public class Invisibility : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            int turns = Math.Max(1, ApplySpellEffectMultiplier(c, 1 + Mathf.FloorToInt(c.GetMage() / 2f)));
            return StealthEffectHelper.Apply(
                c,
                turns,
                $"{c.characterName} turns invisible for {turns} turn(s).",
                Color.gray);
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null && !c.killed;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
