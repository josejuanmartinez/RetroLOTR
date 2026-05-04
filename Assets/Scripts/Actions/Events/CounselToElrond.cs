using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class CounselToElrond : CharacterAction
{
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
            if (character == null || character.hex == null || character.hex.characters == null) return false;
            return character.hex.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch));
        };

        async Task<bool> counselAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();
            if (allies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied character",
                    "Ok",
                    "Cancel",
                    allies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = allies.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = allies.OrderByDescending(ch => 100 - ch.health).FirstOrDefault();
            }

            if (target == null) return false;

            int missingHealth = Mathf.Max(0, 100 - target.health);
            target.Heal(missingHealth);

            string drawText = string.Empty;
            if (missingHealth > 50)
            {
                Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
                DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
                if (game != null && deckManager != null && character.GetOwner() == game.player && deckManager.HasDeckFor(game.player))
                {
                    if (deckManager.GetHand(game.player).Count < deckManager.GetHandSize())
                    {
                        if (deckManager.TryDrawCard(game.player, out _))
                        {
                            drawText = " Drew 1 card for deep wounds.";
                        }
                    }
                }
            }

            target.ApplyStatusEffect(StatusEffectEnum.Hope, 2);

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{target.characterName} is fully healed.{drawText} Hope (2).", Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, counselAsync);
    }
}
