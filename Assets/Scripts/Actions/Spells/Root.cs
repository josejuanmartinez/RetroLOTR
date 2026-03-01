using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Root : Spell
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
            return FindEnemyCharacterTargetAtHex(c) != null;
        };

        async System.Threading.Tasks.Task<bool> rootAsync(Character caster)
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

            int turns = Mathf.Clamp(ApplySpellEffectMultiplier(caster, 1 + Mathf.FloorToInt(caster.GetMage() / 2f)), 1, 3);
            target.ApplyStatusEffect(StatusEffectEnum.Blocked, turns);
            MessageDisplayNoUI.ShowMessage(caster.hex, caster, $"{target.characterName} is rooted and blocked for {turns} turn(s).", Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, rootAsync);
    }
}
