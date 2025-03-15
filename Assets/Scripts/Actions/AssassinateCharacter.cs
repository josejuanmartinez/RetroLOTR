using System;

public class AssassinateCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        PlayableLeader playable = (c.GetOwner() as PlayableLeader);
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = c.hex.characters.Find(
                x => x.GetAlignment() != c.GetAlignment() && 
                x.GetAlignment() != AlignmentEnum.neutral &&
                (x as PlayableLeader == null)
            );
            if (enemy == null)
            {
                enemy = c.hex.characters.Find(
                    x => x.GetAlignment() == AlignmentEnum.neutral &&
                    (x as PlayableLeader == null)
                );
            }

            if (enemy == null) return false;

            enemy.Killed(c.GetOwner());

            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.hex.characters.Find(x => x.GetAlignment() != c.GetAlignment() && (x as PlayableLeader == null)) != null &&
            (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
