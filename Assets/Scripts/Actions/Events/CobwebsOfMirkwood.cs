using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CobwebsOfMirkwood : EventAction
{
    private const int Radius = 2;
    private const int PoisonTurns = 2;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

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

            List<Hex> area = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest)
                .ToList();

            int webbed = 0;
            foreach (Hex hex in area)
            {
                if (hex.characters == null) continue;
                foreach (Character enemy in hex.characters
                    .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                    .ToList())
                {
                    enemy.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    enemy.ApplyStatusEffect(StatusEffectEnum.Poisoned, PoisonTurns);
                    webbed++;
                }
            }

            int hastenedSpiders = 0;
            foreach (Character spider in character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Spider && IsAllied(character, ch))
                .Distinct())
            {
                spider.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                hastenedSpiders++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Cobwebs of Mirkwood: {webbed} enemy(ies) on forest tiles Halted and Poisoned; {hastenedSpiders} allied Spider(s) hastened.",
                Color.green);
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
