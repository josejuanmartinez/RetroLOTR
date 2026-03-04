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
            if (c == null || c.hex == null) return false;

            bool hasEnemyArmy = c.hex.armies != null
                && c.hex.armies.Find(x =>
                    x != null
                    && x.GetCommander() != null
                    && x.GetCommander().GetOwner() != c.GetOwner()
                    && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())) != null;

            PC pc = c.hex.GetPC();
            Leader pcOwner = pc != null ? pc.owner : null;
            bool hasEnemyOwnedPc = pcOwner != null
                && pcOwner != c.GetOwner()
                && (pcOwner.GetAlignment() == AlignmentEnum.neutral || pcOwner.GetAlignment() != c.GetAlignment());

            return hasEnemyArmy || hasEnemyOwnedPc;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

