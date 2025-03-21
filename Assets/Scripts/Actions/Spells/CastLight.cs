using System;

public class CastLight: FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Army army = FindEnemyArmyNotNeutral(c);
            if (army == null) return false;
            army.ReceiveCasualties(Math.Clamp(UnityEngine.Random.Range(0.05f, 0.25f) * c.GetMage(), 0.1f, 1f));
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return FindEnemyArmyNotNeutral(c) != null && c.artifacts.Find(x => x.providesSpell is CastLight) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
