using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ReachOfBaradUngol : CharacterAction
{
    private const int Radius = 2;
    private const int ExposureDamage = 15;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
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

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsEnemy(character, ch)));
        };

        async Task<bool> reachAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int revealedCount = 0;
            int damagedCount = 0;

            foreach (Character enemy in enemies)
            {
                bool wasHidden = enemy.HasStatusEffect(StatusEffectEnum.Hidden);
                if (wasHidden)
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    enemy.Wounded(character.GetOwner(), ExposureDamage);
                    enemy.hasActionedThisTurn = true;
                    revealedCount++;
                    damagedCount++;
                }
            }

            if (revealedCount == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Reach of Barad-Ungol: {revealedCount} hidden enemy(ies) exposed; {damagedCount} take {ExposureDamage} damage and lose their action.",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, reachAsync);
    }
}
