using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AMorgulBlade : CharacterAction
{
    private const int ImmediateDamage = 10;
    private const int PoisonTurns = 3;
    private const int LowHPThreshold = 25;

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
                    "Strike with the Morgul Blade",
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

            target.Wounded(character.GetOwner(), ImmediateDamage);
            target.ApplyStatusEffect(StatusEffectEnum.Poisoned, PoisonTurns);

            string extraMsg = "";
            if (target.health <= LowHPThreshold)
            {
                target.ApplyStatusEffect(StatusEffectEnum.MorgulTouch, PoisonTurns);
                extraMsg = $" The wound festers: MorgulTouch ({PoisonTurns}) applied.";
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{target.characterName} suffers the Morgul Blade: {ImmediateDamage} damage and Poisoned ({PoisonTurns}).{extraMsg}",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, morgulAsync);
    }
}
