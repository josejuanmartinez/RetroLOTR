using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnexpectedFireworksAction : EventAction
{
    private const int Radius = 2;
    private const int RevealRadius = 3;

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

            List<Hex> revealedHexes = character.hex.GetHexesInRadius(RevealRadius)
                .Where(h => h != null)
                .ToList();

            if (revealedHexes.Count == 0) return false;

            for (int i = 0; i < revealedHexes.Count; i++)
            {
                revealedHexes[i].RevealMapOnlyArea(0, false, false);
            }

            List<Character> hobbits = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Hobbit)
                .Distinct()
                .ToList();

            int hobbitsInspired = 0;
            for (int i = 0; i < hobbits.Count; i++)
            {
                Character target = hobbits[i];
                if (target.GetAlignment() == character.GetAlignment())
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    hobbitsInspired++;
                }
            }

            if (hobbitsInspired == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Unexpected Fireworks: {hobbitsInspired} Hobbit unit(s) gain Hope <sprite name=\"hope\"> in radius {Radius}; radius {RevealRadius} is seen for 1 turn.",
                Color.yellow);

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
