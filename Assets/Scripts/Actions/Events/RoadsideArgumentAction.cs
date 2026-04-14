using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadsideArgumentAction : EventAction
{
    private const int Radius = 2;

    private static int GetPriority(Character target)
    {
        if (target == null) return 0;
        return target.GetCommander() + target.GetAgent() + target.GetEmmissary() + target.GetMage();
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

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int enemiesRevealed = 0;
            int enemiesDisplaced = 0;
            int hobbitsQuickened = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (target.GetAlignment() != character.GetAlignment())
                {
                    if (target.HasStatusEffect(StatusEffectEnum.Hidden))
                    {
                        target.ClearStatusEffect(StatusEffectEnum.Hidden);
                        enemiesRevealed++;
                    }
                }

                if (target.race == RacesEnum.Hobbit && target.GetAlignment() == character.GetAlignment())
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                    hobbitsQuickened++;
                }
            }

            Character strongest = nearby
                .Where(ch => ch.GetAlignment() != character.GetAlignment())
                .OrderByDescending(GetPriority)
                .ThenByDescending(ch => ch.health)
                .FirstOrDefault();

            if (strongest != null && strongest.hex != null)
            {
                List<Hex> escapeHexes = strongest.hex.GetHexesInRadius(1)
                    .Where(h => h != null && h != strongest.hex && (h.characters == null || h.characters.Count == 0))
                    .ToList();

                if (escapeHexes.Count > 0)
                {
                    Hex destination = escapeHexes[UnityEngine.Random.Range(0, escapeHexes.Count)];
                    board.MoveCharacterOneHex(strongest, strongest.hex, destination, true, false);
                    enemiesDisplaced++;
                }
            }

            if (enemiesRevealed == 0 && enemiesDisplaced == 0 && hobbitsQuickened == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Roadside Argument: {enemiesRevealed} hidden enemy unit(s) are exposed, {enemiesDisplaced} enemy unit(s) are shoved aside, and {hobbitsQuickened} Hobbit(s) gain Haste (1).",
                Color.magenta);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
