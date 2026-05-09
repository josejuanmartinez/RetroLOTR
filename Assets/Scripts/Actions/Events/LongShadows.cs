using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LongShadows : EventAction
{
    private const int Radius = 4;
    private const int ForestScoutRadius = 3;

    private static bool IsBeastRace(RacesEnum race) =>
        race == RacesEnum.Troll || race == RacesEnum.Goblin || race == RacesEnum.Spider
        || race == RacesEnum.Dragon || race == RacesEnum.Undead || race == RacesEnum.Beast;

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

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> beasts = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsBeastRace(ch.race) && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (beasts.Count == 0) return false;

            // Reset movement for allied beasts so they can move again this turn
            foreach (Character beast in beasts)
            {
                beast.moved = 0;
                beast.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            // Enemy army commanders on forest tiles in radius 3 lose scouting
            Leader casterOwner = character.GetOwner();
            List<Hex> nearbyForestHexes = character.hex.GetHexesInRadius(ForestScoutRadius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest)
                .ToList();

            int obscuredCount = 0;
            foreach (Hex fh in nearbyForestHexes)
            {
                if (fh.armies != null && fh.armies.Any(a => a != null && !a.killed
                    && a.GetCommander() != null && IsAllied(character, a.GetCommander()) == false))
                {
                    fh.Obscure();
                    obscuredCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Long Shadows: {beasts.Count} beast(s) reset movement and gain Encouraged; {obscuredCount} enemy forest position(s) obscured.",
                Color.gray);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && IsBeastRace(ch.race) && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
