using System;
using System.Collections.Generic;
using System.Linq;

public class Possession : DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) => true;
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharactersAtHex(c).Any(t => c.CanPossess(t));
        };

        async System.Threading.Tasks.Task<bool> possessionAsync(Character caster)
        {
            if (originalEffect != null && !originalEffect(caster)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(caster)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(caster)
                .Where(t => caster.CanPossess(t))
                .ToList();
            if (enemies.Count < 1) return false;

            bool isAI = !caster.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask(
                    "Select enemy character to possess",
                    "Ok",
                    "Cancel",
                    enemies.Select(x => x.characterName).ToList(),
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(caster) : null);
                if (string.IsNullOrEmpty(targetCharacter)) return false;
                target = enemies.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                target = enemies.OrderByDescending(t => t.GetMage())
                                .ThenByDescending(t => t.GetAgent())
                                .First();
            }

            if (target == null) return false;

            return caster.Possess(target);
        }

        base.Initialize(c, condition, effect, possessionAsync);
    }
}
