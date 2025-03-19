using System;

public class Teleport: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex randomHex = FindFirstObjectByType<Board>().GetHexes().Find(x => !c.GetOwner().visibleHexes.Contains(x));
            if (randomHex == null) return false;
            randomHex.RevealArea(c.mage);
            FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, randomHex, true);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell is Teleport) != null && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
