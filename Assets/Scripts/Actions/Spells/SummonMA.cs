using System;

public class SummonMA: DarkSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Character commander = c.hex.characters.Find(x => x.owner == c.owner && x.GetCommander() > 0);
            if (!commander.IsArmyCommander())
            {
                commander.CreateArmy(TroopsTypeEnum.ma, 1, false);
            }
            else
            {
                commander.GetArmy().Recruit(TroopsTypeEnum.ma, 1);
            }
            c.hex.RedrawCharacters();
            c.hex.RedrawArmies();
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && c.artifacts.Find(x => x.providesSpell == "SummonMA") != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

