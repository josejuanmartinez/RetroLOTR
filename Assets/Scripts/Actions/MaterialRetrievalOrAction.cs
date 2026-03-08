using System;
using System.Threading.Tasks;
using UnityEngine;

public class MaterialRetrievalOrAction : MaterialRetrieval
{
    private PendingPcChoice pendingChoice;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return await ResolvePendingChoiceAsync(character);
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }

    protected bool GrantResources(Character c, ProducesEnum first, int firstAmount, ProducesEnum second, int secondAmount, string sourceName)
    {
        if (c == null) return false;

        pendingChoice = new PendingPcChoice
        {
            sourceName = sourceName,
            first = first,
            firstAmount = firstAmount,
            second = second,
            secondAmount = secondAmount
        };
        return true;
    }

    protected override string GetDescription()
    {
        PcEffectCatalog.PcEffectDefinition definition = PcEffectCatalog.GetDefinition(GetType().Name);
        return definition != null ? definition.description : "Activate this place's local effect instead of taking its resources.";
    }

    private async Task<bool> ResolvePendingChoiceAsync(Character character)
    {
        PendingPcChoice choice = pendingChoice;
        pendingChoice = null;
        if (choice == null) return true;

        PcEffectCatalog.PcEffectDefinition definition = PcEffectCatalog.GetDefinition(GetType().Name);
        bool canUseEffect = definition != null && definition.CanExecute(character);
        bool useResources = true;

        if (canUseEffect)
        {
            if (character != null && character.isPlayerControlled)
            {
                string effectLabel = string.IsNullOrWhiteSpace(definition.title) ? "Activate the local effect" : $"Activate {definition.title}";
                string prompt =
                    $"Choose for {choice.GetLabel()}:\n" +
                    $"{choice.GetResourceSummary()}\nOR\n{effectLabel}";
                useResources = await ConfirmationDialog.Ask(prompt, "Resources", "Effect");
            }
            else
            {
                useResources = !definition.PreferEffectForAi(character);
            }
        }

        if (useResources || !canUseEffect)
        {
            return ApplyResources(character, choice);
        }

        return definition.Execute(character);
    }

    private bool ApplyResources(Character character, PendingPcChoice choice)
    {
        if (character == null) return false;
        Leader owner = character.GetOwner();
        if (owner == null) return false;

        AddResource(owner, choice.first, choice.firstAmount);
        AddResource(owner, choice.second, choice.secondAmount);
        MessageDisplayNoUI.ShowMessage(character.hex, character, $"{choice.GetLabel()}: {choice.GetResourceSummary()}", Color.yellow);
        return true;
    }

    private static void AddResource(Leader owner, ProducesEnum resource, int amount)
    {
        if (owner == null || amount <= 0) return;
        switch (resource)
        {
            case ProducesEnum.leather:
                owner.AddLeather(amount);
                break;
            case ProducesEnum.mounts:
                owner.AddMounts(amount);
                break;
            case ProducesEnum.timber:
                owner.AddTimber(amount);
                break;
            case ProducesEnum.iron:
                owner.AddIron(amount);
                break;
            case ProducesEnum.steel:
                owner.AddSteel(amount);
                break;
            case ProducesEnum.mithril:
                owner.AddMithril(amount);
                break;
            case ProducesEnum.gold:
                owner.AddGold(amount);
                break;
        }
    }

    private sealed class PendingPcChoice
    {
        public string sourceName;
        public ProducesEnum first;
        public int firstAmount;
        public ProducesEnum second;
        public int secondAmount;

        public string GetLabel()
        {
            return string.IsNullOrWhiteSpace(sourceName) ? "PC" : sourceName;
        }

        public string GetResourceSummary()
        {
            return $"+{firstAmount} <sprite name=\"{first}\"/>  +{secondAmount} <sprite name=\"{second}\"/>";
        }
    }
}
