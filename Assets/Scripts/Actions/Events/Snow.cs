using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Snow : EventAction
{
    private const float FreezeChance = 0.05f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && !ch.IsImmuneToNegativeEnvironmentalCards())
            .Distinct().ToList();

        int frozen = 0;
        foreach (Character ch in allChars)
        {
            if (UnityEngine.Random.value < FreezeChance)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                frozen++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Snow (ongoing): {frozen} unit(s) frozen by the cold.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> allChars = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct()
                .ToList();

            if (allChars.Count == 0) return false;

            int frozen = 0;
            foreach (Character ch in allChars)
            {
                if (UnityEngine.Random.value < FreezeChance)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                    frozen++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Snow falls: {frozen} unit(s) frozen by the biting cold.",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
