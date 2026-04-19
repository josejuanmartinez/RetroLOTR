using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CrickhollowDiversionAction : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment())
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            for (int i = 0; i < allies.Count; i++)
            {
                Character target = allies[i];
                target.moved = 0;
                target.ApplyStatusEffect(StatusEffectEnum.RefusingDuels, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Crickhollow Rest: {allies.Count} allied unit(s) in the hex rest and take up the watch again.",
                new Color(0.78f, 0.68f, 0.36f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
