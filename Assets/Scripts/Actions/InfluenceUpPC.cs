using System;
using UnityEngine;

public class InfluenceUpPC : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            PC pc = c.hex.GetPC();
            int loyalty = UnityEngine.Random.Range(1, 10) * c.GetEmmissary();
            pc.IncreaseLoyalty(loyalty, c);
            // MessageDisplayNoUI.ShowMessage(pc.hex, c, $"{pc.pcName} +{loyalty} loyalty!", Color.green);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => { return c.hex.GetPC() != null && c.hex.GetPC().loyalty < 100 && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
