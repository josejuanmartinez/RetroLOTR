using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheNecromancersWhisper : EventAction
{
    private const int RevealRadius = 2;
    private const int FallbackRadius = 1;
    private const int DespairTurns = 2;
    private const int FearTurns = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> hiddenEnemies = character.hex.GetHexesInRadius(RevealRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.IsHidden())
                .Distinct()
                .ToList();

            if (hiddenEnemies.Count > 0)
            {
                foreach (Character enemy in hiddenEnemies)
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    enemy.ApplyStatusEffect(StatusEffectEnum.Despair, DespairTurns);
                }

                MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Necromancer's Whisper reveals {hiddenEnemies.Count} hidden enemy unit(s) and fills them with Despair ({DespairTurns} turns).", Color.red);
                return true;
            }

            List<Character> visibleEnemies = character.hex.GetHexesInRadius(FallbackRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (visibleEnemies.Count == 0) return false;

            foreach (Character enemy in visibleEnemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, FearTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Necromancer's Whisper instills Fear ({FearTurns} turn) in {visibleEnemies.Count} enemy unit(s).", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(RevealRadius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
