using System;
using UnityEngine;

public class OhElbereth : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = FindEnemyNonNeutralCharactersAtHex(c);
            if (enemy == null) return false;
            enemy.Wounded(c.GetOwner(), UnityEngine.Random.Range(0, 20) * c.GetMage());
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == "OhElbereth") != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
