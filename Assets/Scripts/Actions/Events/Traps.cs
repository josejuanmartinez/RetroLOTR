using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class Traps : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return FindEnemyCharactersAtHex(character).Count > 0;
        };

        async Task<bool> trapsAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(character);
            if (enemies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetName = await SelectionDialog.Ask(
                    "Select enemy character",
                    "Ok",
                    "Cancel",
                    enemies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(targetName)) return false;
                target = enemies.FirstOrDefault(x => x.characterName == targetName);
            }
            else
            {
                target = enemies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            target.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
            target.ApplyStatusEffect(StatusEffectEnum.Poisoned, 2);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{target.characterName} is caught in traps: Blocked (1) and Poisoned (2).",
                Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, trapsAsync);
    }
}
