using System;

public class TrainLightCavalry : CommanderPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (!c.IsArmyCommander())
            {
                c.CreateArmy(TroopsTypeEnum.lc, 1, false);
            }
            else
            {
                c.GetArmy().Recruit(TroopsTypeEnum.lc, 1);
            }
            c.hex.RedrawCharacters();
            c.hex.RedrawArmies();
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return originalCondition == null || originalCondition(c); };
        base.Initialize(c, condition, effect);
    }
}
