using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeastsBiddenToServe : EventAction
{
    private const int Radius = 3;
    private const int StrengthenTurns = 2;
    private const int HasteTurns = 2;

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

            List<Character> beasts = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Beast && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (beasts.Count == 0) return false;

            foreach (Character beast in beasts)
            {
                beast.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenTurns);
                beast.ApplyStatusEffect(StatusEffectEnum.Haste, HasteTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Beasts Bidden to Serve strengthen {beasts.Count} allied beast(s) for {StrengthenTurns} turns.", Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Beast && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
