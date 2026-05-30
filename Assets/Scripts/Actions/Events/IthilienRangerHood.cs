using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class IthilienRangerHood : EventAction
{
    public override void ApplyOngoingEffect() { }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            int hiddenCount = 0;
            foreach (Hex hex in character.hex.GetHexesInRadius(5))
            {
                if (hex?.characters == null) continue;
                foreach (Character ch in hex.characters.Where(ch =>
                    ch != null && !ch.killed &&
                    ch.race == RacesEnum.Dunedain &&
                    ch.GetAlignment() == AlignmentEnum.freePeople).ToList())
                {
                    ch.Hide(1);
                    hiddenCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Ithilien Camp: {hiddenCount} allied Dunedain gain Hidden (1 turn) in radius 5.",
                Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetAlignment() == AlignmentEnum.freePeople;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
