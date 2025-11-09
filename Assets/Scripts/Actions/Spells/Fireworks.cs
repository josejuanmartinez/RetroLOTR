using System;

public class Fireworks: FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PC pc = c.hex.GetPC();
            if (pc == null) return false;            
            int loyalty = UnityEngine.Random.Range(0, 10) * c.GetMage();
            if (pc.owner.GetAlignment() == c.GetAlignment())
            {
                c.hex.GetPC().IncreaseLoyalty(loyalty);
            } else
            {
                c.hex.GetPC().DecreaseLoyalty(loyalty, c.GetOwner());
            }            
            
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.artifacts.Find(x => x.providesSpell == actionName) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
