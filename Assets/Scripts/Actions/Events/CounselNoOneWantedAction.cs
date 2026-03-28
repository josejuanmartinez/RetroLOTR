using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CounselNoOneWantedAction : EventAction
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

    private static bool IsHumanLike(Character ch)
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

            List<Character> nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsHumanLike(ch))
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int heartenedAllies = 0;
            int burdenedEnemies = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (IsAllied(character, target))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    heartenedAllies++;
                }
                else
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    burdenedEnemies++;
                }
            }

            if (heartenedAllies == 0 && burdenedEnemies == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Counsel No One Wanted: {heartenedAllies} allied Human/Dunedain unit(s) gain Hope (1); {burdenedEnemies} enemy Human/Dunedain unit(s) gain Despair (1).",
                Color.white);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsHumanLike(ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
