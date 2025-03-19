using System;

public class ReturnToCapital: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Hex capitalHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.pc != null && x.pc.owner == c.GetOwner() && x.pc.isCapital);
            if (capitalHex == null) return false;
            FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, capitalHex, true);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell is ReturnToCapital) != null && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
