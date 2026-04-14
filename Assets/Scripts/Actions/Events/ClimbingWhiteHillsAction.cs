using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ClimbingWhiteHillsAction : EventAction
{
    private const int Radius = 3;

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
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            List<Hex> area = character.hex.GetHexesInRadius(Radius);
            owner.AddTemporarySeenHexes(area);
            owner.AddTemporaryScoutCenters(new[] { character.hex });
            character.hex.RevealArea(Radius, true, owner);

            List<Character> allies = area
                .Where(h => h != null && h.terrainType == TerrainEnum.hills && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return true;

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                allies[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Climbing the White Hills: the view opens wide, and {allies.Count} Hobbit/Dwarf unit(s) on the hills gain Hope and Haste.",
                new Color(0.78f, 0.8f, 0.58f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return true;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
