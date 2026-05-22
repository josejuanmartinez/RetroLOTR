using NUnit.Framework;

namespace RetroLOTR.Tutorial.Tests;

/// <summary>
/// Unit tests covering tutorial data structure invariants and the card-name
/// resolution logic that TutorialManager.ResolveRequiredCardName implements.
/// These tests run without Unity and catch configuration mistakes early.
/// </summary>
[TestFixture]
public class TutorialUnitTests : TutorialTestBase
{
    // ── ResolveRequiredCardName logic ────────────────────────────────────────
    // Mirrors the logic in TutorialManager.ResolveRequiredCardName.

    [Test]
    public void ResolveCardName_NullVariantCards_ReturnsBase()
    {
        var step = MakeStep("Bounders", variantCards: null);
        Assert.That(Resolve(step, "gandalf"), Is.EqualTo("Bounders"));
    }

    [Test]
    public void ResolveCardName_EmptyVariantCards_ReturnsBase()
    {
        var step = MakeStep("Bounders", variantCards: new());
        Assert.That(Resolve(step, "gandalf"), Is.EqualTo("Bounders"));
    }

    [Test]
    public void ResolveCardName_MatchingVariant_ReturnsVariantCard()
    {
        var step = MakeStep("Bounders", variantCards: new()
        {
            new("stormcrow", "Men Of Bree"),
            new("gandalf",   "Rangers"),
        });
        Assert.That(Resolve(step, "gandalf"), Is.EqualTo("Rangers"));
    }

    [Test]
    public void ResolveCardName_NoMatchingVariant_ReturnsBase()
    {
        var step = MakeStep("Bounders", variantCards: new()
        {
            new("stormcrow", "Men Of Bree"),
        });
        Assert.That(Resolve(step, "gandalf"), Is.EqualTo("Bounders"));
    }

    [Test]
    public void ResolveCardName_CaseInsensitiveVariantMatch()
    {
        var step = MakeStep("Bounders", variantCards: new()
        {
            new("GANDALF", "Rangers"),
        });
        Assert.That(Resolve(step, "gandalf"), Is.EqualTo("Rangers"));
    }

    // ── Tutorial flow structure ──────────────────────────────────────────────

    [Test]
    public void AllLeaders_HaveTutorialFlows()
    {
        var flows = LoadTutorial().Tutorials;
        string[] expected = { "Gandalf", "Saruman", "Sauron" };
        foreach (string leader in expected)
            Assert.That(flows.Any(f => string.Equals(f.LeaderName, leader, StringComparison.OrdinalIgnoreCase)),
                Is.True, $"Missing tutorial flow for '{leader}'");
    }

    [Test]
    public void AllFlows_HaveAtLeastOneRequiredStep()
    {
        foreach (var flow in LoadTutorial().Tutorials)
            Assert.That(flow.Steps.Count(s => s.Required), Is.GreaterThan(0),
                $"Flow '{flow.FlowId}' has no required steps");
    }

    [Test]
    public void AllStepIds_AreUniqueWithinFlow()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        {
            var duplicates = flow.Steps
                .GroupBy(s => s.StepId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Assert.That(duplicates, Is.Empty,
                $"Flow '{flow.FlowId}' has duplicate step IDs: {string.Join(", ", duplicates)}");
        }
    }

