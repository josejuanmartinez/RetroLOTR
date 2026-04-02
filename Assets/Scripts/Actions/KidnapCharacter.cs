using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class KidnapCharacter : AgentCharacterAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindKidnapTarget(c) != null;
        };

        effect = (c) => true;

        async System.Threading.Tasks.Task<bool> kidnapAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(c)
                .Where(target => c.CanKidnap(target))
                .ToList();
            if (enemies.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character enemy = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask("Select enemy character to kidnap", "Ok", "Cancel", enemies.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                enemy = enemies.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                enemy = FindKidnapTarget(c);
            }

            if (enemy == null) return false;

            return c.Kidnap(enemy);
        }

        base.Initialize(c, condition, effect, kidnapAsync);
    }

    private Character FindKidnapTarget(Character c)
    {
        return FindEnemyCharactersAtHex(c)
            .Where(target => c.CanKidnap(target))
            .OrderByDescending(target => target.GetAgent())
            .ThenByDescending(target => target.GetMage())
            .ThenByDescending(target => target.GetEmmissary())
            .ThenByDescending(target => target.GetCommander())
            .FirstOrDefault();
    }
}
