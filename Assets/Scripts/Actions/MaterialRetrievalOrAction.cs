using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class MaterialRetrievalOrAction : MaterialRetrieval
{
    private const int FoundingGoldCost = 10;
    private PendingPcChoice pendingChoice;
    private static Dictionary<string, PcCardMetadata> pcCardMetadataByActionRef;
    private static Dictionary<string, string> pcNamesBySourceKey;

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
            if (character == null) return false;
            return HasAssociatedPcInGame(null) || CanFoundAssociatedPc(character);
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
        PcEffectCatalog.PcEffectDefinition definition = ResolvePcEffectDefinition(null);
        string effectDescription = definition != null
            ? definition.description
            : "Activate this place's local effect instead of taking its resources.";
        return $"{effectDescription} If this PC is not in play, found it in your hex instead for 10 gold (Commander 2 or Emmissary 1, no PC there, no enemy armies there).";
    }

    private async Task<bool> ResolvePendingChoiceAsync(Character character)
    {
        PendingPcChoice choice = pendingChoice;
        pendingChoice = null;
        if (choice == null || character == null) return false;

        if (!HasAssociatedPcInGame(choice))
        {
            return TryFoundAssociatedPc(character, choice);
        }

        PcEffectCatalog.PcEffectDefinition definition = ResolvePcEffectDefinition(choice);
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

    private bool TryFoundAssociatedPc(Character character, PendingPcChoice choice)
    {
        if (!CanFoundAssociatedPc(character)) return false;

        Leader owner = character.GetOwner();
        if (owner == null || character.hex == null) return false;

        owner.RemoveGold(FoundingGoldCost);

        string pcName = ResolveAssociatedPcName(choice);
        PC foundedPc = new(owner, pcName, PCSizeEnum.camp, FortSizeEnum.NONE, false, false, character.hex, false, 75);
        character.hex.RedrawPC();
        MessageDisplayNoUI.ShowMessage(character.hex, character, $"{pcName} is founded.", Color.green);
        return foundedPc != null;
    }

    private bool CanFoundAssociatedPc(Character character)
    {
        if (character == null || character.killed || character.hex == null) return false;

        Leader owner = character.GetOwner();
        if (owner == null || owner.goldAmount < FoundingGoldCost) return false;
        if (character.hex.HasAnyPC()) return false;
        if (character.GetCommander() < 2 && character.GetEmmissary() < 1) return false;

        List<Army> armies = character.hex.armies;
        if (armies == null) return true;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null || army.killed || army.commander == null || army.commander.killed) continue;
            if (IsEnemyArmy(character, army)) return false;
        }

        return true;
    }

    private static bool IsEnemyArmy(Character character, Army army)
    {
        if (character == null || army == null || army.commander == null) return false;
        if (army.commander.GetOwner() == character.GetOwner()) return false;

        AlignmentEnum sourceAlignment = character.GetAlignment();
        AlignmentEnum targetAlignment = army.GetAlignment();
        if (targetAlignment == AlignmentEnum.neutral) return true;
        if (sourceAlignment == AlignmentEnum.neutral) return true;
        return targetAlignment != sourceAlignment;
    }

    private bool HasAssociatedPcInGame(PendingPcChoice choice)
    {
        return FindAssociatedPc(choice) != null;
    }

    private PcEffectCatalog.PcEffectDefinition ResolvePcEffectDefinition(PendingPcChoice choice)
    {
        string effectId = ResolveAssociatedPcEffectId(choice);
        return PcEffectCatalog.GetDefinition(effectId);
    }

    private PC FindAssociatedPc(PendingPcChoice choice)
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return null;

        string targetKey = NormalizePcLookupKey(ResolveAssociatedPcName(choice));
        if (string.IsNullOrWhiteSpace(targetKey)) return null;

        List<Hex> hexes = board.GetHexes();
        if (hexes == null) return null;

        for (int i = 0; i < hexes.Count; i++)
        {
            PC candidate = hexes[i]?.GetPCData();
            if (candidate == null) continue;
            if (NormalizePcLookupKey(candidate.pcName) == targetKey) return candidate;
        }

        return null;
    }

    private string ResolveAssociatedPcName(PendingPcChoice choice)
    {
        EnsurePcCardLookupLoaded();

        string actionKey = NormalizePcLookupKey(GetType().Name);
        if (!string.IsNullOrWhiteSpace(actionKey)
            && pcCardMetadataByActionRef.TryGetValue(actionKey, out PcCardMetadata metadataByAction)
            && !string.IsNullOrWhiteSpace(metadataByAction.pcName))
        {
            return metadataByAction.pcName;
        }

        string sourceKey = NormalizePcLookupKey(choice != null ? choice.sourceName : null);
        if (!string.IsNullOrWhiteSpace(sourceKey) && pcNamesBySourceKey.TryGetValue(sourceKey, out string nameBySource))
        {
            return nameBySource;
        }

        string fallback = choice != null ? choice.GetLabel() : actionName;
        return HumanizeSourceName(fallback);
    }

    private string ResolveAssociatedPcEffectId(PendingPcChoice choice)
    {
        EnsurePcCardLookupLoaded();

        string actionKey = NormalizePcLookupKey(GetType().Name);
        if (!string.IsNullOrWhiteSpace(actionKey)
            && pcCardMetadataByActionRef.TryGetValue(actionKey, out PcCardMetadata metadataByAction))
        {
            return metadataByAction.pcEffectId;
        }

        return null;
    }

    private static void EnsurePcCardLookupLoaded()
    {
        if (pcCardMetadataByActionRef != null && pcNamesBySourceKey != null) return;

        pcCardMetadataByActionRef = new Dictionary<string, PcCardMetadata>(StringComparer.OrdinalIgnoreCase);
        pcNamesBySourceKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TextAsset manifestAsset = Resources.Load<TextAsset>("Cards");
        if (manifestAsset == null) return;

        CardsManifest manifest = JsonUtility.FromJson<CardsManifest>(manifestAsset.text);
        if (manifest?.decks == null) return;

        for (int i = 0; i < manifest.decks.Count; i++)
        {
            DeckManifestEntry entry = manifest.decks[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.resourcePath)) continue;

            TextAsset deckAsset = Resources.Load<TextAsset>(entry.resourcePath);
            if (deckAsset == null) continue;

            DeckData deck = JsonUtility.FromJson<DeckData>(deckAsset.text);
            if (deck?.cards == null) continue;

            for (int j = 0; j < deck.cards.Count; j++)
            {
                CardData card = deck.cards[j];
                if (card == null || card.GetCardType() != CardTypeEnum.PC || string.IsNullOrWhiteSpace(card.name)) continue;

                string actionKey = NormalizePcLookupKey(card.GetActionRef());
                if (!string.IsNullOrWhiteSpace(actionKey) && !pcCardMetadataByActionRef.ContainsKey(actionKey))
                {
                    pcCardMetadataByActionRef[actionKey] = new PcCardMetadata
                    {
                        pcName = card.name,
                        pcEffectId = card.pcEffectId
                    };
                }

                if (!string.IsNullOrWhiteSpace(card.spriteName))
                {
                    string sourceKey = NormalizePcLookupKey(card.spriteName);
                    if (!string.IsNullOrWhiteSpace(sourceKey) && !pcNamesBySourceKey.ContainsKey(sourceKey))
                    {
                        pcNamesBySourceKey[sourceKey] = card.name;
                    }
                }

                string fallbackKey = NormalizePcLookupKey(card.name);
                if (!string.IsNullOrWhiteSpace(fallbackKey) && !pcNamesBySourceKey.ContainsKey(fallbackKey))
                {
                    pcNamesBySourceKey[fallbackKey] = card.name;
                }
            }
        }
    }

    private static string NormalizePcLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        char[] chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }

    private static string HumanizeSourceName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return "PC";

        List<char> chars = new(sourceName.Length + 4);
        for (int i = 0; i < sourceName.Length; i++)
        {
            char current = sourceName[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(sourceName[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }
        return new string(chars.ToArray());
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

    private sealed class PcCardMetadata
    {
        public string pcName;
        public string pcEffectId;
    }
}
