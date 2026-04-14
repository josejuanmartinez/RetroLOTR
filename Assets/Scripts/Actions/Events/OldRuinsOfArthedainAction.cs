using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OldRuinsOfArthedainAction : EventAction
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
                    (ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Dunedain))
                .Distinct()
                .ToList();

            for (int i = 0; i < allies.Count; i++)
            {
                allies[i].ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }

            if (revealed > 0)
            {
                owner.AddGold(1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Old Ruins of Arthedain: {revealed} hidden enemy unit(s) are uncovered, and the old stones bless nearby Hobbit/Dunedain allies with Hope.",
                new Color(0.64f, 0.64f, 0.45f));

            return revealed > 0 || allies.Count > 0;
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
