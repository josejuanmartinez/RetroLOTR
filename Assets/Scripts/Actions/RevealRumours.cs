using System;
using UnityEngine;

public class RevealRumours : EmmissaryAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            int enemyRumours = (int) Math.Max(1, Math.Floor(c.GetEmmissary() * UnityEngine.Random.Range(0.1f, 0.5f)));
            int friendlyRumours = (int) Math.Max(2, Math.Floor(c.GetEmmissary() * UnityEngine.Random.Range(0.25f, 0.75f)));
            RumoursManager.GetRumours(c.GetAlignment(), enemyRumours, friendlyRumours);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c); 
        };
        base.Initialize(c, condition, effect);
    }
}
