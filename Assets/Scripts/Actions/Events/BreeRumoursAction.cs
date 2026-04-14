using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BreeRumoursAction : EventAction
{
    private const int Radius = 2;
    private const int ReductionValue = 1;
    private const int ReductionTurns = 2;

    private static bool IsEligibleAlly(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        if (target.GetAlignment() != source.GetAlignment()) return false;
        return target.race == RacesEnum.Hobbit
            || target.race == RacesEnum.Dwarf
            || target.race == RacesEnum.Common;
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
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int hiddenEnemiesRevealed = 0;
            int scoutsHoned = 0;

            foreach (Character target in nearby)
            {
                if (target.GetAlignment() != character.GetAlignment() && target.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    target.ClearStatusEffect(StatusEffectEnum.Hidden);
                    hiddenEnemiesRevealed++;
                }

                if (!IsEligibleAlly(character, target)) continue;

                target.GrantTemporaryActionDifficultyReduction("ScoutArea", ReductionValue, ReductionTurns, target.hex);
                target.GrantTemporaryActionDifficultyReduction("FindArtifact", ReductionValue, ReductionTurns, target.hex);
                scoutsHoned++;
            }

            if (hiddenEnemiesRevealed == 0 && scoutsHoned == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Bree Rumours: {hiddenEnemiesRevealed} hidden enemy unit(s) are exposed, and {scoutsHoned} allied Hobbit/Dwarf/Human unit(s) gain sharper scouting in this area for {ReductionTurns} turns.",
                new Color(0.86f, 0.74f, 0.45f));

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