    [Test]
    public void AllPerformActionSteps_HaveCardName_Or_TargetLeader()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps.Where(IsAction))
        {
            bool hasCard   = !string.IsNullOrWhiteSpace(step.Requirements?.CardName);
            bool hasTarget = !string.IsNullOrWhiteSpace(step.Requirements?.TargetLeader);
            Assert.That(hasCard || hasTarget, Is.True,
                $"Step '{step.StepId}' is performAction but has neither cardName nor targetLeader");
        }
    }

    // ── Variant-free guarantee ───────────────────────────────────────────────

    [Test]
    public void AllSteps_VariantCards_AreEmpty()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps)
            Assert.That(step.Requirements?.VariantCards?.Count ?? 0, Is.EqualTo(0),
                $"Step '{step.StepId}' in '{flow.FlowId}' has variantCards — tutorial must be variant-agnostic");
    }

    [Test]
    public void AllSteps_VariantSupportCards_AreEmpty()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps)
            Assert.That(step.Requirements?.VariantSupportCards?.Count ?? 0, Is.EqualTo(0),
                $"Step '{step.StepId}' in '{flow.FlowId}' has variantSupportCards — tutorial must be variant-agnostic");
    }

    // ── Granted characters ───────────────────────────────────────────────────

    [Test]
    public void AllGrantedCharacters_HaveNonEmptyName()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps)
        foreach (var c in step.Rewards?.GrantCharacters ?? Enumerable.Empty<GrantedCharacter>())
            Assert.That(string.IsNullOrWhiteSpace(c.CharacterName), Is.False,
                $"Step '{step.StepId}' grants a character with an empty name");
    }

    [Test]
    public void AllGrantedCharacters_HaveAtLeastOneRole()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps)
        foreach (var c in step.Rewards?.GrantCharacters ?? Enumerable.Empty<GrantedCharacter>())
            Assert.That(c.Commander + c.Agent + c.Emmissary + c.Mage, Is.GreaterThan(0),
                $"'{c.CharacterName}' granted in '{step.StepId}' has no role (commander/agent/emmissary/mage all zero)");
    }

    [Test]
    public void AllGrantedCharacters_HaveValidRace()
    {
        foreach (var flow in LoadTutorial().Tutorials)
        foreach (var step in flow.Steps)
        foreach (var c in step.Rewards?.GrantCharacters ?? Enumerable.Empty<GrantedCharacter>())
            Assert.That(c.Race, Is.InRange(0, 10),
                $"'{c.CharacterName}' in '{step.StepId}' has race={c.Race} which is out of expected range [0,10]");
    }

    // ── Tutorial artifacts (biome config) ───────────────────────────────────

    [Test]
    public void AllTutorialArtifacts_HaveNonEmptyName()
    {
        foreach (var biome in LoadBiomes().Biomes)
        foreach (var a in biome.TutorialArtifacts ?? Enumerable.Empty<ArtifactEntry>())
            Assert.That(string.IsNullOrWhiteSpace(a.ArtifactName), Is.False,
                $"Biome '{biome.CharacterName}' has a tutorialArtifact with an empty artifactName");
    }

    [Test]
    public void AllTutorialLeaders_HaveConfiguredArtifacts()
    {
        string[] leaders = { "Gandalf", "Saruman", "Sauron" };
        var biomes = LoadBiomes();
        foreach (string leader in leaders)
        {
            var biome = biomes.Biomes.FirstOrDefault(b =>
                string.Equals(b.CharacterName, leader, StringComparison.OrdinalIgnoreCase));
            Assert.That(biome, Is.Not.Null, $"No biome entry found for '{leader}'");
            Assert.That(biome!.TutorialArtifacts?.Count ?? 0, Is.GreaterThan(0),
                $"Leader '{leader}' has no tutorialArtifacts configured");
        }
    }

    // ── Cards manifest completeness ──────────────────────────────────────────

    [Test]
    public void BaseDeckResourceFiles_AllExistOnDisk()
    {
        string[] baseDeckIds = { "gandalf_base", "saruman_base", "sauron_base" };
        var manifest = LoadManifest();
        foreach (string deckId in baseDeckIds)
        {
            var entry = manifest.Decks.FirstOrDefault(d => d.DeckId == deckId);
            Assert.That(entry, Is.Not.Null, $"Deck '{deckId}' not found in Cards.json manifest");
            string filePath = Path.Combine(ResourcesPath, entry!.ResourcePath + ".json");
            Assert.That(File.Exists(filePath), Is.True,
                $"Resource file for deck '{deckId}' does not exist at '{filePath}'");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TutorialStep MakeStep(string cardName, List<VariantCardEntry>? variantCards) =>
        new("test_step", "performAction", true,
            new StepRequirements(cardName, null, null, null, variantCards, null), null);

    private static string Resolve(TutorialStep step, string variantId)
    {
        if (step.Requirements?.VariantCards != null)
        {
            var match = step.Requirements.VariantCards.FirstOrDefault(v =>
                string.Equals(v.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.CardName;
        }
        return step.Requirements?.CardName ?? string.Empty;
    }

    private static bool IsAction(TutorialStep s) =>
        string.Equals(s.Type, "performAction", StringComparison.OrdinalIgnoreCase);
}
