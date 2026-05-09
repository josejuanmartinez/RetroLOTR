using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GondorianHorn : CharacterAction
{
    private const int Radius = 3;
    private const int AlliedRadius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanOrDunedain(Character ch) =>
        ch != null && (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain);

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            // Reveal hidden enemies in radius 3
            int revealedCount = 0;
            List<Hex> nearbyHexes = character.hex.GetHexesInRadius(Radius);
            foreach (Hex h in nearbyHexes)
            {
                if (h?.characters == null) continue;
                foreach (Character enemy in h.characters.Where(ch => ch != null && !ch.killed
                    && !IsAllied(character, ch) && ch.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealedCount++;
                }
            }

            // Allied Human/Dunedain in radius 2 gain extra movement + Encouraged
            List<Character> alliedTargets = character.hex.GetHexesInRadius(AlliedRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch))
                .Distinct()
                .ToList();

            foreach (Character ally in alliedTargets)
            {
                ally.moved = Math.Max(0, ally.moved - 1);
                ally.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            // Enemies in caster's hex cannot leave (Halt)
            int haltedCount = 0;
            if (character.hex.characters != null)
            {
                foreach (Character enemy in character.hex.characters.Where(ch => ch != null && !ch.killed && !IsAllied(character, ch)).ToList())
                {
                    enemy.Halt(1);
                    haltedCount++;
                }
            }

            if (revealedCount == 0 && alliedTargets.Count == 0 && haltedCount == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Gondorian Horn: {revealedCount} hidden enemy(ies) revealed; {alliedTargets.Count} Human/Dunedain ally(ies) gain Courage and extra movement; {haltedCount} enemy(ies) in hex halted.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            bool hasHiddenEnemies = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && !IsAllied(character, ch)));

            bool hasAllies = character.hex.GetHexesInRadius(AlliedRadius)
                .Any(h => h != null && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch) && IsHumanOrDunedain(ch)));

            return hasHiddenEnemies || hasAllies;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
