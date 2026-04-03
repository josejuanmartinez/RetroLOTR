using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class EncounterResolver
{
    public static async Task<bool> ResolveAsync(CardData encounterCard, Character actor)
    {
        if (encounterCard == null || actor == null || actor.killed) return false;

        await ShowHistoryAsync(encounterCard, actor);

        List<EncounterOptionData> options = BuildSelectableOptions(encounterCard);
        if (options.Count == 0) return false;

        bool isAi = !actor.isPlayerControlled;
        Sprite portrait = ResolvePortrait(encounterCard);
        string prompt = BuildPrompt(encounterCard);
        string selection = await SelectionDialog.Ask(
            prompt,
            "Choose",
            string.Empty,
            options.Select(GetOptionLabel).ToList(),
            isAi,
            portrait,
            EventIconType.Encounter);
        if (string.IsNullOrWhiteSpace(selection)) return false;

        EncounterOptionData chosenOption = options.FirstOrDefault(option => string.Equals(GetOptionLabel(option), selection, StringComparison.Ordinal));
        if (chosenOption == null) return false;

        EncounterOutcomeData outcome = ResolveOutcome(chosenOption, actor);
        if (outcome == null) return false;

        ApplyOutcome(actor, outcome);
        return true;
    }

    private static async Task ShowHistoryAsync(CardData encounterCard, Character actor)
    {
        if (string.IsNullOrWhiteSpace(encounterCard.historyText) || PopupManager.Instance == null) return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PopupManager.ShowWithIconType(
            EventIconType.Encounter,
            encounterCard.name,
            ResolvePortrait(encounterCard),
            ResolveActorPortrait(actor),
            encounterCard.historyText,
            true,
            0,
            () => tcs.TrySetResult(true));
        await tcs.Task;
    }

    private static List<EncounterOptionData> BuildSelectableOptions(CardData encounterCard)
    {
        List<EncounterOptionData> options = new();
        if (encounterCard.encounterOptions != null)
        {
            options.AddRange(encounterCard.encounterOptions.Where(option => option != null).Take(3));
        }
        if (encounterCard.fleeOption != null)
        {
            options.Add(encounterCard.fleeOption);
        }
        return options;
    }

    private static string BuildPrompt(CardData encounterCard)
    {
        return string.IsNullOrWhiteSpace(encounterCard.description)
            ? $"How will you answer {encounterCard.name}?"
            : encounterCard.description.Trim();
    }

    private static string GetOptionLabel(EncounterOptionData option)
    {
        if (option == null) return string.Empty;
        return string.IsNullOrWhiteSpace(option.description)
            ? option.label
            : $"{option.label}: {option.description}";
    }

    private static EncounterOutcomeData ResolveOutcome(EncounterOptionData option, Character actor)
    {
        if (option?.outcomes == null || actor == null) return null;
        return option.outcomes.FirstOrDefault(outcome => MeetsConditions(outcome, actor));
    }

    private static bool MeetsConditions(EncounterOutcomeData outcome, Character actor)
    {
        if (outcome == null || actor == null) return false;
        if (!string.IsNullOrWhiteSpace(outcome.requiredAlignment)
            && !string.Equals(outcome.requiredAlignment, "any", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actor.GetAlignment().ToString(), outcome.requiredAlignment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (actor.GetCommander() < Mathf.Max(0, outcome.minCommander)) return false;
        if (actor.GetAgent() < Mathf.Max(0, outcome.minAgent)) return false;
        if (actor.GetEmmissary() < Mathf.Max(0, outcome.minEmmissary)) return false;
        if (actor.GetMage() < Mathf.Max(0, outcome.minMage)) return false;
        if (actor.health < Mathf.Max(0, outcome.minHealth)) return false;
        if (outcome.maxHealth >= 0 && actor.health > outcome.maxHealth) return false;
        return true;
    }

    private static void ApplyOutcome(Character actor, EncounterOutcomeData outcome)
    {
        if (actor == null || outcome == null) return;

        Leader owner = actor.GetOwner();
        if (outcome.healthDelta > 0)
        {
            actor.Heal(outcome.healthDelta);
        }
        else if (outcome.healthDelta < 0)
        {
            actor.Wounded(null, -outcome.healthDelta);
        }

        if (owner != null)
        {
            ApplyResourceDelta(owner, outcome.goldDelta, owner.AddGold, owner.RemoveGold);
            ApplyResourceDelta(owner, outcome.leatherDelta, owner.AddLeather, owner.RemoveLeather);
            ApplyResourceDelta(owner, outcome.timberDelta, owner.AddTimber, owner.RemoveTimber);
            ApplyResourceDelta(owner, outcome.mountsDelta, owner.AddMounts, owner.RemoveMounts);
            ApplyResourceDelta(owner, outcome.ironDelta, owner.AddIron, owner.RemoveIron);
            ApplyResourceDelta(owner, outcome.steelDelta, owner.AddSteel, owner.RemoveSteel);
            ApplyResourceDelta(owner, outcome.mithrilDelta, owner.AddMithril, owner.RemoveMithril);
        }

        if (outcome.statuses != null)
        {
            foreach (EncounterStatusEffectData status in outcome.statuses)
            {
                if (status == null || string.IsNullOrWhiteSpace(status.statusId)) continue;
                if (!Enum.TryParse(status.statusId, true, out StatusEffectEnum parsed)) continue;
                actor.ApplyStatusEffect(parsed, Mathf.Max(1, status.turns));
            }
        }

        if (!string.IsNullOrWhiteSpace(outcome.resultText))
        {
            MessageDisplayNoUI.ShowMessage(actor.hex, actor, outcome.resultText, ResolveOutcomeColor(outcome));
        }
    }

    private static void ApplyResourceDelta(Leader owner, int delta, Action<int> add, Action<int, bool> remove)
    {
        if (owner == null || delta == 0) return;
        if (delta > 0)
        {
            add?.Invoke(delta);
            return;
        }
        remove?.Invoke(-delta, false);
    }

    private static Color ResolveOutcomeColor(EncounterOutcomeData outcome)
    {
        if (outcome == null) return Color.white;
        if (outcome.healthDelta < 0 || outcome.goldDelta < 0 || outcome.leatherDelta < 0 || outcome.timberDelta < 0
            || outcome.mountsDelta < 0 || outcome.ironDelta < 0 || outcome.steelDelta < 0 || outcome.mithrilDelta < 0)
        {
            return Color.red;
        }
        if (outcome.healthDelta > 0 || outcome.goldDelta > 0 || outcome.leatherDelta > 0 || outcome.timberDelta > 0
            || outcome.mountsDelta > 0 || outcome.ironDelta > 0 || outcome.steelDelta > 0 || outcome.mithrilDelta > 0)
        {
            return Color.green;
        }
        return Color.yellow;
    }

    private static Sprite ResolvePortrait(CardData encounterCard)
    {
        if (encounterCard == null) return null;
        Illustrations illustrations = UnityEngine.Object.FindFirstObjectByType<Illustrations>();
        if (illustrations == null) return null;
        string portraitName = !string.IsNullOrWhiteSpace(encounterCard.portraitName) ? encounterCard.portraitName : encounterCard.spriteName;
        if (string.IsNullOrWhiteSpace(portraitName)) portraitName = encounterCard.name;
        return illustrations.GetIllustrationByName(portraitName);
    }

    private static Sprite ResolveActorPortrait(Character actor)
    {
        Illustrations illustrations = UnityEngine.Object.FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(actor?.characterName) : null;
    }
}
