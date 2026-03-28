using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WhispersBeforeTheGateAction : EventAction
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

            int hiddenAllies = 0;
            int haltedEnemies = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (IsAllied(character, target))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                    hiddenAllies++;
                }
                else
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    haltedEnemies++;
                }
            }

            if (hiddenAllies == 0 && haltedEnemies == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Whispers Before the Gate: {hiddenAllies} allied Human/Dunedain unit(s) become Hidden (1); {haltedEnemies} enemy Human/Dunedain unit(s) are Halted (1).",
                Color.gray);

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
