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
            Character commander = c.hex.characters.Find(x => x != null && x.owner == c.owner && x.GetCommander() > 0);
            if (commander == null) return false;
            int troops = Math.Max(1, ApplySpellEffectMultiplier(c, 1));
            if (!commander.IsArmyCommander())
            {
                commander.CreateArmy(TroopsTypeEnum.ma, troops, false);
            }
            else
            {
                commander.GetArmy().Recruit(TroopsTypeEnum.ma, troops);
            }
            c.hex.RedrawCharacters();
            c.hex.RedrawArmies();
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.hex == null) return false;
            Character commander = c.hex.characters.Find(x => x != null && x.owner == c.owner && x.GetCommander() > 0);
            return commander != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
