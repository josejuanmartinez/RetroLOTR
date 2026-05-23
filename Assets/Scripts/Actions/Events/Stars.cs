using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Stars : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int hopeGranted = 0, revealed = 0;
        foreach (Character ch in allChars)
        {
            if (ch.race == RacesEnum.Elf)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                hopeGranted++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.darkServants
                && ch.HasStatusEffect(StatusEffectEnum.Hidden))
            {
                // Starlight pierces shadow — all hidden dark servants are revealed
                ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                revealed++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Stars (ongoing): {hopeGranted} Elves gain Hope; {revealed} hidden enemies revealed by starlight.",
            Color.cyan);
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
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> elves = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf && ch.hex != null)
                .Distinct()
                .ToList();

            if (elves.Count == 0) return false;

            int revealedCount = 0;
            int activatedCount = 0;

            foreach (Character elf in elves)
            {
                // Elves reveal hidden enemies in radius 1
                List<Hex> adjacentHexes = elf.hex.GetHexesInRadius(1);
                foreach (Hex h in adjacentHexes)
                {
                    if (h?.characters == null) continue;
                    foreach (Character enemy in h.characters.Where(e => e != null && !e.killed
                        && IsEnemy(elf, e) && e.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                    {
                        enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                        revealedCount++;
                    }
                }

                // Hidden Elves that were hidden gain an extra action
                if (elf.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    elf.hasActionedThisTurn = false;
                    activatedCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Stars: Elves reveal {revealedCount} hidden enemy unit(s); {activatedCount} Hidden Elf unit(s) may act again.",
                Color.cyan);
            return revealedCount > 0 || activatedCount > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
