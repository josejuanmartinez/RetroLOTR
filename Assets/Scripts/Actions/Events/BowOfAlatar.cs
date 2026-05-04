using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class BowOfAlatar : CharacterAction
{
    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    private static List<Character> FindEnemyTargets(Character source)
    {
        if (source == null || source.hex == null) return new List<Character>();
        return source.hex.GetHexesInRadius(1)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsEnemy(source, ch))
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return FindEnemyTargets(character).Count > 0;
        };

        async Task<bool> bowAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> enemies = FindEnemyTargets(character);
            if (enemies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string picked = await SelectionDialog.Ask(
                    "Select enemy character",
                    "Ok",
                    "Cancel",
                    enemies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(picked)) return false;
                target = enemies.FirstOrDefault(x => x.characterName == picked);
            }
            else
            {
                target = enemies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            int wound = 15;
            target.Wounded(character.GetOwner(), wound);

            string revealText = string.Empty;
            if (character.GetMage() >= 3 && target.hex != null)
            {
                target.hex.RevealArea(1, true, character.GetOwner());
                revealText = " Surroundings revealed.";
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Bow of Alatar strikes {target.characterName}: {wound} damage.{revealText}", Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, bowAsync);
    }
}
