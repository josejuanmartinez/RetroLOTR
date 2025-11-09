using System;
using System.Collections.Generic;

public class IceStorm : DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Army army = FindEnemyArmyAtHex(c);
            if (army == null) return false;
            army.commander.Halt();
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return FindEnemyArmyAtHex(c) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
