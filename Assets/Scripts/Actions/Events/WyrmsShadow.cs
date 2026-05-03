using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WyrmsShadow : EventAction
{
    private const int Radius = 2;
    private const int FearTurns = 2;
    private const int StrengthenTurns = 2;

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

            List<Character> enemyArmies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.IsArmyCommander())
                .Distinct()
                .ToList();

            List<Character> friendlyDragons = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dragon)
                .Distinct()
                .ToList();

            if (enemyArmies.Count == 0 && friendlyDragons.Count == 0) return false;

            int fortifiedRemoved = 0;
            foreach (Character enemy in enemyArmies)
            {
                if (enemy.HasStatusEffect(StatusEffectEnum.Fortified))
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Fortified);
                    fortifiedRemoved++;
                }
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, FearTurns);
            }

            foreach (Character dragon in friendlyDragons)
            {
                dragon.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Wyrm's Shadow breaks the courage of {enemyArmies.Count} enemy army commander(s) (removing Fortified from {fortifiedRemoved}) and strengthens {friendlyDragons.Count} allied Dragon(s).", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            bool hasEnemyArmy = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch) && ch.IsArmyCommander()));

            bool hasFriendlyDragon = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dragon));

            return hasEnemyArmy || hasFriendlyDragon;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
