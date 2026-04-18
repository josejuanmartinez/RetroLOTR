using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class MaterialRetrievalOrAction : MaterialRetrieval
{
    private const int FoundingGoldCost = 10;
    private static int activePcLookupFrame = -1;
    private static readonly HashSet<string> activePcLookupKeys = new(StringComparer.OrdinalIgnoreCase);
    protected override bool GrantsResourcesImmediately => false;

    public override void Initialize(Character c, CardData card = null, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
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
            return HasAssociatedPcInGame() || CanFoundAssociatedPc(character);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return await ResolvePendingChoiceAsync(character);
        };

        base.Initialize(c, card, condition, effect, asyncEffect);
    }

    protected override string GetDescription()
    {
        PcEffectCatalog.PcEffectDefinition definition = ResolvePcEffectDefinition();
        string effectDescription = definition != null
            ? definition.description
            : "Activate this place's local effect instead of taking its resources.";
        return $"{effectDescription} If this PC is not in play, found it in your hex instead for 10 gold (Commander 2 or Emmissary 1, no PC there, no enemy armies there).";
    }

    private async Task<bool> ResolvePendingChoiceAsync(Character character)
    {
        if (character == null) return false;

        if (!HasAssociatedPcInGame())
        {
            return TryFoundAssociatedPc(character);
        }

        PcEffectCatalog.PcEffectDefinition definition = ResolvePcEffectDefinition();
        bool canUseEffect = definition != null && definition.CanExecute(character);
        Leader owner = character.GetOwner();
        bool alreadyPlayedLandThisTurn = owner != null && owner.HasPlayedLandThisTurn();
        bool useResources = true;

        if (alreadyPlayedLandThisTurn && !canUseEffect)
        {
            MessageDisplayNoUI.ShowMessage(character.hex, character, "Only one land card can be played each turn.", Color.red);
            return false;
        }

        if (canUseEffect)
        {
            if (alreadyPlayedLandThisTurn)
            {
                useResources = false;
            }
            else if (character.isPlayerControlled)
            {
                string effectLabel = string.IsNullOrWhiteSpace(definition.title) ? "Activate the local effect" : $"Activate {definition.title}";
                string prompt =
                    $"Choose for {ResolveAssociatedPcName()}:\n" +
                    $"{GetResourceSummary()}\nOR\n{effectLabel}";
                useResources = await ConfirmationDialog.Ask(prompt, "Resources", "Effect");
            }
            else
            {
                useResources = !definition.PreferEffectForAi(character);
            }
        }

        if (useResources || !canUseEffect)
        {
            return ApplyResources(character);
        }

        return definition.Execute(character);
    }

    private string GetResourceSummary()
    {
        if (card == null) return "Resources";
        List<string> parts = new();
        if (card.leatherGranted > 0) parts.Add($"+{card.leatherGranted} <sprite name=\"leather\"/>");
        if (card.mountsGranted > 0) parts.Add($"+{card.mountsGranted} <sprite name=\"mounts\"/>");
        if (card.timberGranted > 0) parts.Add($"+{card.timberGranted} <sprite name=\"timber\"/>");
        if (card.ironGranted > 0) parts.Add($"+{card.ironGranted} <sprite name=\"iron\"/>");
        if (card.steelGranted > 0) parts.Add($"+{card.steelGranted} <sprite name=\"steel\"/>");
        if (card.mithrilGranted > 0) parts.Add($"+{card.mithrilGranted} <sprite name=\"mithril\"/>");
        if (card.goldGranted > 0) parts.Add($"+{card.goldGranted} <sprite name=\"gold\"/>");
        return string.Join("  ", parts);
    }

    private bool ApplyResources(Character character)
    {
        if (character == null || card == null) return false;
        Leader owner = character.GetOwner();
        if (owner == null) return false;

        owner.RecordPlayedLandThisTurn();
        if (card.leatherGranted > 0) owner.AddLeather(card.leatherGranted, false);
        if (card.mountsGranted > 0) owner.AddMounts(card.mountsGranted, false);
        if (card.timberGranted > 0) owner.AddTimber(card.timberGranted, false);
        if (card.ironGranted > 0) owner.AddIron(card.ironGranted, false);
        if (card.steelGranted > 0) owner.AddSteel(card.steelGranted, false);
        if (card.mithrilGranted > 0) owner.AddMithril(card.mithrilGranted, false);
        if (card.goldGranted > 0) owner.AddGold(card.goldGranted, false);

        MessageDisplayNoUI.ShowMessage(character.hex, character, $"{ResolveAssociatedPcName()}: {GetResourceSummary()}", Color.yellow);
        return true;
    }

    private bool TryFoundAssociatedPc(Character character)
    {
        if (!CanFoundAssociatedPc(character)) return false;

        Leader owner = character.GetOwner();
        if (owner == null || character.hex == null) return false;

        owner.RemoveGold(FoundingGoldCost);

        string pcName = ResolveAssociatedPcName();
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

    private bool HasAssociatedPcInGame()
    {
        string targetKey = NormalizePcLookupKey(ResolveAssociatedPcName());
        if (string.IsNullOrWhiteSpace(targetKey)) return false;

        RefreshActivePcLookup();
        return activePcLookupKeys.Contains(targetKey);
    }

    private PcEffectCatalog.PcEffectDefinition ResolvePcEffectDefinition()
    {
        string effectId = ResolveAssociatedPcEffectId();
        return PcEffectCatalog.GetDefinition(effectId);
    }

    private static void RefreshActivePcLookup()
    {
        if (activePcLookupFrame == Time.frameCount) return;

        activePcLookupFrame = Time.frameCount;
        activePcLookupKeys.Clear();

        Board board = FindFirstObjectByType<Board>();
        List<Hex> hexes = board != null ? board.GetHexes() : null;
        if (hexes == null) return;

        for (int i = 0; i < hexes.Count; i++)
        {
            PC candidate = hexes[i]?.GetPCData();
            string key = NormalizePcLookupKey(candidate != null ? candidate.pcName : null);
            if (!string.IsNullOrWhiteSpace(key))
            {
                activePcLookupKeys.Add(key);
            }
        }
    }

    private string ResolveAssociatedPcName()
    {
        if (card != null && !string.IsNullOrWhiteSpace(card.name)) return card.name;
        return HumanizeSourceName(GetType().Name);
    }

    private string ResolveAssociatedPcEffectId()
    {
        if (card != null && !string.IsNullOrWhiteSpace(card.pcEffectId)) return card.pcEffectId;
        return null;
    }

    private static string NormalizePcLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string HumanizeSourceName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        List<string> parts = new();
        string current = string.Empty;
        foreach (char ch in value)
        {
            if (char.IsUpper(ch) && current.Length > 0)
            {
                parts.Add(current);
                current = string.Empty;
            }
            current += ch;
        }
        if (current.Length > 0) parts.Add(current);
        return string.Join(" ", parts);
    }
}
