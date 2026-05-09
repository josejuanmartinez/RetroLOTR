using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheNineRideAgain : EventAction
{
    private const int ChargeDamage = 15;
    private const int ExtraMovement = 2;

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

            int chargedCount = 0;
            int enemiesDamaged = 0;

            foreach (Character nazgul in nazguls)
            {
                nazgul.moved = Math.Max(0, nazgul.moved - ExtraMovement);
                chargedCount++;

                if (nazgul.hex?.characters == null) continue;
                foreach (Character enemy in nazgul.hex.characters.Where(e => e != null && !e.killed && IsEnemy(nazgul, e)).ToList())
                {
                    enemy.Wounded(nazgul.GetOwner(), ChargeDamage);
                    enemy.Halt(1);
                    enemiesDamaged++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"The Nine Ride Again: {chargedCount} Nazgul gain {ExtraMovement} movement; {enemiesDamaged} enemy unit(s) struck for {ChargeDamage} damage.",
                Color.magenta);
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
