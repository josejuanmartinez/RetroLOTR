using System;
using UnityEngine;

public class DoubleCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character enemy = FindEnemyCharacterTargetAtHex(c);
            if (enemy == null) return false;
            enemy.Doubled(c.GetOwner());
            // Message show in Doubled()
            // MessageDisplayNoUI.ShowMessage(enemy.hex, $"{enemy.characterName} doubled", Color.green);
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => { return FindEnemyCharacterTargetAtHex(c) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
}
