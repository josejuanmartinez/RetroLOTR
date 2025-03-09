using System;

public class TrainMetAtArms : CommanderPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.army == null)
            {
                c.army = new(c, TroopsTypeEnum.ma, 1)
                {
                    commander = c
                };
            }
            else
            {
                c.army.ma += 1;
            }
            c.hex.armies.Add(c.army);
            c.hex.RedrawArmies();
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return originalCondition == null || originalCondition(c); };
        base.Initialize(c, condition, effect);
    }
}
