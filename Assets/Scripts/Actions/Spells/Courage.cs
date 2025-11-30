using System;
using UnityEngine;

public class Courage : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Army army = FindFriendlyArmyAtHex(c);
            if (army == null) return false;
            int turns = 1 + c.GetMage() * Mathf.FloorToInt(UnityEngine.Random.Range(0.0f, 0.5f));
            army.commander.Encourage(turns);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Courage for ${turns} turns!", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindFriendlyArmyAtHex(c) != null && c.artifacts.Find(x => x.providesSpell == actionName) != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

