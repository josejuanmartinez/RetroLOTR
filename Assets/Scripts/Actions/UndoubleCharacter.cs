using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UndoubleCharacter : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            Character doubled = FindDoubledCharacters(c);
            if (doubled == null) return false;
            bool isFriendlyAligned = doubled.GetAlignment() == c.GetAlignment() && doubled.GetAlignment() != AlignmentEnum.neutral;
            bool sameOwner = doubled.GetOwner() == c.GetOwner();
            return (sameOwner || isFriendlyAligned);
        };
        async System.Threading.Tasks.Task<bool> undoubleAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> doubledChars = c.hex.characters.FindAll(x =>
                x.doubledBy.Contains(c.GetOwner()) &&
                (x.GetOwner() == c.GetOwner() || (x.GetAlignment() == c.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral)));
            if (doubledChars.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character doubled = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select doubled character", "Ok", "Cancel", doubledChars.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                doubled = doubledChars.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                doubled = FindDoubledCharacters(c);
            }
            
            if (doubled == null) return false;
            
            doubled.Undouble(c.GetOwner());
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{c.characterName} will not reveal secrets anymore", Color.green);

            return true; 
        }
        base.Initialize(c, condition, effect, undoubleAsync);
    }
    private Character FindDoubledCharacters(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(x => x.doubledBy.Contains(c.GetOwner()));
    }
}
