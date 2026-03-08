using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class JoiningTheRangers : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
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

            List<Character> nearbyDunedain = character.hex.GetHexesInRadius(2)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dunedain)
                .Distinct()
                .ToList();

            return nearbyDunedain.Count > 0;
        };

        async Task<bool> rangerAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Hex> area = character.hex.GetHexesInRadius(2);
            List<Character> nearbyDunedain = area
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dunedain)
                .Distinct()
                .ToList();

            if (nearbyDunedain.Count == 0) return false;

            foreach (Character ally in nearbyDunedain)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            Leader owner = character.GetOwner();
            character.hex.RevealArea(2, true, owner);
            owner?.AddTemporarySeenHexes(area);
            owner?.AddTemporaryScoutCenters(new[] { character.hex });
            for (int i = 0; i < area.Count; i++)
            {
                area[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Joining the Rangers grants Courage (1) to {nearbyDunedain.Count} allied Dunedain and scouts radius 2.", Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, rangerAsync);
    }
}
