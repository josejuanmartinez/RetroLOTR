using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheWitchKingsDecree : EventAction
{
    private const int AllyRadius = 2;
    private const int EnemyRadius = 1;
    private const int StrengthenedTurns = 2;
    private const int FearTurns = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
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

            int strengthened = 0;
            foreach (Character nazgul in character.hex.GetHexesInRadius(AllyRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul && IsAllied(character, ch))
                .Distinct())
            {
                nazgul.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenedTurns);
                strengthened++;
            }

            int feared = 0;
            foreach (Character enemy in character.hex.GetHexesInRadius(EnemyRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Distinct())
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, FearTurns);
                feared++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"The Witch-king's Decree: {strengthened} allied Nazgul strengthened; {feared} enemy(ies) gain Fear.",
                Color.black);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
