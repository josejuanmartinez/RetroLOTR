using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AssassinateCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalAsyncEffect = asyncEffect;
        var originalCondition = condition;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharacterTargetAtHex(c) != null;
        };
        effect = (c) => true;
        Func<Character, System.Threading.Tasks.Task<bool>> assassinateAsync = async (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> characters = c.hex.GetEnemyCharacters(c.GetOwner());
            if(characters.Count < 1) return false;
            bool isAI = FindFirstObjectByType<Game>().player == c.GetOwner();
            Character enemy = null;
            if(!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select enemy character", "Ok", "Cancel", characters.Select(x => x.characterName).ToList(), isAI);    
                enemy = c.hex.characters.Find(x => x.characterName == targetCharacter);
            } 
            else
            {
                enemy = FindEnemyCharacterTargetAtHex(c);    
            }
            
            if (enemy == null) return false;
            
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
            MessageDisplay.ShowMessage(message, color);

            enemy.Killed(c.GetOwner());
            MessageDisplayNoUI.ShowMessage(enemy.hex, c, $"{enemy.characterName} assassinated!", Color.green);

            return true; 
        };

        base.Initialize(c, condition, effect, assassinateAsync);
    }
}
