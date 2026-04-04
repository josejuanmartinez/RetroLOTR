using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShutteredWatchfiresAction : EventAction
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

            int fortified = 0;
            int concealed = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (!IsAllied(character, target)) continue;

                target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                fortified++;

                if (!target.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                    concealed++;
                }
            }

            if (fortified == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Shuttered Watchfires: {fortified} allied Human/Dunedain unit(s) gain Fortified (1); {concealed} also become Hidden (1).",
                new Color(0.75f, 0.72f, 0.55f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsHumanLike(ch) && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
