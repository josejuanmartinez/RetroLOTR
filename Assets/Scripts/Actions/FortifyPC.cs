using System;
using UnityEngine;

public class FortifyPC : CommanderPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null || pc.owner == null) return false;
            if (pc.owner != c.GetOwner()) return false;
            pc.IncreaseFort();
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"{pc.pcName} <sprite name=\"fort\"> {pc.GetFortSize()}", Color.green);
            return true; 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.GetPC() != null && c.hex.GetPC().fortSize < FortSizeEnum.citadel;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

