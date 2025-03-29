using System;

public class SummonMA: DarkSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
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
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.hex.GetPC().owner == c.GetOwner() && c.artifacts.Find(x => x.providesSpell == "SummonMA") != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
