using System;
using System.Linq;
using UnityEngine;

public class Heal: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            int health = UnityEngine.Random.Range(0, 10) * c.GetMage();
            Character selectedCharacter = FindFirstObjectByType<Board>().selectedCharacter;
            if (c.health < 100)
            {
                c.Heal(health);
                if(selectedCharacter == c) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(c);
            } else
            {
                Character target = c.hex.characters.Find(x => x.GetOwner() == c.GetOwner() && x.health < 100);
                if(target != null)
                {
                    target.Heal(health);
                    if (selectedCharacter == target) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(target);
                }
                else
                {
                    target = c.hex.characters.Find(x => x.GetOwner() != c.GetOwner() && x.alignment == c.alignment && x.alignment != AlignmentEnum.neutral && x.health < 100);
                    if (target != null)
                    {
                        target.Heal(health);
                        if (selectedCharacter == target) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(target);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.characters.Find(x => x.health < 100 && (x.GetOwner() == c.GetOwner() || x.GetAlignment() == c.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral)) != null; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

