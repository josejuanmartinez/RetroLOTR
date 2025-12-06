using System;
using System.Linq;
using UnityEngine;

public class OhElbereth : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Character enemy = FindEnemyNonNeutralCharactersAtHex(c);
            if (enemy == null) return false;
            enemy.Wounded(c.GetOwner(), UnityEngine.Random.Range(0, 20) * c.GetMage());
            if(enemy.race == RacesEnum.Nazgul) enemy.Halt();
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return true; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

