using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OhElbereth : FreeSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyNonNeutralCharactersAtHex(c) != null; 
        };
        async System.Threading.Tasks.Task<bool> ohElberethAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> enemies = c.hex.characters.Where(x =>
                x.GetOwner() != c.GetOwner() &&
                x.GetAlignment() != c.GetAlignment() &&
                x.GetAlignment() != AlignmentEnum.neutral
            ).ToList();
            if (enemies.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character enemy = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select enemy character", "Ok", "Cancel", enemies.Select(x => x.characterName).ToList(), isAI);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                enemy = enemies.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                enemy = FindEnemyNonNeutralCharactersAtHex(c);
            }

            if (enemy == null) return false;

            int damage = UnityEngine.Random.Range(0, 20) * c.GetMage();
            damage = Math.Max(0, ApplySpellEffectMultiplier(c, damage));
            enemy.Wounded(c.GetOwner(), damage);
            if(enemy.race == RacesEnum.Nazgul) enemy.Halt();
            return true;
        }
        base.Initialize(c, condition, effect, ohElberethAsync);
    }
}
