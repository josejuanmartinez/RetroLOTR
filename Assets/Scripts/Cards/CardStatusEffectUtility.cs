using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class CardStatusEffectUtility
{
    private static readonly HashSet<StatusEffectEnum> NegativeStatusEffects = new()
    {
        StatusEffectEnum.Halted,
        StatusEffectEnum.RefusingDuels,
        StatusEffectEnum.Poisoned,
        StatusEffectEnum.Burning,
        StatusEffectEnum.Frozen,
        StatusEffectEnum.Blocked,
        StatusEffectEnum.Despair,
        StatusEffectEnum.Fear,
        StatusEffectEnum.MorgulTouch,
        StatusEffectEnum.Bleeding
    };

    public static bool TryGetStatusEffect(CardData card, out StatusEffectEnum status)
    {
        status = default;
        return card != null
            && !string.IsNullOrWhiteSpace(card.statusEffect)
            && Enum.TryParse(card.statusEffect.Trim(), true, out status);
    }

    public static bool HasStatusEffect(CardData card)
    {
        return TryGetStatusEffect(card, out _);
    }

    public static bool IsNegativeStatus(StatusEffectEnum status)
    {
        return NegativeStatusEffects.Contains(status);
    }

    public static int GetProcChance(CardData card)
    {
        if (card == null) return 0;
        return Mathf.Clamp(card.procChance <= 0 ? 100 : card.procChance, 0, 100);
    }

    public static string BuildCardStatusText(CardData card)
    {
        if (!TryGetStatusEffect(card, out StatusEffectEnum status)) return string.Empty;

        int chance = GetProcChance(card);
        string statusName = FormatStatusName(status);
        string spriteName = GetSpriteName(status);
        return $"{statusName} <sprite name=\"{spriteName}\">{chance}%";
    }

    public static int ApplyCardStatusEffect(CardData card, Character actor)
    {
        if (!TryGetStatusEffect(card, out StatusEffectEnum status) || actor == null || actor.hex == null) return 0;

        int chance = GetProcChance(card);
        if (chance <= 0) return 0;

        bool negative = IsNegativeStatus(status);
        List<Character> targets = actor.hex.characters
            .Where(target => IsValidStatusTarget(actor, target, negative))
            .ToList();
        if (targets.Count < 1) return 0;

        int applied = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            Character target = targets[i];
            if (UnityEngine.Random.Range(0, 100) >= chance) continue;
            target.ApplyStatusEffect(status, 1);
            applied++;
            MessageDisplayNoUI.ShowMessage(
                target.hex,
                target,
                $"{FormatStatusName(status)} applied to {target.characterName}.",
                negative ? Color.magenta : Color.green);
        }

        return applied;
    }

    private static bool IsValidStatusTarget(Character actor, Character target, bool negative)
    {
        if (actor == null || target == null || target.killed) return false;
        if (negative) return IsEnemy(actor, target);
        return IsFriendly(actor, target);
    }

    private static bool IsEnemy(Character actor, Character target)
    {
        if (target.GetOwner() == actor.GetOwner()) return false;
        AlignmentEnum actorAlignment = actor.GetAlignment();
        AlignmentEnum targetAlignment = target.GetAlignment();
        return targetAlignment == AlignmentEnum.neutral || actorAlignment == AlignmentEnum.neutral || targetAlignment != actorAlignment;
    }

    private static bool IsFriendly(Character actor, Character target)
    {
        if (target.GetOwner() == actor.GetOwner()) return true;
        AlignmentEnum actorAlignment = actor.GetAlignment();
        return actorAlignment != AlignmentEnum.neutral && target.GetAlignment() == actorAlignment;
    }

    public static string GetSpriteName(StatusEffectEnum status)
    {
        return status.ToString().ToLowerInvariant();
    }

    public static string FormatStatusName(StatusEffectEnum status)
    {
        string spaced = Regex.Replace(status.ToString(), "([a-z])([A-Z])", "$1 $2");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }
}
