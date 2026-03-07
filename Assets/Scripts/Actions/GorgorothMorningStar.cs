using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class GorgorothMorningStar : CharacterAction
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

        async Task<bool> morningStarAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(character);
            if (enemies.Count < 1) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string targetName = await SelectionDialog.Ask(
                    "Select enemy character",
                    "Ok",
                    "Cancel",
                    enemies.Select(x => x.characterName).ToList(),
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(targetName)) return false;
                target = enemies.Find(x => x.characterName == targetName);
            }
            else
            {
                target = enemies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            const int wound = 18;
            target.Wounded(character.GetOwner(), wound);
            if (target.killed) return true;

            target.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{target.characterName} is crushed by the Gorgoroth Morning Star: {wound} damage and Fear (1).", Color.red);
            return true;
        }

        base.Initialize(c, condition, effect, morningStarAsync);
    }
}
