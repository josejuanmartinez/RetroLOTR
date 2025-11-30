using System;

public class CommanderEnemyArmyAction : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => 
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return (
                c.hex.armies.Find(x => x.GetCommander() != null && x.GetCommander().GetOwner() != c.GetOwner() && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())) != null 
                ||
                (c.hex.GetPC() != null && c.hex.GetPC().owner != c.GetOwner() && (c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral || c.hex.GetPC().owner.GetAlignment() != c.GetAlignment()))
            ); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

