using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OvathaUnleashed : EventAction
{
    private static bool IsEasterling(Character ch) =>
        ch != null && ch.race == RacesEnum.Easterling;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int strengthened = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsEasterling(ch)).ToList())
            {
                Army army = ch.GetArmy();
                bool hasLightCavalry = army != null && army.lc > 0;
                bool inDesert = hex.terrainType == TerrainEnum.desert;

                if (!hasLightCavalry && !inDesert) continue;

                ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                strengthened++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ovatha Unleashed (ongoing): {strengthened} Easterling commander(s) with light cavalry or in deserts gain Strengthened.",
            Color.red);
    }

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

            List<Character> easterlings = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEasterling(ch))
                .Distinct()
                .ToList();

            int boosted = 0;
            foreach (Character ch in easterlings)
            {
                ch.AddCommander(1);
                boosted++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Ovatha Unleashed: {boosted} Easterling character(s) gain +1 Commander.",
                Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && IsEasterling(ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
