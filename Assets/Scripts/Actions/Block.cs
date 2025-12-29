using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Block : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyArmyAtHex(c) != null;
        };
        async System.Threading.Tasks.Task<bool> blockAsync(Character c)
        {            
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            List<Character> characters = c.hex.GetEnemyArmies(c.GetOwner());
            if(characters.Count < 1) return false;
            bool isAI = !c.isPlayerControlled;
            Army enemy = null;
            if(!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select enemy army", "Ok", "Cancel", characters.Select(x => x.characterName).ToList(), isAI);    
                Character enemyChar = c.hex.characters.Find(x => x.characterName == targetCharacter);
                if(!enemyChar.IsArmyCommander()) return false;
                enemy = enemyChar.GetArmy();
            } 
            else
            {
                enemy = FindEnemyArmyAtHex(c);    
            }
            
            if (enemy == null) return false;

            return true;
        }
        base.Initialize(c, condition, effect, blockAsync);
    }
}
