using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChainsoftheLidlessEye : EventAction
{
    private const int EnemyHaltRadius = 2;

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
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> nazguls = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul && ch.hex != null)
                .Distinct()
                .ToList();

            if (nazguls.Count == 0) return false;

            int revealedCount = 0;
            int empoweredCount = 0;

            // Each Nazgul reveals hidden enemies in its hex and gains Strengthened if it revealed any
            foreach (Character nazgul in nazguls)
            {
                if (nazgul.hex?.characters == null) continue;
                bool revealed = false;
                foreach (Character enemy in nazgul.hex.characters.Where(e => e != null && !e.killed
                    && IsEnemy(nazgul, e) && e.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealedCount++;
                    revealed = true;
                }
                if (revealed)
                {
                    nazgul.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                    empoweredCount++;
                }
            }

            // Enemies in radius 2 of caster cannot become Hidden this turn (Halt proxy)
            int haltedCount = 0;
            if (character.hex != null)
            {
                foreach (Hex h in character.hex.GetHexesInRadius(EnemyHaltRadius))
                {
                    if (h?.characters == null) continue;
                    foreach (Character enemy in h.characters.Where(e => e != null && !e.killed && IsEnemy(character, e)).ToList())
                    {
                        enemy.Halt(1);
                        haltedCount++;
                    }
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Chains of the Lidless Eye: {revealedCount} hidden enemy(ies) exposed; {empoweredCount} Nazgul gain Strengthened; {haltedCount} enemy(ies) in radius {EnemyHaltRadius} halted.",
                new Color(0.6f, 0.1f, 0.8f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
