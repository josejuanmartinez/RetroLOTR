using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PCAction : MaterialRetrieval
{
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
            if (character == null || character.killed || character.hex == null) return false;
            if (!IsOnAssociatedRegion(character)) return false;
            if (character.hex.IsWaterTerrain()) return false;
            if (!IsRegionDiscovered(character)) return false;

            PC hexPc = character.hex.GetPCData();
            if (hexPc != null && hexPc.citySize != PCSizeEnum.NONE)
            {
                string targetName = NormalizePcLookupKey(ResolveAssociatedPcName());
                string hexPcName = NormalizePcLookupKey(hexPc.pcName);
                if (hexPcName != targetName) return false;
            }

            return true;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null) return true;

            PC existingPc = FindAssociatedPcInGame();
            if (existingPc != null)
            {
                if (existingPc.hex != null)
                {
                    Leader owner = character.GetOwner();
                    if (owner != null)
                    {
                        existingPc.hex.Reveal(owner);
                    }
                }
                ShowCaravanNotification(character, existingPc);
            }
            else
            {
                bool founded = TryFoundAssociatedPc(character);
                if (founded)
                {
                    ShowCaravanNotification(character, null);
                }
                else
                {
                    string pcName = ResolveAssociatedPcName();
                    MessageDisplayNoUI.ShowMessage(character.hex, character, $"Could not found {pcName}.", Color.red);
                }
            }

            return true;
        };

        base.Initialize(c, card, condition, effect, asyncEffect);
    }

    protected override string GetDescription()
    {
        return card != null ? card.GetRenderedDescription(true) : string.Empty;
    }

    private string GetResourceSummary()
    {
        if (card == null) return "Resources";
        List<string> parts = new();
        if (card.leatherGranted > 0) parts.Add($"+{card.leatherGranted} <sprite name=\"leather\">");
        if (card.mountsGranted > 0) parts.Add($"+{card.mountsGranted} <sprite name=\"mounts\">");
        if (card.timberGranted > 0) parts.Add($"+{card.timberGranted} <sprite name=\"timber\">");
        if (card.ironGranted > 0) parts.Add($"+{card.ironGranted} <sprite name=\"iron\">");
        if (card.steelGranted > 0) parts.Add($"+{card.steelGranted} <sprite name=\"steel\">");
        if (card.mithrilGranted > 0) parts.Add($"+{card.mithrilGranted} <sprite name=\"mithril\">");
        if (card.goldGranted > 0) parts.Add($"+{card.goldGranted} <sprite name=\"gold\">");
        return string.Join("  ", parts);
    }

    private bool ApplyResources(Character character)
    {
        if (character == null || card == null) return false;
        Leader owner = character.GetOwner();
        if (owner == null) return false;
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

        string pcName = ResolveAssociatedPcName();
        PC foundedPc = new(owner, pcName, PCSizeEnum.camp, FortSizeEnum.NONE, false, false, character.hex, false, 75);
        character.hex.RedrawPC();
        MessageDisplayNoUI.ShowMessage(character.hex, character, $"{pcName} is founded.", Color.green);
        return foundedPc != null;
    }

    private bool CanFoundAssociatedPc(Character character)
    {
        if (character == null || character.killed || character.hex == null) return false;
        if (!IsOnAssociatedRegion(character)) return false;
        if (character.hex.IsWaterTerrain()) return false;

        PC hexPc = character.hex.GetPCData();
        if (hexPc != null && hexPc.citySize != PCSizeEnum.NONE)
        {
            string targetName = NormalizePcLookupKey(ResolveAssociatedPcName());
            string hexPcName = NormalizePcLookupKey(hexPc.pcName);
            if (hexPcName != targetName) return false;
        }

        return true;
    }

    private bool IsOnAssociatedRegion(Character character)
    {
        if (character == null || character.hex == null) return false;

        string pcRegion = ResolveAssociatedPcRegion();
        string hexRegion = character.hex.GetLandRegion();
        if (string.IsNullOrWhiteSpace(pcRegion) || string.IsNullOrWhiteSpace(hexRegion)) return false;

        return string.Equals(NormalizePcLookupKey(pcRegion), NormalizePcLookupKey(hexRegion), StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRegionDiscovered(Character character)
    {
        string pcRegion = ResolveAssociatedPcRegion();
        if (string.IsNullOrWhiteSpace(pcRegion)) return true;

        Leader owner = character.GetOwner();
        if (owner == null) return false;

        Board board = FindFirstObjectByType<Board>();
        if (board == null) return false;

        string normalizedTarget = NormalizePcLookupKey(pcRegion);

        foreach (Hex hex in board.GetHexes())
        {
            if (hex == null) continue;

            string hexRegion = hex.GetLandRegion();
            if (string.IsNullOrWhiteSpace(hexRegion))
            {
                PC pc = hex.GetPCData();
                if (pc != null)
                {
                    hexRegion = ResolveRegionForPc(pc);
                }
            }

            if (string.IsNullOrWhiteSpace(hexRegion)) continue;
            if (!string.Equals(NormalizePcLookupKey(hexRegion), normalizedTarget, StringComparison.OrdinalIgnoreCase)) continue;

            if (hex.IsHexRevealed() || hex.IsScoutedBy(owner)) return true;
        }

        return false;
    }

    private string ResolveRegionForPc(PC pc)
    {
        if (pc == null) return null;

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        if (deckManager != null)
        {
            return deckManager.ResolveRegionForPc(pc);
        }

        return null;
    }

    private PC FindAssociatedPcInGame()
    {
        string targetKey = NormalizePcLookupKey(ResolveAssociatedPcName());
        if (string.IsNullOrWhiteSpace(targetKey)) return null;

        Board board = FindFirstObjectByType<Board>();
        List<Hex> hexes = board?.GetHexes();
        if (hexes == null) return null;

        for (int i = 0; i < hexes.Count; i++)
        {
            PC candidate = hexes[i]?.GetPCData();
            if (candidate == null) continue;
            if (NormalizePcLookupKey(candidate.pcName) == targetKey) return candidate;
        }
        return null;
    }

    private void ShowCaravanNotification(Character character, PC existingPc)
    {
        string pcName = ResolveAssociatedPcName();
        string message = $"A caravan from {pcName} arrives with {GetResourceSummary()}";
        Hex pcHex = existingPc?.hex;
        bool isRevealed = pcHex != null && pcHex.IsHexRevealed();

        EventIconsManager iconsManager = EventIconsManager.FindManager();
        if (iconsManager == null) return;

        EventIcon icon = null;
        icon = iconsManager.AddEventIcon(EventIconType.HexMessage, true, () =>
        {
            if (isRevealed && BoardNavigator.Instance != null)
            {
                BoardNavigator.Instance.EnqueueMessageFocus(pcHex, () =>
                    MessageDisplayNoUI.ShowAnchoredMessage(pcHex, message, Color.yellow, true));
            }
            else
            {
                MessageDisplayNoUI.ShowAnchoredMessage(character.hex, message, Color.yellow, true);
            }
            icon?.ConsumeAndDestroy();
        });
    }

    private string ResolveAssociatedPcName()
    {
        if (card != null && !string.IsNullOrWhiteSpace(card.name)) return card.name;
        return HumanizeSourceName(GetType().Name);
    }

    private string ResolveAssociatedPcRegion()
    {
        if (card != null && !string.IsNullOrWhiteSpace(card.region)) return card.region;
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

public class MaterialRetrievalOrAction : PCAction
{
}

public static class PcDescriptionBuilder
{
    public static string BuildBody(CardData data, bool includeFoundingText)
    {
        if (data == null) return string.Empty;

        string regionName = FormatDisplayRegionName(data.region);
        bool hasRegion = !string.IsNullOrWhiteSpace(regionName);

        System.Text.StringBuilder sb = new();
        if (hasRegion)
        {
            sb.Append(regionName).Append(". ");
        }

        string resources = BuildResourceSummary(data);
        if (!string.IsNullOrWhiteSpace(resources))
        {
            sb.Append(resources).Append(".");
        }
        else if (hasRegion)
        {
            sb.Append("Resources.");
        }

        if (includeFoundingText && hasRegion)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append("Can be founded in ").Append(regionName).Append(".");
            if (!string.IsNullOrWhiteSpace(data.name))
            {
                sb.Append(" Allows recruiting characters born in ").Append(data.name).Append(".");
            }
        }

        string body = sb.ToString();
        return body;
    }

    public static string FormatDisplayRegionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (ShouldInsertWordSpace(value, i))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    private static string BuildResourceSummary(CardData data)
    {
        List<string> parts = new();
        if (data.leatherGranted > 0) parts.Add($"{data.leatherGranted}<sprite name=\"leather\">");
        if (data.timberGranted > 0) parts.Add($"{data.timberGranted}<sprite name=\"timber\">");
        if (data.mountsGranted > 0) parts.Add($"{data.mountsGranted}<sprite name=\"mounts\">");
        if (data.ironGranted > 0) parts.Add($"{data.ironGranted}<sprite name=\"iron\">");
        if (data.steelGranted > 0) parts.Add($"{data.steelGranted}<sprite name=\"steel\">");
        if (data.mithrilGranted > 0) parts.Add($"{data.mithrilGranted}<sprite name=\"mithril\">");
        if (data.goldGranted > 0) parts.Add($"{data.goldGranted}<sprite name=\"gold\">");
        return string.Join(" ", parts);
    }

    private static bool ShouldInsertWordSpace(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (index <= 0 || index >= value.Length) return false;

        char current = value[index];
        if (!char.IsUpper(current)) return false;

        char previous = value[index - 1];
        if (char.IsWhiteSpace(previous)) return false;

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        if (!char.IsUpper(previous)) return false;

        if (index + 1 < value.Length && char.IsLower(value[index + 1]))
        {
            return true;
        }

        return false;
    }
}
