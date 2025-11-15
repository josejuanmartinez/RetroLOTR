using System;
using UnityEngine;

public class WoundCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = FindEnemyCharacterTargetAtHex(c);
            if (enemy == null) return false;

            int wound = UnityEngine.Random.Range(0, 20) * c.GetAgent();
            Hex capitalHex = FindFirstObjectByType<Board>().GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == c.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;
            int random = UnityEngine.Random.Range(0, 5);
            string message = $"Agent returned to capital";
            Color color = Color.green;
            if (random > c.GetAgent())
            {
                message += " wounded";
                c.Wounded(c.hex.GetPC().owner, random * 10);
                color = Color.red;
            }
            FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, capitalHex, true);
            MessageDisplayNoUI.ShowMessage(c.hex, c, message, color);

            enemy.Wounded(c.GetOwner(), wound);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => { return FindEnemyCharacterTargetAtHex(c) != null && (originalCondition == null || originalCondition(c));};
        base.Initialize(c, condition, effect);
    }
}
