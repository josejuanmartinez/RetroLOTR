using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldForestTurnaboutAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) => true;

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0 || allies.Count == 0) return false;

            int enemyStops = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                Character enemy = enemies[i];
                int previousMovementLeft = enemy.GetMovementLeft();
                enemy.moved = enemy.GetMaxMovement();
                if (previousMovementLeft > 0) enemyStops++;
            }

            bool isAI = !character.isPlayerControlled;
            Character allyTarget = null;
            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Choose ally guided by the Old Forest",
                    "Guide",
                    "Cancel",
                    allies.Select(x => x.characterName).ToList(),
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrEmpty(selected)) return false;
                allyTarget = allies.Find(x => x.characterName == selected);
            }
            else
            {
                allyTarget = allies
                    .OrderByDescending(ch => ch.GetMovementLeft())
                    .ThenByDescending(ch => ch.GetAgent() + ch.GetCommander() + ch.GetEmmissary() + ch.GetMage())
                    .FirstOrDefault();
            }

            if (allyTarget == null) return false;
            allyTarget.ApplyStatusEffect(StatusEffectEnum.Haste, 1);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Old Forest Turnabout: {enemyStops} enemy unit(s) lose the rest of their movement, and {allyTarget.characterName} gains Haste (1).",
                new Color(0.35f, 0.65f, 0.35f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            bool hasEnemy = character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment());
            bool hasAlly = character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment());
            return hasEnemy && hasAlly;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
