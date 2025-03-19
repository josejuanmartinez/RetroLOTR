using System;
using UnityEngine;

public class Heal: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.health < 100)
            {
                c.health += UnityEngine.Random.Range(0, 10) * c.mage;
                if(GameObject.FindFirstObjectByType<Board>().selectedCharacter == c) GameObject.FindFirstObjectByType<SelectedCharacterIcon>().Refresh(c);
            } else
            {
                Character target = c.hex.characters.Find(x => x.GetOwner() == c.GetOwner() && x.health < 100);
                if(target != null)
                {
                    target.health += UnityEngine.Random.Range(0, 10) * c.mage;
                    if (GameObject.FindFirstObjectByType<Board>().selectedCharacter == target) GameObject.FindFirstObjectByType<SelectedCharacterIcon>().Refresh(target);
                }
                else
                {
                    target = c.hex.characters.Find(x => x.GetOwner() != c.GetOwner() && x.alignment == c.alignment && x.alignment != AlignmentEnum.neutral && x.health < 100);
                    if (target != null)
                    {
                        target.health += UnityEngine.Random.Range(0, 10) * c.mage;
                        if (GameObject.FindFirstObjectByType<Board>().selectedCharacter == target) GameObject.FindFirstObjectByType<SelectedCharacterIcon>().Refresh(target);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return  c.artifacts.Find(x => x.providesSpell is Heal) != null && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
