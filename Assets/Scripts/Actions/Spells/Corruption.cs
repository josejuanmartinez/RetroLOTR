using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Corruption : DarkNeutralSpell
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

        async System.Threading.Tasks.Task<bool> corruptionAsync(Character caster)
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

            int baseDamage = UnityEngine.Random.Range(5, 16) * Math.Max(1, caster.GetMage());
            int damage = Math.Max(1, ApplySpellEffectMultiplier(caster, baseDamage));
            target.Wounded(caster.GetOwner(), damage);
            target.ApplyStatusEffect(StatusEffectEnum.Fear, 2);

            MessageDisplayNoUI.ShowMessage(
                caster.hex,
                caster,
                $"{target.characterName} is corrupted: {damage} damage and Fear (2).",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, corruptionAsync);
    }
}
