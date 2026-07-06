using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ---------------------------------------------------------------------------
// Serialized form of the advisor tuning: scoring weights AIContext uses when
// an advisor ranks a character's playable actions, plus per-action advisor
// ownership overrides (which advisor an action class belongs to).
// Edited via Window > RetroLOTR > AI Widget > Advisors.
// ---------------------------------------------------------------------------

[Serializable]
public class AdvisorWeightEntry
{
    public string key = string.Empty;
    public float value;
}

[Serializable]
public class AdvisorActionOverride
{
    public string actionClass = string.Empty;
    // Empty = keep the advisor coded on the action class.
    public string advisor = string.Empty;
    // Flat score adjustment applied whenever the AI scores this action;
    // lets an action be prioritized over its advisor's other cards.
    public float scoreBonus;
    // Per-action formula composition: true = leave that term out of the score.
    public bool ignoreDifficulty;
    public bool ignoreGoldCost;
    public bool ignoreSkills;
    public bool ignoreSituation;
}

// Which score terms an action opts out of. Default (all false) = full formula.
[Serializable]
public struct ActionScoreFlags
{
    public bool ignoreDifficulty;
    public bool ignoreGoldCost;
    public bool ignoreSkills;
    public bool ignoreSituation;

    public bool AnySet => ignoreDifficulty || ignoreGoldCost || ignoreSkills || ignoreSituation;
}

[Serializable]
public class AdvisorConfigData
{
    public List<AdvisorWeightEntry> weights = new();
    public List<AdvisorActionOverride> actionOverrides = new();
}

public class AdvisorWeightDefinition
{
    public readonly string key;
    public readonly float defaultValue;
    public readonly string description;

    public AdvisorWeightDefinition(string key, float defaultValue, string description)
    {
        this.key = key;
        this.defaultValue = defaultValue;
        this.description = description;
    }
}

public static class AIAdvisorConfig
{
    public const string ResourcePath = "AI/AdvisorConfig";

    public static class Keys
    {
        public const string BaseScore = "Global.BaseScore";
        public const string DifficultyDivisor = "Global.DifficultyDivisor";
        public const string MaxDifficultyPenalty = "Global.MaxDifficultyPenalty";
        public const string CostPressureWhenPoor = "Global.CostPressureWhenPoor";

        public const string MilitaristicPerCommanderLevel = "Affinity.Militaristic.PerCommanderLevel";
        public const string MilitaristicLeadingArmyBonus = "Affinity.Militaristic.LeadingArmyBonus";
        public const string EconomicPerEmissaryLevel = "Affinity.Economic.PerEmissaryLevel";
        public const string EconomicPerCommanderLevel = "Affinity.Economic.PerCommanderLevel";
        public const string DiplomaticPerEmissaryLevel = "Affinity.Diplomatic.PerEmissaryLevel";
        public const string IntelligencePerAgentLevel = "Affinity.Intelligence.PerAgentLevel";
        public const string MagicPerMageLevel = "Affinity.Magic.PerMageLevel";
        public const string MagicPerArtifactCarried = "Affinity.Magic.PerArtifactCarried";
        public const string MovementPerCommanderLevel = "Affinity.Movement.PerCommanderLevel";
        public const string MovementPerAgentLevel = "Affinity.Movement.PerAgentLevel";
        public const string MovementPerEmissaryLevel = "Affinity.Movement.PerEmissaryLevel";

        public const string EconomyCriticalBonus = "Economic.EconomyCriticalBonus";
        public const string EconomyWeakBonus = "Economic.EconomyWeakBonus";
        public const string EconomyStableBonus = "Economic.EconomyStableBonus";

        public const string EconomyCriticalIncomeBelow = "Economy.CriticalIncomeBelow";
        public const string EconomyCriticalGoldBelow = "Economy.CriticalGoldBelow";
        public const string EconomyWeakIncomeAtMost = "Economy.WeakIncomeAtMost";
        public const string EconomyWeakGoldBelow = "Economy.WeakGoldBelow";
        public const string EconomyStableIncomeAtMost = "Economy.StableIncomeAtMost";

        public const string EnemyProximityMax = "Targeting.EnemyProximityMax";
        public const string NeutralTargetExtraDistance = "Targeting.NeutralTargetExtraDistance";

        public const string NoArmyPenalty = "Militaristic.NoArmyPenalty";
        public const string FarTargetPenalty = "Militaristic.FarTargetPenalty";

        public const string IntelligencePoorEconomyBonus = "Intelligence.PoorEconomyBonus";
        public const string IntelligenceOutmatchedBonus = "Intelligence.OutmatchedBonus";
        public const string ScoutAreaBonus = "Intelligence.ScoutAreaBonus";
        public const string EnemyCharacterProximityMax = "Intelligence.EnemyCharacterProximityMax";

        public const string ArtifactScarcityWeight = "Magic.ArtifactScarcityWeight";

