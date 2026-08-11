using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MorgulPassage : EventAction
{
    private const int NazgulSearchRadius = 5;
    private const int StrengthenedTurns = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static Character GetTravelTarget(Character character)
    {
        if (character?.hex?.characters == null) return null;
        return character.hex.characters
            .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
            .OrderByDescending(ch => ch == character) // prefer the caster themselves as a stable default
            .ThenByDescending(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
            .FirstOrDefault();
    }

    // Nearest allied Nazgul within range — same "closest of X race" tie-break other
    // radius-search cards (ChainsoftheLidlessEye, FullMoon) use.
    private static Hex GetNearestAlliedNazgulHex(Character character, Board board)
    {
        if (character?.hex == null || board?.hexes == null) return null;
        Hex best = null;
        float bestDistance = float.MaxValue;
        foreach (Hex hex in board.hexes.Values)
        {
            if (hex?.characters == null) continue;
            if (!hex.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul && IsAllied(character, ch))) continue;

            float distance = Vector2.Distance(character.hex.v2, hex.v2);
            if (distance > NazgulSearchRadius) continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hex;
            }
        }
        return best;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;
            Board board = Board.Instance;
            return GetTravelTarget(character) != null && GetNearestAlliedNazgulHex(character, board) != null;
        };

        async System.Threading.Tasks.Task<bool> travelAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null) return false;

            Board board = Board.Instance;
            Character target = GetTravelTarget(character);
            Hex destination = GetNearestAlliedNazgulHex(character, board);
            if (target?.hex == null || destination == null || target.hex == destination) return false;

            Hex previousHex = target.hex;

            if (previousHex.characters.Contains(target)) previousHex.characters.Remove(target);
            if (target.IsArmyCommander() && previousHex.armies != null && target.GetArmy() != null && previousHex.armies.Contains(target.GetArmy()))
                previousHex.armies.Remove(target.GetArmy());
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();

            if (!destination.characters.Contains(target)) destination.characters.Add(target);
            if (target.IsArmyCommander() && destination.armies != null && target.GetArmy() != null && !destination.armies.Contains(target.GetArmy()))
                destination.armies.Add(target.GetArmy());

            target.hex = destination;
            target.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
            Character.RefreshArtifactPcVisibilityForHex(destination);

            destination.RedrawCharacters();
            destination.RedrawArmies();

            target.ApplyStatusEffect(StatusEffectEnum.Strengthened, StrengthenedTurns);

            MessageDisplayNoUI.ShowMessage(destination, target,
                $"Morgul Passage: {target.characterName} travels the dark road to an allied Nazgul's side, arriving Strengthened.",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, travelAsync);
    }
}
