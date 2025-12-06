using System;
using System.Linq;
using UnityEngine;

public class CastLight: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Hex hex = c.hex;
            hex.RevealArea(1, true, c.owner);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area secrets revealed!", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return true; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