        public const string DiplomaticOutmatchedBonus = "Diplomatic.OutmatchedBonus";
        public const string NpcProximityMax = "Diplomatic.NpcProximityMax";

        public const string MovementPriorityBonus = "Movement.PriorityBonus";
        public const string MovementProximityMax = "Movement.ProximityMax";
        public const string MovementDistancePenaltyPerHex = "Movement.DistancePenaltyPerHex";
    }

    public static readonly IReadOnlyList<AdvisorWeightDefinition> KnownWeights = new List<AdvisorWeightDefinition>
    {
        new(Keys.BaseScore, 1f, "Starting score every candidate action begins with."),
        new(Keys.DifficultyDivisor, 25f, "Card difficulty is divided by this to compute the penalty; higher = difficulty matters less."),
        new(Keys.MaxDifficultyPenalty, 3f, "Cap on the difficulty penalty."),
        new(Keys.CostPressureWhenPoor, 2.5f, "Gold-cost penalty multiplier while the economy needs help (1 = no extra pressure)."),

        new(Keys.MilitaristicPerCommanderLevel, 2f, "Militaristic appeal per commander level."),
        new(Keys.MilitaristicLeadingArmyBonus, 2f, "Extra militaristic appeal when the character leads an army."),
        new(Keys.EconomicPerEmissaryLevel, 0.5f, "Economic appeal per emissary level."),
        new(Keys.EconomicPerCommanderLevel, 0.25f, "Economic appeal per commander level."),
        new(Keys.DiplomaticPerEmissaryLevel, 2f, "Diplomatic appeal per emissary level."),
        new(Keys.IntelligencePerAgentLevel, 2f, "Intelligence appeal per agent level."),
        new(Keys.MagicPerMageLevel, 2f, "Magic appeal per mage level."),
        new(Keys.MagicPerArtifactCarried, 1f, "Magic appeal per artifact the character carries."),
        new(Keys.MovementPerCommanderLevel, 0.5f, "Movement appeal per commander level."),
        new(Keys.MovementPerAgentLevel, 0.4f, "Movement appeal per agent level."),
        new(Keys.MovementPerEmissaryLevel, 0.25f, "Movement appeal per emissary level."),

        new(Keys.EconomyCriticalIncomeBelow, 0f, "Economy is Critical when gold income per turn is below this."),
        new(Keys.EconomyCriticalGoldBelow, 5f, "Economy is Critical when stored gold is below this."),
        new(Keys.EconomyWeakIncomeAtMost, 1f, "Economy is Weak when gold income per turn is at most this."),
        new(Keys.EconomyWeakGoldBelow, 15f, "Economy is Weak when stored gold is below this."),
        new(Keys.EconomyStableIncomeAtMost, 4f, "Economy is Stable when gold income per turn is at most this; above it, Surplus."),

        new(Keys.EconomyCriticalBonus, 8f, "Economic advisor bonus while the economy is Critical."),
        new(Keys.EconomyWeakBonus, 5f, "Economic advisor bonus while the economy is Weak."),
        new(Keys.EconomyStableBonus, 2f, "Economic advisor bonus while the economy is Stable."),

        new(Keys.EnemyProximityMax, 10f, "Enemy-proximity bonus at distance 0; fades by 1 per hex."),
        new(Keys.NeutralTargetExtraDistance, 2f, "Neutral targets count as this many hexes farther away."),

        new(Keys.NoArmyPenalty, -4f, "Militaristic score adjustment when the character leads no army."),
        new(Keys.FarTargetPenalty, 1.5f, "Militaristic penalty when the enemy target is more than 1 hex away."),

        new(Keys.IntelligencePoorEconomyBonus, 3f, "Intelligence bonus while the economy needs help."),
        new(Keys.IntelligenceOutmatchedBonus, 3f, "Intelligence bonus when the army is outmatched (indirect approach)."),
        new(Keys.ScoutAreaBonus, 6f, "Extra bonus for the Scout Area action."),
        new(Keys.EnemyCharacterProximityMax, 6f, "Intelligence bonus when an enemy character is at distance 0; fades by 1 per hex."),

        new(Keys.ArtifactScarcityWeight, 2f, "Magic bonus scale for how few artifacts the nation owns (0..1 scarcity times this)."),

        new(Keys.DiplomaticOutmatchedBonus, 2f, "Diplomatic bonus when the army is outmatched (indirect approach)."),
        new(Keys.NpcProximityMax, 10f, "Diplomatic bonus when an unrevealed NPC is at distance 0; fades by 1 per hex."),

        new(Keys.MovementPriorityBonus, 10f, "Movement bonus while movement is the priority (no threats, has a destination)."),
        new(Keys.MovementProximityMax, 8f, "Movement bonus at distance 0 from the preferred destination."),
        new(Keys.MovementDistancePenaltyPerHex, 2f, "How fast the movement bonus fades per hex of distance."),
    };

