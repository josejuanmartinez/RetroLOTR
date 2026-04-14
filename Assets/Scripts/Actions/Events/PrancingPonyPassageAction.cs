using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PrancingPonyPassageAction : EventAction
{
    private const int Radius = 1;
    private const int HealAmount = 10;
    private const int GoldReward = 1;

    private static bool IsEligible(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        if (target.hex == null || target.hex == source.hex) return false;
        if (target.race != RacesEnum.Hobbit && target.race != RacesEnum.Dwarf) return false;
        return target.GetOwner() == source.GetOwner() || target.GetAlignment() == source.GetAlignment();
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

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target.hex == null) continue;
                board.MoveCharacterOneHex(target, target.hex, character.hex, true, false);
                movedCount++;
                int before = target.health;
                target.Heal(HealAmount);
                if (target.health > before) healedCount++;
            }

            if (movedCount == 0) return false;

            owner.AddGold(GoldReward);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Prancing Pony Passage: {movedCount} Hobbit/Dwarf ally unit(s) are escorted into the hex, {healedCount} are healed for {HealAmount}, and {owner.characterName} gains {GoldReward} gold.",
                new Color(0.82f, 0.7f, 0.4f));

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
