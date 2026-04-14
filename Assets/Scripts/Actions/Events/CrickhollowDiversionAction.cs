using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CrickhollowDiversionAction : EventAction
{
    private const int Radius = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int revealed = 0;
            foreach (Character target in enemies)
            {
                if (target.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    target.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
                target.ApplyStatusEffect(StatusEffectEnum.RefusingDuels, 1);
            }

            Character strongest = enemies
                .OrderByDescending(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
                .ThenByDescending(ch => ch.health)
                .FirstOrDefault();

            if (strongest != null)
            {
                strongest.ApplyStatusEffect(StatusEffectEnum.Blocked, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Crickhollow Diversion: {revealed} hidden enemy unit(s) are exposed, enemy unit(s) in radius {Radius} refuse duels, and the strongest is Blocked (1).",
                new Color(0.78f, 0.68f, 0.36f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
