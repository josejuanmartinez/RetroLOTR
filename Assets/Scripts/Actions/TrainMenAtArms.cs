using System;

public class TrainMenAtArms : CommanderPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (!c.IsArmyCommander())
            {
                c.CreateArmy(TroopsTypeEnum.ma, 1, false);
            }
            else
            {
                c.GetArmy().Recruit(TroopsTypeEnum.ma, 1);
            }
            c.hex.RedrawCharacters();
            c.hex.RedrawArmies();
            return true;
        };
        condition = (c) => { return originalCondition == null || originalCondition(c); };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

