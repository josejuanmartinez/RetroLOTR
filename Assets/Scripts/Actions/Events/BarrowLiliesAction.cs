using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BarrowLiliesAction : EventAction
{
    private const int Radius = 2;
    private const int HealAmount = 10;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            int revealed = 0;
            foreach (Character enemy in enemies)
            {
                if (enemy.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
            }

            List<Character> allies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment() &&
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dwarf))
                .Distinct()
                .ToList();

            int healed = 0;
            for (int i = 0; i < allies.Count; i++)
            {
                int before = allies[i].health;
                allies[i].Heal(HealAmount);
                if (allies[i].health > before) healed++;
            }

            if (revealed > 0)
            {
                owner.AddGold(1);
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Barrow Lilies: {revealed} hidden enemy unit(s) are exposed, {healed} Hobbit/Dwarf ally unit(s) heal {HealAmount}, and the old barrow treasure yields 1 gold.",
                new Color(0.72f, 0.74f, 0.48f));

            return revealed > 0 || healed > 0;
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
