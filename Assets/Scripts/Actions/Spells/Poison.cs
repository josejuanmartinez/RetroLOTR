using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Poison : DarkSpell
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (caster) => true;
        condition = (caster) =>
        {
            if (originalCondition != null && !originalCondition(caster)) return false;
            return FindEnemyCharacterTargetAtHex(caster) != null;
        };

        async System.Threading.Tasks.Task<bool> poisonAsync(Character caster)
        {
            if (originalEffect != null && !originalEffect(caster)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(caster)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(caster);
            if (enemies.Count < 1) return false;

            bool isAI = !caster.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string targetCharacter = await SelectionDialog.Ask(
                    "Select enemy character",
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
                target = FindEnemyCharacterTargetAtHex(caster);
            }

            if (target == null) return false;

            const int turns = 2;
            target.ApplyStatusEffect(StatusEffectEnum.Poisoned, turns);
            MessageDisplayNoUI.ShowMessage(caster.hex, caster, $"{target.characterName} is poisoned ({turns}).", Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, poisonAsync);
    }
}
