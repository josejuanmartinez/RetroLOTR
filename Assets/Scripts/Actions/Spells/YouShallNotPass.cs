using System;
using System.Collections.Generic;

public class YouShallNotPass : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            List<Character> chars = FindEnemyCharactersNotArmyCommandersAtHex(c);
            if (chars == null || chars.Count < 1) return false;
            chars.ForEach((x) => {
                x.Halt();
                if (x.race == RacesEnum.Balrog) x.Wounded(c.GetOwner(), UnityEngine.Random.Range(0, 20) * c.GetMage());
            });
            return true; 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharactersNotArmyCommandersAtHex(c).Count > 0;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

