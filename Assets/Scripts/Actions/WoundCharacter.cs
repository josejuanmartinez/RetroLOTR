using System;

public class WoundCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        PlayableLeader playable = (c.GetOwner() as PlayableLeader);
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = c.hex.characters.Find(x => x.GetAlignment() != c.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral);
            if (enemy == null) enemy = c.hex.characters.Find(x => x.GetAlignment() == AlignmentEnum.neutral);
            if (enemy == null) return false;
            enemy.health -= Math.Max(1, enemy.health - (UnityEngine.Random.Range(0, 20) *  c.agent));
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c);
        };
        base.Initialize(c, condition, effect);
    }
}
