using System.Text.Json.Serialization;

namespace RetroLOTR.Tutorial.Tests;

// ── Tutorial.json ─────────────────────────────────────────────────────────────

public record TutorialRoot(
    [property: JsonPropertyName("tutorials")] List<TutorialFlow> Tutorials
);

public record TutorialFlow(
    [property: JsonPropertyName("leaderName")] string LeaderName,
    [property: JsonPropertyName("flowId")]     string FlowId,
    [property: JsonPropertyName("steps")]      List<TutorialStep> Steps
);

public record TutorialStep(
    [property: JsonPropertyName("stepId")]       string StepId,
    [property: JsonPropertyName("type")]         string Type,
    [property: JsonPropertyName("required")]     bool Required,
    [property: JsonPropertyName("requirements")] StepRequirements? Requirements,
    [property: JsonPropertyName("rewards")]      StepRewards? Rewards
);

public record StepRequirements(
    [property: JsonPropertyName("cardName")]           string? CardName,
    [property: JsonPropertyName("actorCharacter")]     string? ActorCharacter,
    [property: JsonPropertyName("targetLeader")]       string? TargetLeader,
    [property: JsonPropertyName("supportCards")]       List<string>? SupportCards,
    [property: JsonPropertyName("variantCards")]       List<VariantCardEntry>? VariantCards,
    [property: JsonPropertyName("variantSupportCards")]List<VariantSupportCardEntry>? VariantSupportCards
);

public record VariantCardEntry(
    [property: JsonPropertyName("variantId")] string VariantId,
    [property: JsonPropertyName("cardName")]  string CardName
);

public record VariantSupportCardEntry(
    [property: JsonPropertyName("variantId")]  string VariantId,
    [property: JsonPropertyName("cardNames")] List<string> CardNames
);

public record StepRewards(
    [property: JsonPropertyName("grantCharacters")]      List<GrantedCharacter>? GrantCharacters,
    [property: JsonPropertyName("grantArtifacts")]       List<GrantedArtifact>? GrantArtifacts,
    [property: JsonPropertyName("unlockRecruitmentTags")]List<string>? UnlockRecruitmentTags
);

public record GrantedCharacter(
    [property: JsonPropertyName("characterName")] string CharacterName,
    [property: JsonPropertyName("race")]          int Race,
    [property: JsonPropertyName("sex")]           int Sex,
    [property: JsonPropertyName("commander")]     int Commander,
    [property: JsonPropertyName("agent")]         int Agent,
    [property: JsonPropertyName("emmissary")]     int Emmissary,
    [property: JsonPropertyName("mage")]          int Mage
);

public record GrantedArtifact(
    [property: JsonPropertyName("artifactName")] string ArtifactName
);

// ── Cards.json (manifest) ─────────────────────────────────────────────────────

public record CardsManifest(
    [property: JsonPropertyName("decks")] List<DeckManifestEntry> Decks
);

public record DeckManifestEntry(
    [property: JsonPropertyName("deckId")]       string DeckId,
    [property: JsonPropertyName("parentDeckId")] string ParentDeckId,
    [property: JsonPropertyName("resourcePath")] string ResourcePath,
    [property: JsonPropertyName("sharedToAll")]  bool SharedToAll
);

// ── PlayableLeaderBiomes.json ─────────────────────────────────────────────────

public record BiomesRoot(
    [property: JsonPropertyName("biomes")] List<BiomeEntry> Biomes
);

public record BiomeEntry(
    [property: JsonPropertyName("characterName")]   string CharacterName,
    [property: JsonPropertyName("tutorialArtifacts")]List<ArtifactEntry>? TutorialArtifacts,
    [property: JsonPropertyName("tutorialAnchors")] List<string>? TutorialAnchors
);

public record ArtifactEntry(
    [property: JsonPropertyName("artifactName")] string ArtifactName
);

// ── Parsed card info from deck files ─────────────────────────────────────────

public record CardInfo(string Name, string Type, int TroopType);