    private static Dictionary<string, float> defaultsByKey;
    private static Dictionary<string, float> loadedWeights;
    private static Dictionary<string, AdvisorType> loadedOverrides;
    private static Dictionary<string, float> loadedBonuses;
    private static Dictionary<string, ActionScoreFlags> loadedFlags;
    private static bool loaded;

    public static void Reload()
    {
        loadedWeights = null;
        loadedOverrides = null;
        loadedBonuses = null;
        loadedFlags = null;
        loaded = false;
    }

    public static float GetWeight(string key)
    {
        EnsureLoaded();
        if (loadedWeights != null && loadedWeights.TryGetValue(key, out float value)) return value;
        return GetDefaultWeight(key);
    }

    public static float GetDefaultWeight(string key)
    {
        defaultsByKey ??= KnownWeights.ToDictionary(d => d.key, d => d.defaultValue, StringComparer.OrdinalIgnoreCase);
        return defaultsByKey.TryGetValue(key, out float value) ? value : 0f;
    }

    // The advisor an action belongs to for AI decision-making: authored
    // override first, then the action's own default.
    public static AdvisorType ResolveAdvisor(CharacterAction action)
    {
        if (action == null) return AdvisorType.None;
        EnsureLoaded();
        if (loadedOverrides != null
            && loadedOverrides.TryGetValue(action.GetType().Name, out AdvisorType overridden))
        {
            return overridden;
        }
        return action.GetAdvisorType();
    }

    // Flat, user-authored score adjustment for this action (0 when unset).
    public static float GetActionScoreBonus(CharacterAction action)
    {
        if (action == null) return 0f;
        EnsureLoaded();
        return loadedBonuses != null
            && loadedBonuses.TryGetValue(action.GetType().Name, out float bonus)
            ? bonus
            : 0f;
    }

    // Single source of truth for how gold buffer + income map to an economy
    // status. Thresholds are editable in the AI Widget (Economy group).
    public static EconomyStatus EvaluateEconomyStatus(int goldBuffer, int goldPerTurn)
    {
        if (goldPerTurn < GetWeight(Keys.EconomyCriticalIncomeBelow)
            || goldBuffer < GetWeight(Keys.EconomyCriticalGoldBelow)) return EconomyStatus.Critical;
        if (goldPerTurn <= GetWeight(Keys.EconomyWeakIncomeAtMost)
            || goldBuffer < GetWeight(Keys.EconomyWeakGoldBelow)) return EconomyStatus.Weak;
        if (goldPerTurn <= GetWeight(Keys.EconomyStableIncomeAtMost)) return EconomyStatus.Stable;
        return EconomyStatus.Surplus;
    }

    // Which formula terms this action ignores (default = none, full formula).
    public static ActionScoreFlags GetActionScoreFlags(CharacterAction action)
    {
        if (action == null) return default;
        EnsureLoaded();
        return loadedFlags != null
            && loadedFlags.TryGetValue(action.GetType().Name, out ActionScoreFlags flags)
            ? flags
            : default;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        loadedWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        loadedOverrides = new Dictionary<string, AdvisorType>(StringComparer.OrdinalIgnoreCase);
        loadedBonuses = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        loadedFlags = new Dictionary<string, ActionScoreFlags>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;

        AdvisorConfigData data = null;
        try { data = JsonUtility.FromJson<AdvisorConfigData>(asset.text); }
        catch (Exception e)
        {
            Debug.LogWarning($"AIAdvisorConfig: could not parse {ResourcePath}.json — using default weights. {e.Message}");
            return;
        }

        if (data?.weights != null)
        {
            foreach (AdvisorWeightEntry entry in data.weights)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                loadedWeights[entry.key] = entry.value;
            }
        }

        if (data?.actionOverrides != null)
        {
            foreach (AdvisorActionOverride entry in data.actionOverrides)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.actionClass)) continue;

                if (!string.IsNullOrWhiteSpace(entry.advisor))
                {
                    if (Enum.TryParse(entry.advisor, true, out AdvisorType advisor))
                    {
                        loadedOverrides[entry.actionClass] = advisor;
                    }
                    else
                    {
                        Debug.LogWarning($"AIAdvisorConfig: unknown advisor '{entry.advisor}' for action '{entry.actionClass}' — ignoring override.");
                    }
                }

                if (!Mathf.Approximately(entry.scoreBonus, 0f))
                {
                    loadedBonuses[entry.actionClass] = entry.scoreBonus;
                }

                ActionScoreFlags flags = new()
                {
                    ignoreDifficulty = entry.ignoreDifficulty,
                    ignoreGoldCost = entry.ignoreGoldCost,
                    ignoreSkills = entry.ignoreSkills,
                    ignoreSituation = entry.ignoreSituation
                };
                if (flags.AnySet)
                {
                    loadedFlags[entry.actionClass] = flags;
                }
            }
        }
    }
}
