using NUnit.Framework;

namespace RetroLOTR.Tutorial.Tests;

/// <summary>
/// Integration tests for the Sauron tutorial end-to-end.
/// Verifies every step's card is in the sauron_base deck chain,
/// Half Orcs (Gothmog's army) is a valid Army card with a real troopType,
/// Gothmog is correctly configured as a granted character,
/// the recruitment unlock step is present, and the skip-tutorial path
/// can grant a real army for all army training steps.
/// </summary>
[TestFixture]
public class SauronTutorialTests : TutorialTestBase
{
    private TutorialFlow _flow = null!;
    private BiomeEntry   _biome = null!;
    private List<CardInfo> _pool = null!;

    [OneTimeSetUp]
    public void LoadData()
    {
        _flow  = GetFlow("Sauron");
        _biome = GetBiome("Sauron");
        _pool  = BuildAvailableCards("sauron_base");
    }

    // ── Flow structure ───────────────────────────────────────────────────────

    [Test]
    public void Flow_HasCorrectLeaderAndSteps()
    {
        Assert.That(_flow.LeaderName, Is.EqualTo("Sauron"));
        Assert.That(_flow.Steps, Is.Not.Empty);
        Assert.That(_flow.Steps.Count(s => s.Required), Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 required tutorial steps for Sauron");
    }

    [Test]
    public void Flow_ContainsExpectedSteps()
    {
        var ids = _flow.Steps.Select(s => s.StepId).ToHashSet();
        foreach (string expected in new[] { "Sauron_BaradDur", "Sauron_MinasMorgul",
                                            "Sauron_ReturnBaradDur", "Sauron_GothmogTrain" })
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
                $"Step '{step.StepId}' requires card '{card}' which is not found in the sauron_base deck chain (including shared decks)");
        }
    }

    // ── Half Orcs (army training step) ──────────────────────────────────────

    [Test]
    public void GothmogTrainStep_RequiresHalfOrcs_ActorIsGothmog()
    {
        var step = _flow.Steps.FirstOrDefault(s => s.StepId == "Sauron_GothmogTrain");
        Assert.That(step, Is.Not.Null, "Sauron_GothmogTrain step missing");
        Assert.That(step!.Requirements?.CardName,      Is.EqualTo("Half Orcs"));
        Assert.That(step.Requirements?.ActorCharacter, Is.EqualTo("Gothmog"));
    }

    [Test]
    public void HalfOrcsCard_ExistsInSauronBase_AsArmyWithValidTroopType()
    {
        var card = FindCard(_pool, "Half Orcs");
        Assert.That(card, Is.Not.Null,
            "Half Orcs card not found in sauron_base deck chain");
        Assert.That(card!.Type, Is.EqualTo("Army"),
            "Half Orcs card type is not 'Army'");
        Assert.That(card.TroopType, Is.InRange(0, 7),
            $"Half Orcs Army card has troopType={card.TroopType} outside the valid TroopsTypeEnum range [0,7]");
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
                $"Skip tutorial: army card '{cardName}' in step '{step.StepId}' has troopType outside valid TroopsTypeEnum range [0,7]");
        }
    }

    [Test]
    public void TutorialArtifacts_AllHaveNonEmptyName()
    {
        Assert.That(_biome.TutorialArtifacts, Is.Not.Null.And.Not.Empty,
            "Sauron biome has no tutorialArtifacts configured");
        foreach (var a in _biome.TutorialArtifacts!)
            Assert.That(a.ArtifactName, Is.Not.Null.And.Not.Empty,
                "A Sauron tutorialArtifact has an empty artifactName");
    }

    [Test]
    public void TutorialArtifacts_ContainSignatureItems()
    {
        var names = (_biome.TutorialArtifacts ?? new()).Select(a => a.ArtifactName).ToList();
        foreach (string expected in new[] { "Dark Lord Banner", "Palantir of Ithil" })
            Assert.That(names, Has.Member(expected),
                $"Sauron tutorialArtifacts missing expected item '{expected}'");
    }

    // ── Gothmog (granted character) ──────────────────────────────────────────

    [Test]
    public void GrantedCharacter_Gothmog_IsCommander_WithValidRace()
    {
        var gothmog = AllGrantedCharacters(_flow)
            .FirstOrDefault(c => string.Equals(c.CharacterName, "Gothmog", StringComparison.OrdinalIgnoreCase));
        Assert.That(gothmog, Is.Not.Null, "Gothmog not found in Sauron tutorial grantCharacters");
        Assert.That(gothmog!.Commander, Is.EqualTo(1), "Gothmog should be a commander");
        Assert.That(gothmog.Race, Is.InRange(0, 10), $"Gothmog race={gothmog.Race} is out of expected range");
    }

    [Test]
    public void GrantedCharacter_Gothmog_IsGrantedBeforeArmyTrainingStep()
    {
        int grantStep = _flow.Steps
            .Select((s, i) => (s, i))
            .Where(t => t.s.Rewards?.GrantCharacters?.Any(c =>
                string.Equals(c.CharacterName, "Gothmog", StringComparison.OrdinalIgnoreCase)) == true)
            .Select(t => t.i)
            .FirstOrDefault(-1);

        int trainStep = _flow.Steps
            .Select((s, i) => (s, i))
            .Where(t => t.s.StepId == "Sauron_GothmogTrain")
            .Select(t => t.i)
            .FirstOrDefault(-1);

        Assert.That(grantStep, Is.GreaterThanOrEqualTo(0), "Gothmog grant step not found");
        Assert.That(trainStep, Is.GreaterThanOrEqualTo(0), "Sauron_GothmogTrain step not found");
        Assert.That(grantStep, Is.LessThan(trainStep),
            "Gothmog must be granted before Sauron_GothmogTrain (can't train with a character not yet in the company)");
    }

    [Test]
    public void AllGrantedCharacters_HaveValidRoleAndRace()
    {
        foreach (var c in AllGrantedCharacters(_flow))
        {
            Assert.That(c.Commander + c.Agent + c.Emmissary + c.Mage, Is.GreaterThan(0),
                $"'{c.CharacterName}' has no role set in Sauron tutorial");
            Assert.That(c.Race, Is.InRange(0, 10),
                $"'{c.CharacterName}' has invalid race={c.Race}");
        }
    }

    // ── Recruitment unlock ───────────────────────────────────────────────────

    [Test]
    public void ReturnBaradDurStep_UnlocksMordorRecruitment()
    {
        var step = _flow.Steps.FirstOrDefault(s => s.StepId == "Sauron_ReturnBaradDur");
        Assert.That(step, Is.Not.Null, "Sauron_ReturnBaradDur step missing");
        var tags = step!.Rewards?.UnlockRecruitmentTags ?? new();
        Assert.That(tags, Has.Member("mordor"),
            "Sauron_ReturnBaradDur should unlock 'mordor' recruitment tag");
    }

    // ── Anchors ──────────────────────────────────────────────────────────────

    [Test]
    public void TutorialAnchors_AreConfigured()
    {
        Assert.That(_biome.TutorialAnchors, Is.Not.Null.And.Not.Empty,
            "Sauron biome has no tutorialAnchors — artifact placement would fail");
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
                failures.Add($"{step.StepId}: card '{cardName}' not found in sauron_base deck chain");
        }

        Assert.That(failures, Is.Empty,
            "Sauron tutorial cannot complete end-to-end:\n" + string.Join("\n", failures));
    }

    private static bool IsAction(TutorialStep s) =>
        string.Equals(s.Type, "performAction", StringComparison.OrdinalIgnoreCase);
}
