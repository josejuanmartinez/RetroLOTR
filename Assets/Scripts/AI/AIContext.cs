using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AIContext
{
    private readonly Board board;

    public PlayableLeader Leader { get; }
    public Character Character { get; }
    public List<CharacterAction> AvailableActions { get; }
    public EconomyStatus EconomyStatus { get; }

    private EnemyTarget closestEnemy;
    private EnemyTarget closestNonNeutralEnemy;
    private float nearestUnrevealedNpcDistance = float.MaxValue;
    private Hex nearestUnrevealedNpcHex = null;
    private float nearestEnemyCharacterDistance = float.MaxValue;
    private Hex nearestEnemyCharacterHex = null;
    private bool needsIndirectApproach = false;
    private int goldPerTurn = 0;
    private int goldBuffer = 0;
    private float nationPercentageArtifacts = 0;
    public CharacterAction LastChosenAction { get; private set; }
    public AdvisorType LastAdvisor { get; private set; }

    public AIContext(PlayableLeader leader, Character character, List<CharacterAction> availableActions)
    {
        Leader = leader;
        Character = character;
        AvailableActions = availableActions ?? new List<CharacterAction>();
        board = UnityEngine.Object.FindFirstObjectByType<Board>();
        EconomyStatus = EvaluateEconomy();
        goldPerTurn = leader != null ? leader.GetGoldPerTurn() : 0;
        goldBuffer = leader != null ? leader.goldAmount : 0;
        nationPercentageArtifacts = CalculateNationArtifacts();
        CacheEnemyTargets();
        CacheNpcTargets();
    }

    public bool NeedsEconomicHelp => EconomyStatus == EconomyStatus.Critical || EconomyStatus == EconomyStatus.Weak;
    public bool HasEnemyTarget => closestEnemy.Hex != null || closestNonNeutralEnemy.Hex != null;
    public bool HasNpcTarget => nearestUnrevealedNpcHex != null;
    public bool ShouldPrioritizeMovement => !NeedsEconomicHelp && !HasEnemyTarget && GetPreferredMovementTarget() != null;

    public async Task<bool> TryExecuteAdvisorActionAsync(AdvisorType advisor)
    {
        CharacterAction action = PickBestActionForAdvisor(advisor);
        if (action == null) return false;

        RecordAction(action, advisor);
        await action.Execute();
        return true;
    }

    public async Task<bool> TryExecuteBestAvailableActionAsync()
    {
        CharacterAction action = AvailableActions
            .OrderByDescending(a => ScoreAction(a, a.GetAdvisorType()))
            .FirstOrDefault();

        if (action == null) return false;

        RecordAction(action, action.GetAdvisorType());
        await action.Execute();
        return true;
    }

    public async Task<bool> PassAsync()
    {
        RecordAction(null, AdvisorType.None);
        await Character.Pass();
        return true;
    }

    private CharacterAction PickBestActionForAdvisor(AdvisorType advisor)
    {
        List<CharacterAction> matches = AvailableActions.Where(a => a.GetAdvisorType() == advisor).ToList();
        if (!matches.Any()) return null;

        return matches.OrderByDescending(a => ScoreAction(a, advisor)).First();
    }

    private float ScoreAction(CharacterAction action, AdvisorType advisor)
    {
        float score = 1f;

        // Prefer easier actions
        score -= Mathf.Clamp(action.difficulty / 25f, 0f, 3f);

        // Prefer advisors that match the character's skills
        score += AdvisorAffinity(advisor);

        // Penalize expensive actions when economy is under pressure
        score -= CalculateCostPressure(action);

        switch (advisor)
        {
            case AdvisorType.Economic:
                score += GetEconomyPressureScore();
                break;
            case AdvisorType.Militaristic:
                score += GetDistanceScore(false);
                score += GetMilitaryEdgeScore();
                break;
            case AdvisorType.Intelligence:
                if (NeedsEconomicHelp) score += 3f;
                if (needsIndirectApproach) score += 3f;
                score += GetNearbyEnemyCharacterScore();
                score += GetDistanceScore(true);
                break;
            case AdvisorType.Magic:
                score += (1f - nationPercentageArtifacts)*2f;
                score += GetDistanceScore(true);
                break;
            case AdvisorType.Diplomatic:
                score += GetDiplomaticScore();
                if (needsIndirectApproach) score += 2f;
                score += GetDistanceScore(true);
                break;
            case AdvisorType.Movement:
                if (ShouldPrioritizeMovement) score += 10f;
                score += GetMovementProximityScore();
                break;
            default:
                break;
        }

        return score;
    }

    private float AdvisorAffinity(AdvisorType advisor)
    {
        return advisor switch
        {
            AdvisorType.Militaristic => Character.GetCommander() * 2f + (Character.IsArmyCommander() ? 2f : 0f),
            AdvisorType.Economic => Character.GetEmmissary() * 0.5f + Character.GetCommander() * 0.25f,
            AdvisorType.Diplomatic => Character.GetEmmissary() * 2f,
            AdvisorType.Intelligence => Character.GetAgent() * 2f,
            AdvisorType.Magic => Character.GetMage() * 2f + Character.artifacts.Count,
            AdvisorType.Movement => Character.GetCommander() * 0.5f + Character.GetAgent() * 0.4f + Character.GetEmmissary() * 0.25f,
            _ => 0f
        };
    }

    private float GetEconomyPressureScore()
    {
        return EconomyStatus switch
        {
            EconomyStatus.Critical => 8f,
            EconomyStatus.Weak => 5f,
            EconomyStatus.Stable => 2f,
            _ => 0f
        };
    }

    private float GetDistanceScore(bool allowNeutral)
    {
        EnemyTarget target = allowNeutral ? closestEnemy : closestNonNeutralEnemy;
        if (target.Hex == null && allowNeutral) target = closestNonNeutralEnemy;
        if (target.Hex == null) target = closestEnemy;

        if (target.Hex == null) return 0f;

        float effectiveDistance = target.Distance + (target.IsNeutral ? 2f : 0f);
        return Mathf.Max(0f, 10f - effectiveDistance);
    }

    private float CalculateCostPressure(CharacterAction action)
    {
        int cost = action.GetGoldCost();
        if (cost <= 0) return 0f;

        float pressureFactor = NeedsEconomicHelp ? 2.5f : 1f;
        float bufferFactor = Mathf.Max(1f, (goldBuffer + Mathf.Max(0, goldPerTurn * 2)) / 10f);
        return (cost / bufferFactor) * pressureFactor;
    }

    private float GetMilitaryEdgeScore()
    {
        if (!Character.IsArmyCommander() || Character.GetArmy() == null) return -4f;

        float myStrength = Character.GetArmy().GetOffence();
        EnemyTarget target = closestEnemy.Hex != null ? closestEnemy : closestNonNeutralEnemy;
        if (target.Hex == null || target.Strength <= 0) return 0f;

        float strengthDiff = myStrength - target.Strength;
        float distancePenalty = target.Distance > 1f ? 1.5f : 0f;

        if (strengthDiff < 0)
        {
            needsIndirectApproach = true;
            return Mathf.Max(-10f, strengthDiff / 10f - distancePenalty);
        }

        return Mathf.Clamp(strengthDiff / 20f, -5f, 8f) - distancePenalty;
    }

    private float GetDiplomaticScore()
    {
        if (nearestUnrevealedNpcDistance == float.MaxValue) return 0f;

        return Mathf.Max(0f, 10f - nearestUnrevealedNpcDistance);
    }

    private float GetNearbyEnemyCharacterScore()
    {
        if (nearestEnemyCharacterDistance == float.MaxValue) return 0f;
        // Closer enemy characters make intelligence more valuable
        return Mathf.Max(0f, 6f - nearestEnemyCharacterDistance);
    }

    private void RecordAction(CharacterAction action, AdvisorType advisor)
    {
        LastChosenAction = action;
        LastAdvisor = advisor;
    }

    public Hex GetPreferredMovementTarget()
    {
        // Priority: unrevealed NPC PCs -> strongest non-neutral enemy -> any enemy -> nearest enemy character
        if (nearestUnrevealedNpcHex != null) return nearestUnrevealedNpcHex;
        if (closestNonNeutralEnemy.Hex != null) return closestNonNeutralEnemy.Hex;
        if (closestEnemy.Hex != null) return closestEnemy.Hex;
        if (nearestEnemyCharacterHex != null) return nearestEnemyCharacterHex;
        return null;
    }

    private float GetMovementProximityScore()
    {
        Hex target = GetPreferredMovementTarget();
        if (target == null || Character == null || Character.hex == null) return 0f;

        float distance = Vector2.Distance(Character.hex.v2, target.v2);
        // Reward being close to the intended destination; closer hexes give larger boosts
        return Mathf.Max(0f, 8f - distance * 2f);
    }

    public AIActionLogEntry BuildLogEntry()
    {
        Hex preferred = GetPreferredMovementTarget();
        return new AIActionLogEntry
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            turn = UnityEngine.Object.FindFirstObjectByType<Game>()?.turn ?? -1,
            leaderName = Leader?.characterName,
            leaderAlignment = Leader?.GetAlignment().ToString(),
            characterName = Character?.characterName,
            characterAlignment = Character?.GetAlignment().ToString(),
            armyCommander = Character != null && Character.IsArmyCommander(),
            commander = Character?.GetCommander() ?? 0,
            agent = Character?.GetAgent() ?? 0,
            emmissary = Character?.GetEmmissary() ?? 0,
            mage = Character?.GetMage() ?? 0,
            goldBuffer = goldBuffer,
            goldPerTurn = goldPerTurn,
            economyStatus = EconomyStatus.ToString(),
            needsIndirect = needsIndirectApproach,
            nationArtifactsShare = nationPercentageArtifacts,
            nearestNpcDistance = nearestUnrevealedNpcDistance,
            nearestEnemyCharacterDistance = nearestEnemyCharacterDistance,
            nearestEnemyStrength = closestEnemy.Strength,
            nearestNonNeutralStrength = closestNonNeutralEnemy.Strength,
            preferredTargetType = preferred != null ? preferred.GetPC() != null ? "PC" : "Hex" : "None",
            preferredTarget = preferred != null ? preferred.v2 : Vector2Int.one * -1,
            actionName = LastChosenAction != null ? LastChosenAction.actionName : "Pass",
            advisorType = LastAdvisor.ToString(),
            actionDifficulty = LastChosenAction != null ? LastChosenAction.difficulty : 0,
            actionGoldCost = LastChosenAction != null ? LastChosenAction.GetGoldCost() : 0
        };
    }

    private EconomyStatus EvaluateEconomy()
    {
        if (Leader == null) return EconomyStatus.Stable;

        int goldPerTurn = Leader.GetGoldPerTurn();
        int goldBuffer = Leader.goldAmount;

        if (goldPerTurn < 0 || goldBuffer < 5) return EconomyStatus.Critical;
        if (goldPerTurn <= 1 || goldBuffer < 15) return EconomyStatus.Weak;
        if (goldPerTurn <= 4) return EconomyStatus.Stable;

        return EconomyStatus.Surplus;
    }

    private void CacheEnemyTargets()
    {
        closestEnemy = new EnemyTarget(null, float.MaxValue, false, 0f);
        closestNonNeutralEnemy = new EnemyTarget(null, float.MaxValue, false, 0f);

        if (board == null || Character == null || Character.hex == null) return;

        IEnumerable<Hex> hexes = board.hexes != null ? board.hexes.Values : Enumerable.Empty<Hex>();

        float myStrength = Character.IsArmyCommander() && Character.GetArmy() != null ? Character.GetArmy().GetOffence() : 0f;

        foreach (Hex hex in hexes)
        {
            bool hasEnemyCharacter = hex.characters.Any(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner()));
            Leader enemyLeader = GetEnemyLeaderOnHex(hex);
            if (enemyLeader == null) continue;

            bool isNeutral = enemyLeader.GetAlignment() == AlignmentEnum.neutral;
            float distance = Vector2.Distance(Character.hex.v2, hex.v2);
            float distanceScore = distance + (isNeutral ? 2f : 0f);
            float strength = EstimateEnemyStrength(hex);

            if (distanceScore < closestEnemy.Score)
            {
                closestEnemy = new EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (!isNeutral && distance < closestNonNeutralEnemy.Distance)
            {
                closestNonNeutralEnemy = new EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (hasEnemyCharacter && distance < nearestEnemyCharacterDistance)
            {
                nearestEnemyCharacterDistance = distance;
                nearestEnemyCharacterHex = hex;
            }
        }

        EnemyTarget best = closestNonNeutralEnemy.Hex != null ? closestNonNeutralEnemy : closestEnemy;
        if (best.Hex != null && best.Strength > myStrength * 1.1f) needsIndirectApproach = true;
    }

    private void CacheNpcTargets()
    {
        if (board == null || Character == null || Character.hex == null) return;

        foreach (Hex hex in board.hexes.Values)
        {
            PC pc = hex.GetPC();
            if (pc == null) continue;
            if (pc.owner is not NonPlayableLeader npc) continue;
            if (npc.IsRevealedToLeader(GameObject.FindFirstObjectByType<Game>().currentlyPlaying)) continue;

            float distance = Vector2.Distance(Character.hex.v2, hex.v2);
            if (distance < nearestUnrevealedNpcDistance)
            {
                nearestUnrevealedNpcDistance = distance;
                nearestUnrevealedNpcHex = hex;
            }
        }
    }

    private Leader GetEnemyLeaderOnHex(Hex hex)
    {
        if (hex == null) return null;

        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner)) return pc.owner;

        Character enemyCharacter = hex.characters.FirstOrDefault(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner()));
        if (enemyCharacter != null) return enemyCharacter.GetOwner();

        return null;
    }

    private float EstimateEnemyStrength(Hex hex)
    {
        if (hex == null) return 0f;

        int strength = 0;
        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner))
        {
            strength = Mathf.Max(strength, pc.GetDefense());
        }

        if (hex.armies != null)
        {
            foreach (Army army in hex.armies)
            {
                if (army == null || army.commander == null) continue;
                if (army.commander.GetOwner() == null) continue;
                if (!IsEnemy(army.commander.GetOwner())) continue;
                strength = Mathf.Max(strength, army.GetDefence());
            }
        }

        return strength;
    }

    private float CalculateNationArtifacts()
    {
        if (Leader == null) return 0;
        return Leader.controlledCharacters.Sum(ch => ch != null ? ch.artifacts.Count * 1f : 0f) / Math.Max(1f, GameObject.FindFirstObjectByType<Game>().artifacts.Count * 1f);
    }

    private bool IsEnemy(Leader other)
    {
        if (other == null || Leader == null) return false;
        if (other == Leader) return false;

        AlignmentEnum myAlignment = Leader.GetAlignment();
        AlignmentEnum otherAlignment = other.GetAlignment();

        if (myAlignment == otherAlignment && myAlignment != AlignmentEnum.neutral) return false;

        // Anything that is not aligned with us is an enemy; neutral is lowest priority but still an enemy.
        return otherAlignment != myAlignment || otherAlignment == AlignmentEnum.neutral;
    }

    private static bool NameContains(string source, string needle)
    {
        return source != null && source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private readonly struct EnemyTarget
    {
        public Hex Hex { get; }
        public float Distance { get; }
        public bool IsNeutral { get; }
        public float Strength { get; }
        public float Score => Distance + (IsNeutral ? 2f : 0f);

        public EnemyTarget(Hex hex, float distance, bool isNeutral, float strength)
        {
            Hex = hex;
            Distance = distance;
            IsNeutral = isNeutral;
            Strength = strength;
        }
    }
}
