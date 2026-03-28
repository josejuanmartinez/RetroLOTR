using System;
using UnityEngine;

public class StormWarningAtPelargirAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null) return false;

            int loyalty = UnityEngine.Random.Range(4, 9);
            pc.IncreaseLoyalty(loyalty, character);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Storm Warning at Pelargir: {pc.pcName} gains +{loyalty} loyalty.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null
                && character.hex != null
                && character.hex.GetPC() != null
                && character.hex.GetPC().loyalty < 100;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}

