using NUnit.Framework;

namespace RetroLOTR.Tutorial.Tests;

/// <summary>
/// Integration tests for the Gandalf tutorial end-to-end.
/// Verifies that every step's required card exists in the gandalf_base deck
/// chain, army cards are valid, artifacts are configured, and the full flow
/// can be completed — including the skip path that grants armies and artifacts.
/// </summary>
[TestFixture]
public class GandalfTutorialTests : TutorialTestBase
{
    private TutorialFlow _flow = null!;
    private BiomeEntry   _biome = null!;
    private List<CardInfo> _pool = null!;

    [OneTimeSetUp]
    public void LoadData()
    {
        _flow  = GetFlow("Gandalf");
        _biome = GetBiome("Gandalf");
        _pool  = BuildAvailableCards("gandalf_base");
    }

    // ── Flow structure ───────────────────────────────────────────────────────

    [Test]
    public void Flow_HasCorrectLeaderAndSteps()
    {
        Assert.That(_flow.LeaderName, Is.EqualTo("Gandalf"));
        Assert.That(_flow.Steps, Is.Not.Empty);
        Assert.That(_flow.Steps.Count(s => s.Required), Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 required tutorial steps for Gandalf");
    }

    [Test]
    public void Flow_ContainsExpectedSteps()
    {
        var ids = _flow.Steps.Select(s => s.StepId).ToHashSet();
        foreach (string expected in new[] { "Gandalf_Hobbiton", "Gandalf_HearRumours",
                                            "Gandalf_FindArtifact", "Gandalf_FrodoTrain" })
            Assert.That(ids, Has.Member(expected), $"Expected step '{expected}' not found in flow");
    }

    // ── Card availability ────────────────────────────────────────────────────

    [Test]
    public void AllPerformActionSteps_RequiredCardExistsInDeckChain()
    {
        foreach (var step in _flow.Steps.Where(IsAction))
        {
            string? card = step.Requirements?.CardName;
            if (string.IsNullOrWhiteSpace(card)) continue;
            Assert.That(CardExists(_pool, card), Is.True,
                $"Step '{step.StepId}' requires card '{card}' which is not found in the gandalf_base deck chain (including shared decks)");
        }
    }

    // ── Bounders (army training step) ────────────────────────────────────────

    [Test]
    public void FrodoTrainStep_RequiresBounders_ActorIsFrodo()
    {
        var step = _flow.Steps.FirstOrDefault(s => s.StepId == "Gandalf_FrodoTrain");
        Assert.That(step, Is.Not.Null, "Gandalf_FrodoTrain step missing");
        Assert.That(step!.Requirements?.CardName,       Is.EqualTo("Bounders"));
        Assert.That(step.Requirements?.ActorCharacter,  Is.EqualTo("Frodo"));
    }

    [Test]
    public void BounderCard_ExistsInGandalfBase_AsArmyWithValidTroopType()
    {
        var card = FindCard(_pool, "Bounders");
        Assert.That(card, Is.Not.Null,
            "Bounders card not found in gandalf_base deck chain — move it to GandalfBase.json");
        Assert.That(card!.Type, Is.EqualTo("Army"),
            "Bounders card type is not 'Army'");
        Assert.That(card.TroopType, Is.InRange(0, 7),
            $"Bounders Army card has troopType={card.TroopType} outside the valid TroopsTypeEnum range [0,7]");
    }

    // ── Skip tutorial path (army + artifacts) ────────────────────────────────

    [Test]
    public void SkipTutorial_AllArmySteps_HaveValidCard()
    {
        foreach (var step in _flow.Steps.Where(IsAction))
        {
            string? cardName = step.Requirements?.CardName;
            if (string.IsNullOrWhiteSpace(cardName)) continue;
            var card = FindCard(_pool, cardName);
            if (card == null || card.Type != "Army") continue;

            Assert.That(card.TroopType, Is.InRange(0, 7),
                $"Skip tutorial: army card '{cardName}' in step '{step.StepId}' has troopType outside valid range [0,7]");
        }
    }

    [Test]
    public void TutorialArtifacts_AllHaveNonEmptyName()
    {
        Assert.That(_biome.TutorialArtifacts, Is.Not.Null.And.Not.Empty,
            "Gandalf biome has no tutorialArtifacts configured");
        foreach (var a in _biome.TutorialArtifacts!)
            Assert.That(a.ArtifactName, Is.Not.Null.And.Not.Empty,
                "A Gandalf tutorialArtifact has an empty artifactName");
    }

    [Test]
    public void TutorialArtifacts_ContainSignatureItems()
    {
        var names = (_biome.TutorialArtifacts ?? new()).Select(a => a.ArtifactName).ToList();
        foreach (string expected in new[] { "Narya", "Glamdring", "Wizard Staff" })
            Assert.That(names, Has.Member(expected),
                $"Gandalf tutorialArtifacts missing expected item '{expected}'");
    }

    // ── Anchors ──────────────────────────────────────────────────────────────

    [Test]
    public void TutorialAnchors_AreConfigured()
    {
        Assert.That(_biome.TutorialAnchors, Is.Not.Null.And.Not.Empty,
            "Gandalf biome has no tutorialAnchors — artifact placement would fail");
    }

    // ── Support cards ────────────────────────────────────────────────────────

    [Test]
    public void HobbitonStep_HasSupportCard_TheShire()
    {
        var step = _flow.Steps.FirstOrDefault(s => s.StepId == "Gandalf_Hobbiton");
        Assert.That(step, Is.Not.Null);
        Assert.That(step!.Requirements?.SupportCards, Has.Member("TheShire"),
            "Gandalf_Hobbiton step missing TheShire support card");
    }

    [Test]
    public void FrodoTrainStep_HasExpectedSupportCards()
    {
        var step = _flow.Steps.First(s => s.StepId == "Gandalf_FrodoTrain");
        var cards = step.Requirements?.SupportCards ?? new();
        Assert.That(cards, Is.Not.Empty,
            "Gandalf_FrodoTrain step has no supportCards configured");
    }

    // ── End-to-end completeness ──────────────────────────────────────────────

    [Test]
    public void EndToEnd_FlowCanComplete_AllRequiredStepsHaveValidCards()
    {
        var failures = new List<string>();

        foreach (var step in _flow.Steps.Where(s => s.Required))
        {
            string? cardName = step.Requirements?.CardName;
            string? target   = step.Requirements?.TargetLeader;
            string stepType  = step.Type ?? "";

            if (stepType.Equals("explore", StringComparison.OrdinalIgnoreCase)) continue;
            if (stepType.Equals("travel", StringComparison.OrdinalIgnoreCase))  continue;

            // performAction must have a card or a targetLeader
            if (string.IsNullOrWhiteSpace(cardName) && string.IsNullOrWhiteSpace(target))
            {
                failures.Add($"{step.StepId}: performAction with no cardName or targetLeader");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(cardName) && !CardExists(_pool, cardName))
                failures.Add($"{step.StepId}: card '{cardName}' not found in gandalf_base deck chain");
        }

        Assert.That(failures, Is.Empty,
            "Gandalf tutorial cannot complete end-to-end:\n" + string.Join("\n", failures));
    }

    private static bool IsAction(TutorialStep s) =>
        string.Equals(s.Type, "performAction", StringComparison.OrdinalIgnoreCase);
}
