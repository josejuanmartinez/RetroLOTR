using System;
using UnityEngine;

public class DoubleCharacter : AgentCharacterAction
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
            enemy.Doubled(c.GetOwner());
            // Message show in Doubled()
            // MessageDisplayNoUI.ShowMessage(enemy.hex, $"{enemy.characterName} doubled", Color.green);
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

