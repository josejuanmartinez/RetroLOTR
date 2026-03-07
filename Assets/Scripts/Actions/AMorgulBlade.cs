using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AMorgulBlade : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.race != RacesEnum.Nazgul) return false;
            return FindEnemyCharactersAtHex(character).Any(x => x != null && x is not Leader);
        };

        async Task<bool> morgulAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(character)
                .Where(x => x != null && x is not Leader)
                .ToList();
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
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);

                if (string.IsNullOrWhiteSpace(targetName)) return false;
                target = enemies.Find(x => x.characterName == targetName);
            }
            else
            {
                target = enemies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            if (UnityEngine.Random.Range(0, 100) >= 50)
            {
                MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Morgul wound fails to take hold on {target.characterName}.", Color.red);
                return false;
            }

            target.ApplyStatusEffect(StatusEffectEnum.MorgulTouch, 7);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{target.characterName} suffers Morgul Touch.", Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, morgulAsync);
    }
}
