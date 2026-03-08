using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HopeReborn : CharacterAction
{
    private static readonly StatusEffectEnum[] NegativeEffects =
    {
        StatusEffectEnum.Fear,
        StatusEffectEnum.Despair,
        StatusEffectEnum.Poisoned,
        StatusEffectEnum.Burning,
        StatusEffectEnum.Frozen,
        StatusEffectEnum.Blocked,
        StatusEffectEnum.Halted
    };

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.hex == null) return false;

            List<Character> targets = c.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int cleared = 0;
            foreach (Character t in targets)
            {
                foreach (var s in NegativeEffects)
                {
                    if (t.HasStatusEffect(s))
                    {
                        t.ClearStatusEffect(s);
                        cleared++;
                    }
                }
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Hope Reborn cleanses {cleared} negative status effect(s) in radius 2.", Color.green);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c != null && c.hex != null;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
