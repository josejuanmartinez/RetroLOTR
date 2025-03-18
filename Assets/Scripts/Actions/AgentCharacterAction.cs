using System;

public class AgentCharacterAction : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => { return originalEffect == null || originalEffect(c); };
        condition = (c) => {
            return c.hex.characters.Find(x => x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment()) != null &&
            (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
    protected Character FindTarget(Character assassin)
    {
        Character target = FindNonNeutralCharactersNoLeader(assassin);
        if (target) return target;
        target = FindCharactersNoLeaders(assassin);
        if (target) return target;
        target = FindNonNeutralCharacters(assassin);
        if (target) return target;
        target = FindCharacters(assassin);
        return target;
    }
    private Character FindNonNeutralCharactersNoLeader(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral &&
            x is not PlayableLeader
        );
    }
    private Character FindCharactersNoLeaders(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())&& (x is not PlayableLeader)
        );
    }

    private Character FindNonNeutralCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() && 
            x.GetAlignment() != c.GetAlignment() && 
            x.GetAlignment() != AlignmentEnum.neutral
        );
    }

    private Character FindCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => x.GetOwner() != c.GetOwner() && 
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }
}
