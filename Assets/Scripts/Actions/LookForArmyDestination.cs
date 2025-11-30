using System;
using System.Collections.Generic;

public class LookForArmyDestination : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            List<Hex> destinations = c.reachableHexes;
            if (destinations.Count < 1) return false;
            Army army = FindTargetEnemyArmyInRange(destinations, c);
            if(army !=null && army.commander != null && army.GetSize() > 0)
            {
                FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, army.commander.hex, true); 
            }
            else
            {
                PC pc = FindEnemyTargetPCInRange(destinations, c);
                if (pc != null)
                {
                    FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, pc.hex, true);
                }
                else
                {
                    // Move to a random hex in range
                    FindFirstObjectByType<Board>().MoveCharacterOneHex(c, c.hex, destinations[UnityEngine.Random.Range(0, destinations.Count)], true);
                }
            }

            return true; 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c)); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

