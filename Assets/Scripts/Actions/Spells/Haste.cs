using System;

public class Haste: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            int boost = (int) Math.Clamp(Math.Round(c.GetMage() * UnityEngine.Random.Range(0.1f, 0.3f)), 0, 3);
            c.moved = Math.Max(c.moved - 2 - boost, 0);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && !c.IsArmyCommander() && (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
