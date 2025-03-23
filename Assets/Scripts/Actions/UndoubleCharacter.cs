using NUnit.Framework;
using System;
using UnityEngine;

public class UndoubleCharacter : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            Character doubled = FindDoubledCharacters(c);
            
            if (doubled == null) return false;
            
            doubled.Undouble(c.GetOwner());
            MessageDisplay.ShowMessage($"{c.characterName} will not reveal secrets anymore", Color.green);

            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => { return FindDoubledCharacters(c) != null && (originalCondition == null || originalCondition(c)); };
        base.Initialize(c, condition, effect);
    }
    private Character FindDoubledCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(x => x.doubledBy.Contains(c.GetOwner()));
    }
}
