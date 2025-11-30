using System;
using UnityEngine;

public class WoundCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
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
            return true; 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharacterTargetAtHex(c) != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

