using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BarrowChillAction : EventAction
{
    private const int Radius = 2;
    private const float FreezeChance = 0.10f;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int frozen = 0, hobbitsBlocked = 0, undeadHasted = 0;
        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isBarrowTerrain = hex.terrainType == TerrainEnum.swamp
                || hex.terrainType == TerrainEnum.mountains
                || hex.terrainType == TerrainEnum.hills;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.race == RacesEnum.Undead && isBarrowTerrain)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                    undeadHasted++;
                }
                else if (ch.race == RacesEnum.Hobbit && UnityEngine.Random.value < 0.33f)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
                    hobbitsBlocked++;
                }
                else if (ch.race != RacesEnum.Undead && !ch.IsImmuneToNegativeEnvironmentalCards() && UnityEngine.Random.value < FreezeChance)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                    frozen++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Barrow Chill (ongoing): {frozen} units frozen by barrow cold; {hobbitsBlocked} Hobbits blocked; {undeadHasted} Undead in barrow terrain hasted.",
            Color.cyan);
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

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int feared = 0;
            foreach (Character target in enemies)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                feared++;
            }

            Character weakest = enemies
                .OrderBy(ch => ch.health)
                .ThenBy(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
                .FirstOrDefault();

            if (weakest != null)
            {
                weakest.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Barrow Chill: {feared} enemy unit(s) gain Frozen (1), and the weakest enemy is Blocked (1).",
                Color.gray);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
