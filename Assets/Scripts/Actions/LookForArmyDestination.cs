using System;
using System.Collections.Generic;

public class LookForArmyDestination : CommanderArmyAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            List<Hex> destinations = c.reachableHexes;
            if (destinations.Count < 1) return false;
            Army army = FindTargetEnemyArmyInRange(destinations, c);
            if(army !=null && army.commander != null && army.GetSize() > 0)
            {
                FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, army.commander.hex, true); 
            }
            else
            {
                PC pc = FindEnemyTargetPCInRange(destinations, c);
                if (pc != null)
                {
                    FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, pc.hex, true);
                }
                else
                {
                    // Move to a random hex in range
                    FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, destinations[UnityEngine.Random.Range(0, destinations.Count)], true);
                }
            }

            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
