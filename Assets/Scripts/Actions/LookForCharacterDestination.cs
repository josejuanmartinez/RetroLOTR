using System;
using System.Collections.Generic;

public class LookForCharacterDestination : CharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            /*List<Hex> destinations = c.reachableHexes;
            if (destinations.Count < 1) return false;
            Character enemyCharacter = FindTargetEnemyInRange(destinations, c);
            Army enemyArmy = FindTargetEnemyArmyInRange(destinations, c);
            PC enemyPC = FindEnemyTargetPCInRange(destinations, c);
            Hex destination = destinations[UnityEngine.Random.Range(0, destinations.Count)];
            PC ownPC = FindOwnTargetPCInRange(destinations, c);

            if (c.GetCommander() > 0)
            {
                // No army (otherwise it would be LookForAmyDestination) so come back to PC
                if (c.hex.GetPC() == null)
                {
                    destination = ownPC.hex;
                }
                else
                {
                    // Don't move until I have an army
                    destination = c.hex;
                }
            }
            if(c.GetAgent() > 0)
            {
                if (enemyPC != null) 
                {
                    destination = enemyPC.hex;
                }
                else if (enemyArmy != null && enemyArmy.commander != null && enemyArmy.GetSize() > 0)
                {
                    destination = enemyArmy.commander.hex;
                }
                else if (enemyCharacter != null)
                {
                    destination = enemyCharacter.hex;
                }
                else
                {
                    // Otherwise, explore
                }
            }
            else if (c.GetEmmissary() > 0)
            {
                if (c.GetOwner().GetGoldPerTurn() <= 1)
                {
                    destination = ownPC.hex;
                }
                else if (c.GetOwner().controlledPcs.Find(pc => pc.loyalty < 50) != null)
                {
                    destination = c.GetOwner().controlledPcs.Find(pc => pc.loyalty < 50).hex;
                }
                else if (enemyPC != null)
                {
                    destination = enemyPC.hex;
                }
                else if (enemyCharacter != null)
                {
                    destination = enemyCharacter.hex;
                }
                else if (c.GetOwner().controlledPcs.Count >= FindFirstObjectByType<Game>().maxPcsPerPlayer)
                {
                    // Don't explore, you can't build more PCs
                    destination = ownPC.hex;
                }
                else
                {
                    // Otherwise, explore to probably build PCs
                }
            } else if (c.GetMage() > 0)
            {
                if (enemyArmy != null && enemyArmy.commander != null && enemyArmy.GetSize() > 0)
                {
                    destination = enemyArmy.commander.hex;
                }
                else if (enemyCharacter != null)
                {
                    destination = enemyCharacter.hex;
                }
                else
                {
                    // Otherwise, explore
                }
            }
            FindFirstObjectByType<Board>().MoveCharacter(c, c.hex, enemyPC.hex, true);*/
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return !c.IsArmyCommander(); 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

