using System;
using System.Collections.Generic;
using System.Linq;

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

            List<Character> chars = FindEnemyCharactersNotArmyCommandersAtHex(c);
            if (chars == null || chars.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select character to halt", "Ok", "Cancel", chars.Select(x => x.characterName).ToList(), isAI);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                target = chars.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                target = chars.FirstOrDefault(x => x.race == RacesEnum.Balrog) ?? chars[UnityEngine.Random.Range(0, chars.Count)];
            }

            if (target == null) return false;

            target.Halt();
            if (target.race == RacesEnum.Balrog)
            {
                int damage = UnityEngine.Random.Range(0, 20) * c.GetMage();
                damage = Math.Max(0, ApplySpellEffectMultiplier(c, damage));
                target.Wounded(c.GetOwner(), damage);
            }
            return true; 
        }
        base.Initialize(c, condition, effect, youShallNotPassAsync);
    }
}
