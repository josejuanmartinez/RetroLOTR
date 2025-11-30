using System;

public class WizardsFire : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Army army = FindEnemyArmyNotNeutralAtHex(c);
            if (army == null) army = FindEnemyArmyAtHex(c);
            if (army == null) return false;
            army.ReceiveCasualties(Math.Clamp(UnityEngine.Random.Range(0.05f, 0.25f) * c.GetMage(), 0.1f, 1f), c.GetOwner());
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyArmyAtHex(c) != null && c.artifacts.Find(x => x.providesSpell == actionName) != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

