using System;
using UnityEngine;

public class InfluenceDownPC : EmmissaryEnemyPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            c.hex.GetPC().DecreaseLoyalty(UnityEngine.Random.Range(1, 10) * c.GetEmmissary(), c.GetOwner());
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.hex.GetPC().loyalty > 0 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
