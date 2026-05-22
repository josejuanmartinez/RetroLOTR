using System;
using System.Collections.Generic;

public class YouShallNotPass : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharactersNotArmyCommandersAtHex(c).Count > 0;
        };
        async System.Threading.Tasks.Task<bool> youShallNotPassAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> targets = FindEnemyCharactersNotArmyCommandersAtHex(c);
            if (targets == null || targets.Count < 1) return false;

            foreach (Character target in targets)
            {
                target.Halt();
            }

            string names = targets.Count == 1
                ? targets[0].characterName
                : $"{targets.Count} characters";

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"You shall not pass! {names} halted.", UnityEngine.Color.cyan);
            return true;
        }
        base.Initialize(c, condition, effect, youShallNotPassAsync);
    }
}
