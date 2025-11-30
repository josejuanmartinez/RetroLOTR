using NUnit.Framework;
using System;
using UnityEngine;

public class UndoubleCharacter : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Character doubled = FindDoubledCharacters(c);
            
            if (doubled == null) return false;
            
            doubled.Undouble(c.GetOwner());
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{c.characterName} will not reveal secrets anymore", Color.green);

            return true; 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindDoubledCharacters(c) != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
    private Character FindDoubledCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(x => x.doubledBy.Contains(c.GetOwner()));
    }
}

