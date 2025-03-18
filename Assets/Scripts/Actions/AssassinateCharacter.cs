using System;

public class AssassinateCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = FindTarget(c);

            if (enemy == null) return false;

            enemy.Killed(c.GetOwner());

            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => { return FindTarget(c) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
