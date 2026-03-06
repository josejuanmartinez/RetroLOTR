using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BearerOfVilya : CharacterAction
{
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

            List<Character> nearbyAllies = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            int healedCount = 0;
            for (int i = 0; i < nearbyAllies.Count; i++)
            {
                int missingHealth = Mathf.Max(0, 100 - nearbyAllies[i].health);
                if (missingHealth <= 0) continue;
                nearbyAllies[i].Heal(missingHealth);
                healedCount++;
            }

            int foamRadius = Mathf.Clamp(character.GetMage(), 2, 4);
            List<Character> nazguls = character.hex.GetHexesInRadius(foamRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul)
                .Distinct()
                .ToList();

            int movedCount = 0;
            for (int i = 0; i < nazguls.Count; i++)
            {
                Character nazgul = nazguls[i];
                Leader owner = nazgul.GetOwner();
                if (owner == null) continue;

                Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
                if (capitalHex == null || capitalHex == nazgul.hex) continue;

                board.MoveCharacterOneHex(nazgul, nazgul.hex, capitalHex, true);
                movedCount++;
            }

            if (healedCount == 0 && movedCount == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Bearer of Vilya fully heals {healedCount} allied character(s) in radius 1 and drives back {movedCount} Nazgul in radius {foamRadius}.",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            bool hasWoundedAllies = character.hex.GetHexesInRadius(1)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.health < 100));
            if (hasWoundedAllies) return true;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            int foamRadius = Mathf.Clamp(character.GetMage(), 2, 4);
            return character.hex.GetHexesInRadius(foamRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul)
                .Any(n =>
                {
                    Leader owner = n.GetOwner();
                    if (owner == null) return false;
                    Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == owner && x.GetPC().isCapital);
                    return capitalHex != null && capitalHex != n.hex;
                });
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
