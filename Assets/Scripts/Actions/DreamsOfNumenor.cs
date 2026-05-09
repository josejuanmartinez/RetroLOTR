using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DreamsOfNumenor : CharacterAction
{
    private const int RevealRadius = 4;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsSeaAdjacent(Hex hex) =>
        hex != null && (hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain());

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

            // Reveal all coastal hexes in radius
            List<Hex> coastalHexes = board.GetHexes()
                .Where(h => h != null && IsSeaAdjacent(h))
                .ToList();

            int revealedCount = 0;
            foreach (Hex h in coastalHexes)
            {
                h.RevealArea(1, false, character.GetOwner());
                revealedCount++;
            }

            // Allied naval commanders gain +1 Warship
            List<Character> navalCommanders = board.GetHexes()
                .Where(h => h != null && IsSeaAdjacent(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander()
                    && IsAllied(character, ch) && ch.GetArmy() != null
                    && (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain))
                .Distinct()
                .ToList();

            foreach (Character commander in navalCommanders)
                commander.GetArmy().ws++;

            // Allied characters on shore tiles gain Haste (ready to embark)
            int embarkedCount = 0;
            List<Character> shoreAllies = board.GetHexes()
                .Where(h => h != null && h.terrainType == TerrainEnum.shore && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            foreach (Character ally in shoreAllies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                embarkedCount++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Dreams of Númenor: {revealedCount} coastal hex(es) revealed; {navalCommanders.Count} commander(s) gain +1 Warship; {embarkedCount} shore ally(ies) gain Haste.",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && IsSeaAdjacent(h) && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
