using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HallFellSilentAction : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanLike(Character ch)
    {
        if (ch == null) return false;
        return ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain;
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

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Hex> nearbyHexes = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null)
                .Distinct()
                .ToList();

            int revealedHexes = 0;
            for (int i = 0; i < nearbyHexes.Count; i++)
            {
                Hex hex = nearbyHexes[i];
                if (!hex.IsScoutedBy(owner))
                {
                    revealedHexes++;
                }
            }

            character.hex.RevealArea(Radius, true, owner);
            owner.AddTemporarySeenHexes(nearbyHexes);
            owner.AddTemporaryScoutCenters(new[] { character.hex });

            for (int i = 0; i < nearbyHexes.Count; i++)
            {
                nearbyHexes[i]?.RefreshVisibilityRendering();
            }

            List<Character> allies = nearbyHexes
                .Where(h => h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsHumanLike(ch) && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return revealedHexes > 0;

            Character allyToReposition = allies
                .OrderByDescending(ch => ch.GetMovementLeft())
                .ThenByDescending(ch => ch.GetAgent() + ch.GetCommander() + ch.GetEmmissary() + ch.GetMage())
                .FirstOrDefault();

            if (allyToReposition != null)
            {
                allyToReposition.moved = Mathf.Max(0, allyToReposition.moved - 1);
            }

            string repositionText = allyToReposition != null
                ? $" {allyToReposition.characterName} may immediately reposition 1 step."
                : string.Empty;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Hall Fell Silent reveals the nearby danger around the court.{repositionText}",
                new Color(0.82f, 0.76f, 0.6f));

            return revealedHexes > 0 || allyToReposition != null;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            bool hasUnscoutedNearbyHex = character.hex.GetHexesInRadius(Radius).Any(h => h != null && !h.IsScoutedBy(owner));
            bool hasAlliedHumanNearby = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsHumanLike(ch) && IsAllied(character, ch)));

            return hasUnscoutedNearbyHex || hasAlliedHumanNearby;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
