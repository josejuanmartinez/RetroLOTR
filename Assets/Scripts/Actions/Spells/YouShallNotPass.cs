using System;
using System.Collections.Generic;

public class YouShallNotPass : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            List<Character> chars = FindEnemyCharactersNotArmyCommandersAtHex(c);
            if (chars == null || chars.Count < 1) return false;
            chars.ForEach((x) => {
                x.Halt();
                if (x.race == RacesEnum.Balrog) x.Wounded(c.GetOwner(), UnityEngine.Random.Range(0, 20) * c.GetMage());
            });
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => { return FindEnemyCharactersNotArmyCommandersAtHex(c).Count > 0 && (originalCondition == null || originalCondition(c));};
        base.Initialize(c, condition, effect);
    }
}
