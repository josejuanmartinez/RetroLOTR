using NUnit.Framework;

namespace RetroLOTR.Tutorial.Tests;

/// <summary>
/// Integration tests for the Saruman tutorial end-to-end.
/// Verifies every step's card is in the saruman_base deck chain,
/// Dunlending Warriors (Waulfa's army) is a valid Army card,
/// Grima and Waulfa are correctly configured as granted characters,
/// and the skip-tutorial path grants a real army.
/// </summary>
[TestFixture]
public class SarumanTutorialTests : TutorialTestBase
{
    private TutorialFlow _flow = null!;
    private BiomeEntry   _biome = null!;
    private List<CardInfo> _pool = null!;

    [OneTimeSetUp]
    public void LoadData()
    {
        _flow  = GetFlow("Saruman");
        _biome = GetBiome("Saruman");
        _pool  = BuildAvailableCards("saruman_base");
    }

    // ── Flow structure ───────────────────────────────────────────────────────

    [Test]
    public void Flow_HasCorrectLeaderAndSteps()
    {
        Assert.That(_flow.LeaderName, Is.EqualTo("Saruman"));
        Assert.That(_flow.Steps, Is.Not.Empty);
        Assert.That(_flow.Steps.Count(s => s.Required), Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 required tutorial steps for Saruman");
    }

    [Test]
    public void Flow_ContainsExpectedSteps()
    {
        var ids = _flow.Steps.Select(s => s.StepId).ToHashSet();
        foreach (string expected in new[] { "Saruman_Orthanc", "Saruman_HearRumours",
                                            "Saruman_WaulfaTrain" })
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
                $"Step '{step.StepId}' requires card '{card}' which is not found in the saruman_base deck chain (including shared decks)");
        }
    }

    // ── Dunlending Warriors (army training step) ──────────────────────────────

    [Test]
    public void WaulfaTrainStep_RequiresDunlendingWarriors_ActorIsWaulfa()
    {
        var step = _flow.Steps.FirstOrDefault(s => s.StepId == "Saruman_WaulfaTrain");
        Assert.That(step, Is.Not.Null, "Saruman_WaulfaTrain step missing");
        Assert.That(step!.Requirements?.CardName,      Is.EqualTo("Dunlending Warriors"));
        Assert.That(step.Requirements?.ActorCharacter, Is.EqualTo("Waulfa"));
    }

    [Test]
    public void DunlendingWarriorsCard_ExistsInSarumanBase_AsArmyWithValidTroopType()
    {
        var card = FindCard(_pool, "Dunlending Warriors");
        Assert.That(card, Is.Not.Null,
            "Dunlending Warriors card not found in saruman_base deck chain");
        Assert.That(card!.Type, Is.EqualTo("Army"),
            "Dunlending Warriors card type is not 'Army'");
        Assert.That(card.TroopType, Is.InRange(0, 7),
            $"Dunlending Warriors Army card has troopType={card.TroopType} outside the valid TroopsTypeEnum range [0,7]");
    }

    // ── Skip tutorial path ───────────────────────────────────────────────────

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
            "Saruman biome has no tutorialArtifacts configured");
        foreach (var a in _biome.TutorialArtifacts!)
            Assert.That(a.ArtifactName, Is.Not.Null.And.Not.Empty,
                "A Saruman tutorialArtifact has an empty artifactName");
    }

    [Test]
    public void TutorialArtifacts_ContainSignatureItems()
    {
        var names = (_biome.TutorialArtifacts ?? new()).Select(a => a.ArtifactName).ToList();
        foreach (string expected in new[] { "Ring of Orthanc", "Palantir of Orthanc", "Saruman's Staff" })
            Assert.That(names, Has.Member(expected),
                $"Saruman tutorialArtifacts missing expected item '{expected}'");
    }

    // ── Granted characters ───────────────────────────────────────────────────

    [Test]
    public void GrantedCharacter_Grima_HasValidRoleAndRace()
    {
        var grima = AllGrantedCharacters(_flow)
            .FirstOrDefault(c => string.Equals(c.CharacterName, "Grima", StringComparison.OrdinalIgnoreCase));
        Assert.That(grima, Is.Not.Null, "Grima not found in Saruman tutorial grantCharacters");
        Assert.That(grima!.Commander + grima.Agent + grima.Emmissary + grima.Mage, Is.GreaterThan(0),
            "Grima has no role set");
        Assert.That(grima.Race, Is.InRange(0, 10), $"Grima race={grima.Race} out of range");
    }

    [Test]
    public void GrantedCharacter_Waulfa_IsCommander_WithCorrectSex()
    {
        var waulfa = AllGrantedCharacters(_flow)
            .FirstOrDefault(c => string.Equals(c.CharacterName, "Waulfa", StringComparison.OrdinalIgnoreCase));
        Assert.That(waulfa, Is.Not.Null, "Waulfa not found in Saruman tutorial grantCharacters");
        Assert.That(waulfa!.Commander, Is.EqualTo(1), "Waulfa should be a commander");
        Assert.That(waulfa.Sex, Is.EqualTo(1), "Waulfa should be female (sex=1)");
    }

    [Test]
    public void AllGrantedCharacters_HaveValidRoleAndRace()
    {
        foreach (var c in AllGrantedCharacters(_flow))
        {
            Assert.That(c.Commander + c.Agent + c.Emmissary + c.Mage, Is.GreaterThan(0),
                $"'{c.CharacterName}' has no role set in Saruman tutorial");
            Assert.That(c.Race, Is.InRange(0, 10),
                $"'{c.CharacterName}' has invalid race={c.Race}");
        }
    }

    // ── Anchors ──────────────────────────────────────────────────────────────

    [Test]
    public void TutorialAnchors_AreConfigured()
    {
        Assert.That(_biome.TutorialAnchors, Is.Not.Null.And.Not.Empty,
            "Saruman biome has no tutorialAnchors — artifact placement would fail");
    }

    // ── End-to-end completeness ──────────────────────────────────────────────

    [Test]
    public void EndToEnd_FlowCanComplete_AllRequiredStepsHaveValidCards()
    {
        var failures = new List<string>();

        foreach (var step in _flow.Steps.Where(s => s.Required))
        {
            string stepType = step.Type ?? "";
            if (stepType.Equals("explore", StringComparison.OrdinalIgnoreCase)) continue;
            if (stepType.Equals("travel",  StringComparison.OrdinalIgnoreCase)) continue;

            string? cardName = step.Requirements?.CardName;
            string? target   = step.Requirements?.TargetLeader;

            if (string.IsNullOrWhiteSpace(cardName) && string.IsNullOrWhiteSpace(target))
            {
                failures.Add($"{step.StepId}: performAction with no cardName or targetLeader");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(cardName) && !CardExists(_pool, cardName))
                failures.Add($"{step.StepId}: card '{cardName}' not found in saruman_base deck chain");
        }

        Assert.That(failures, Is.Empty,
            "Saruman tutorial cannot complete end-to-end:\n" + string.Join("\n", failures));
    }

    private static bool IsAction(TutorialStep s) =>
        string.Equals(s.Type, "performAction", StringComparison.OrdinalIgnoreCase);
}
