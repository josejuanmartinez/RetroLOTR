using System;

public class Courage : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Army army = FindFriendlyArmyAtHex(c);
            if (army == null) return false;
            army.commander.Encourage();
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return FindFriendlyArmyAtHex(c) != null && c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}