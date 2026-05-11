using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CladInManyColours : EventAction
{
    private const int Radius = 2;
    private const int StolenBuffTurns = 2;
    private const int WoundDamage = 25;

    private static readonly StatusEffectEnum[] PositiveEffects =
    {
        StatusEffectEnum.Hope,
        StatusEffectEnum.Encouraged,
        StatusEffectEnum.Haste,
        StatusEffectEnum.ArcaneInsight,
        StatusEffectEnum.Strengthened,
        StatusEffectEnum.Fortified,
        StatusEffectEnum.DuelSupremacy,
    };

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Hex> area = character.hex.GetHexesInRadius(Radius);

            // Reveal hidden enemies — his many-coloured light strips all shadow
            int revealedCount = 0;
            foreach (Hex h in area)
            {
                if (h?.characters == null) continue;
                foreach (Character enemy in h.characters
                    .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch) && ch.HasStatusEffect(StatusEffectEnum.Hidden))
                    .ToList())
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealedCount++;
                }
            }

            List<Character> enemies = area
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Distinct()
                .ToList();

            // Wound every enemy — the fracturing light cuts as well as blinds
            foreach (Character enemy in enemies)
                enemy.Wounded(character.GetOwner(), WoundDamage);

            // Steal positive status effects from enemies and claim them for Saruman
            int stolenCount = 0;
            foreach (Character enemy in enemies)
            {
                foreach (StatusEffectEnum buff in PositiveEffects)
                {
                    if (!enemy.HasStatusEffect(buff)) continue;
                    enemy.ClearStatusEffect(buff);
                    character.ApplyStatusEffect(buff, StolenBuffTurns);
                    stolenCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Clad in Many Colours: {revealedCount} hidden enemy(ies) exposed; {enemies.Count} foe(s) wounded; {stolenCount} buff(s) consumed.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
