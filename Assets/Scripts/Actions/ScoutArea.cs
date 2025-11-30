using System;
using UnityEngine;

public class ScoutArea : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            c.hex.RevealArea(1, true, c.GetOwner());
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area scouted", Color.green);
            return true;
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c);
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

