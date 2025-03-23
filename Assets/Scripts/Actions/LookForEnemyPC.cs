using System;

public class LookForEnemyPCArmy : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        // Hex destination = FindFirstObjectByType<Board>().GetHexesInRange(c.hex, c.GetMaxMovement()).Find(hex => hex.GetPC() != null && hex.GetPC().GetAlignment() != c.GetAlignment() && hex.GetPC().GetAlignment() != AlignmentEnum.neutral);

        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { 
            // if (destination == null) return false;
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
