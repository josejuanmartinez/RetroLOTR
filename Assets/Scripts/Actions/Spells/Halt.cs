using System;
using System.Collections.Generic;
using System.Linq;

public class Halt : Spell
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
        async System.Threading.Tasks.Task<bool> haltAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> targets = FindEnemyCharactersNotArmyCommandersAtHex(c);
            if (targets.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select character to halt", "Ok", "Cancel", targets.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                target = targets.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                target = targets[UnityEngine.Random.Range(0, targets.Count)];
            }

            if (target == null) return false;

            target.Halt();
            return true; 
        }
        base.Initialize(c, condition, effect, haltAsync);
    }
}
