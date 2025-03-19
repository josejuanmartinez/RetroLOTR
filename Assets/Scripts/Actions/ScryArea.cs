using System;

public class ScryArea : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex randomHex = FindFirstObjectByType<Board>().GetHexes().Find(x => !c.GetOwner().visibleHexes.Contains(x));
            if (randomHex == null) return false;
            randomHex.RevealArea(c.mage);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return c.GetOwner() == FindFirstObjectByType<Game>().player && c.artifacts.Find(x => x.providesSpell is ScryArea) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
