using System;

public class CommanderEnemyArmyAction : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return (
            c.hex.armies.Find(x => x.GetCommander() != null && x.GetAlignment() != c.GetAlignment()) != null ||
            (c.hex.GetPC() != null && c.hex.GetPC().owner.GetAlignment() != c.GetAlignment())
            ) 
            && 
            (
            originalCondition == null || originalCondition(c)
            ); 
        };
        base.Initialize(c, condition, effect);
    }
}
