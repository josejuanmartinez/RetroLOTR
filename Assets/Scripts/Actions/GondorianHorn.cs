using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GondorianHorn : CharacterAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanOrDunedain(Character ch)
    {
        if (ch == null) return false;
        return ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain;
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

            List<Character> alliedTargets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch))
                .Distinct()
                .ToList();

            List<Character> enemyTargets = character.hex.characters == null
                ? new List<Character>()
                : character.hex.characters
                    .Where(ch => ch != null
                        && !ch.killed
                        && !IsAllied(character, ch)
                        && (ch.GetAlignment() != character.GetAlignment() || character.GetAlignment() == AlignmentEnum.neutral))
                    .Distinct()
                    .ToList();

            if (alliedTargets.Count == 0 && enemyTargets.Count == 0) return false;

            for (int i = 0; i < alliedTargets.Count; i++)
            {
                alliedTargets[i].ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            for (int i = 0; i < enemyTargets.Count; i++)
            {
                enemyTargets[i].ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Gondorian Horn grants Courage to {alliedTargets.Count} allied Human/Dunedain unit(s) in radius {Radius} and spreads Fear to {enemyTargets.Count} enemy unit(s) in the hex.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            bool hasAllies = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch)));

            bool hasEnemies = character.hex.characters != null
                && character.hex.characters.Any(ch => ch != null
                    && !ch.killed
                    && !IsAllied(character, ch)
                    && (ch.GetAlignment() != character.GetAlignment() || character.GetAlignment() == AlignmentEnum.neutral));

            return hasAllies || hasEnemies;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
