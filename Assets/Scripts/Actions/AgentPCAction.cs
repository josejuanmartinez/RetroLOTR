using System;
using UnityEngine;

public class AgentPCAction : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            Hex capitalHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == c.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;
            int random = UnityEngine.Random.Range(0, 5);
            string message = "Agent returned to capital";
            if (random > c.GetAgent())
            {
                message += " wounded";
                c.Wounded(c.hex.GetPC().owner, random * 10);
            }
            FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, capitalHex, true);
            MessageDisplay.ShowMessage(message, Color.green);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.hex.GetPC() != null && 
            c.hex.GetPC().owner != c.GetOwner() &&
            (c.hex.GetPC().owner.GetAlignment() == AlignmentEnum.neutral || c.hex.GetPC().owner.GetAlignment() != c.GetAlignment()) &&
            (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
