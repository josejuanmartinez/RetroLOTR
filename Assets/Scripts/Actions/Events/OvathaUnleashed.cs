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
        // Amplifier role (heat-side mirror of Hoarmurath Unleashed): while Ovatha walks, the desert
        // sun is merciless — Sunburnt bites harder and strikes far more often.
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null)
        {
            env.SunburntMovementExtraPenalty = 1;   // -3 movement instead of -2
            env.SunburntDamageExtraPenalty = 5;     // -15 health instead of -10
            env.SunburntEntryChanceBonus = 0.15f;   // desert-entry sunburn 5% -> 20%
        }

        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int strengthened = 0, held = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.IsArmyCommander() && IsEasterling(ch))
                {
                    Army army = ch.GetArmy();
                    bool hasLightCavalry = army != null && army.lc > 0;
                    bool inDesert = hex.terrainType == TerrainEnum.desert;
                    if (hasLightCavalry || inDesert) { ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1); strengthened++; }
                }
                else if (!IsEasterling(ch) && ch.HasStatusEffect(StatusEffectEnum.Sunburnt))
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Sunburnt, 1); held++;   // the sun keeps blistering the unprepared
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ovatha Unleashed (ongoing): {strengthened} Easterling commander(s) gain Strengthened; the sun holds {held} sunburnt enemy(ies) — Sunburnt now -3 move, -15 health.",
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
