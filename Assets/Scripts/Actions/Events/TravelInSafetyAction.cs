using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TravelInSafetyAction : EventAction
{
    private const int Radius = 2;
    private const int HealAmount = 10;

    private static bool IsEligible(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        if (target.hex == null || target.hex == source.hex) return false;
        if (target.race != RacesEnum.Hobbit && target.race != RacesEnum.Dwarf) return false;
        return target.GetOwner() == source.GetOwner() || target.GetAlignment() == source.GetAlignment();
    }

    private static bool IsFarmRoad(Hex hex)
    {
        return hex != null && (hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands);
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
            Leader owner = character.GetOwner();
            if (board == null || owner == null) return false;

            List<Character> targets = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => IsEligible(character, ch))
                .Distinct()
                .Take(2)
                .ToList();

            if (targets.Count == 0) return false;

            int movedCount = 0;
            int healedCount = 0;
            int hopedCount = 0;

            foreach (Character target in targets)
            {
                if (target.hex == null) continue;

                Hex sourceHex = target.hex;
                board.MoveCharacterOneHex(target, target.hex, character.hex, true, false);

                movedCount++;
                int before = target.health;
                target.Heal(HealAmount);
                if (target.health > before) healedCount++;

                target.Hide(1);
                if (IsFarmRoad(sourceHex))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    hopedCount++;
                }
            }

            if (movedCount == 0) return false;

            owner.AddGold(1);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Travel in Safety: {movedCount} Hobbit/Dwarf ally unit(s) are brought into the cart's protection, {healedCount} heal {HealAmount}, and {hopedCount} gain Hope from the farm roads.",
                new Color(0.74f, 0.68f, 0.44f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => IsEligible(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
