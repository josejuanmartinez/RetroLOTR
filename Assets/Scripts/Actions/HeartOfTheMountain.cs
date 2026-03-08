using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class HeartOfTheMountain : CharacterAction
{
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

    private static bool IsMountainOrPcHex(Hex hex)
    {
        if (hex == null) return false;
        return hex.terrainType == TerrainEnum.mountains || hex.HasAnyPC();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            if (!IsMountainOrPcHex(character.hex)) return false;

            List<Character> localAllies = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .Distinct()
                .ToList();

            return localAllies.Count > 0;
        };

        async Task<bool> heartAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;
            if (!IsMountainOrPcHex(character.hex)) return false;

            List<Hex> area = character.hex.GetHexesInRadius(1)
                .Where(h => h != null)
                .ToList();

            List<Character> alliedDwarfCommanders = area
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.race == RacesEnum.Dwarf && IsAllied(character, ch))
                .Distinct()
                .ToList();

            List<Character> enemyCommanders = area
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (alliedDwarfCommanders.Count == 0) return false;

            foreach (Character ally in alliedDwarfCommanders)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                ally.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            }

            foreach (Character enemy in enemyCommanders)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Heart of the Mountain empowers {alliedDwarfCommanders.Count} allied Dwarf commander(s) and unsettles {enemyCommanders.Count} enemy commander(s).",
                Color.yellow);

            return true;
        }

        base.Initialize(c, condition, effect, heartAsync);
    }
}
