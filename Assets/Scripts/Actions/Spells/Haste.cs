using System;
using System.Linq;
using UnityEngine;

public class Haste: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            int boost = (int) Math.Clamp(Math.Round(c.GetMage() * UnityEngine.Random.Range(0.1f, 0.3f)), 0, 3);
            c.moved = Math.Max(c.moved - 2 - boost, 0);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"+{boost} <sprite name=\"movement\"/>", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return !c.IsArmyCommander();
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

