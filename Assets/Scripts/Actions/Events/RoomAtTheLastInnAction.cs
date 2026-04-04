using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomAtTheLastInnAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> everyone = character.hex.characters
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (everyone.Count == 0) return false;

            int healedCount = 0;
            for (int i = 0; i < everyone.Count; i++)
            {
                Character target = everyone[i];
                int before = target.health;
                target.Heal(15);
                if (target.health > before) healedCount++;
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Room at the Last Inn: all characters in this hex recover 15 health.",
                new Color(0.4f, 0.75f, 0.4f));

            return healedCount > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.health < 100);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
