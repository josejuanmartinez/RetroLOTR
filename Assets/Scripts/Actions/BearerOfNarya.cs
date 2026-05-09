using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class BearerOfNarya : CharacterAction
{
    private const int ArtifactRadius = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        async Task<bool> naryaAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            // Heal one wounded ally (lowest health)
            Character wounded = allies.Where(ch => ch.health < 100).OrderBy(ch => ch.health).FirstOrDefault();
            bool healed = false;
            if (wounded != null)
            {
                wounded.Heal(100);
                healed = true;
            }

            // Clear Fear and Despair from all allies in radius 1
            int fearCleared = 0;
            int despairCleared = 0;
            foreach (Character ally in allies)
            {
                if (ally.HasStatusEffect(StatusEffectEnum.Fear)) { ally.ClearStatusEffect(StatusEffectEnum.Fear); fearCleared++; }
                if (ally.HasStatusEffect(StatusEffectEnum.Despair)) { ally.ClearStatusEffect(StatusEffectEnum.Despair); despairCleared++; }
            }

            // Reveal one hidden artifact site in radius
            int artifactsRevealed = 0;
            foreach (Hex h in character.hex.GetHexesInRadius(ArtifactRadius))
            {
                if (h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0)
                {
                    h.RevealArtifact();
                    artifactsRevealed++;
                    break;
                }
            }

            string healMsg = healed ? $" {wounded.characterName} fully healed." : "";
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Bearer of Narya: {fearCleared} Fear and {despairCleared} Despair cleared from allies.{healMsg} {artifactsRevealed} artifact site(s) revealed.",
                Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, naryaAsync);
    }
}
