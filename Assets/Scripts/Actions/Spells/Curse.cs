using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Curse : DarkNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => true;
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return FindEnemyCharacterTargetAtHex(c) != null;
        };
        async System.Threading.Tasks.Task<bool> curseAsync(Character caster)
        {
            if (originalEffect != null && !originalEffect(caster)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(caster)) return false;

            List<Character> enemies = FindEnemyCharactersAtHex(caster);
            if (enemies.Count < 1) return false;

            bool isAI = !caster.isPlayerControlled;
            Character enemy = null;
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
                enemy = enemies.Find(x => x.characterName == targetCharacter);
            }
            else
            {
                enemy = FindEnemyCharacterTargetAtHex(caster);
            }

            if (enemy == null) return false;

            int turns = Math.Max(1, ApplySpellEffectMultiplier(caster, 1 + Mathf.FloorToInt(caster.GetMage() / 2f)));
            enemy.ApplyStatusEffect(StatusEffectEnum.MorgulTouch, turns);

            MessageDisplayNoUI.ShowMessage(
                caster.hex,
                caster,
                $"{enemy.characterName} is afflicted with the Morgul Touch ({turns} turns).",
                Color.magenta);
            return true;
        }
        base.Initialize(c, condition, effect, curseAsync);
    }
}
