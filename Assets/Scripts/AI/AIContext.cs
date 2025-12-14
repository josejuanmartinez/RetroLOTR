using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AIContext
{
    private readonly Board board;
    private readonly List<AIScoredAction> scoredActions = new();
    private readonly List<ArtifactTransferCandidate> artifactTransferCandidates = new();
    private readonly HashSet<string> scoredActionKeys = new();
    private readonly AIContextPrecomputedData? _precomputed;
    private ResourceSnapshot preSnapshot;
    private Dictionary<PlayableLeader, int> preVictoryPoints;

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

    public AIContext(PlayableLeader leader, Character character, List<CharacterAction> availableActions, AIContextPrecomputedData? precomputed = null)
    {
        Leader = leader;
        Character = character;
        AvailableActions = availableActions ?? new List<CharacterAction>();
        board = UnityEngine.Object.FindFirstObjectByType<Board>();
        EconomyStatus = EvaluateEconomy();

        _precomputed = precomputed;
        ApplyPrecomputedData(precomputed ?? AIContextDataBuilder.Build(leader, character));
        preSnapshot = CaptureSnapshot();
        preVictoryPoints = CaptureVictoryPointsSnapshot();
    }

    public bool NeedsEconomicHelp => EconomyStatus == EconomyStatus.Critical || EconomyStatus == EconomyStatus.Weak;
    public bool HasEnemyTarget => closestEnemy.Hex != null || closestNonNeutralEnemy.Hex != null;
    public bool HasNpcTarget => nearestUnrevealedNpcHex != null;
    public bool ShouldPrioritizeMovement => !NeedsEconomicHelp && !HasEnemyTarget && GetPreferredMovementTarget() != null;

    public async Task<bool> TryExecuteAdvisorActionAsync(AdvisorType advisor)
    {
        ResetScoringData();
        CharacterAction action = PickBestActionForAdvisor(advisor);
        if (action == null) return false;

        RecordAction(action, advisor);
        await action.Execute();
        return true;
    }

    public async Task<bool> TryExecuteBestAvailableActionAsync()
    {
        ResetScoringData();
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

    private void ApplyPrecomputedData(AIContextPrecomputedData data)
    {
        goldPerTurn = data.GoldPerTurn;
        goldBuffer = data.GoldBuffer;
        nationPercentageArtifacts = data.NationPercentageArtifacts;
        closestEnemy = data.ClosestEnemy;
        closestNonNeutralEnemy = data.ClosestNonNeutralEnemy;
        nearestUnrevealedNpcDistance = data.NearestUnrevealedNpcDistance;
        nearestUnrevealedNpcHex = data.NearestUnrevealedNpcHex;
        nearestEnemyCharacterDistance = data.NearestEnemyCharacterDistance;
        nearestEnemyCharacterHex = data.NearestEnemyCharacterHex;
        needsIndirectApproach = data.NeedsIndirectApproach;

        if (data.ArtifactTransferCandidates != null && data.ArtifactTransferCandidates.Count > 0)
        {
            artifactTransferCandidates.Clear();
            artifactTransferCandidates.AddRange(data.ArtifactTransferCandidates);
        }
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
                score += GetArtifactTransferScore();
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

        RecordScoredAction(action, advisor, score);
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

    private float GetArtifactTransferScore()
    {
        if (Character == null || Character.hex == null) return 0f;

        // If we have precomputed candidates, reuse them and simply adjust by availability
        if (_precomputed.HasValue && _precomputed.Value.ArtifactTransferCandidates != null && _precomputed.Value.ArtifactTransferCandidates.Count > 0)
        {
            artifactTransferCandidates.Clear();
            artifactTransferCandidates.AddRange(_precomputed.Value.ArtifactTransferCandidates);
            bool canTransferCached = AvailableActions.Any(a => a is TransferArtifact);
            return canTransferCached ? Mathf.Max(0f, _precomputed.Value.BestArtifactTransferScore) : 0f;
        }

        bool canTransfer = AvailableActions.Any(a => a is TransferArtifact);
        if (!canTransfer) return 0f;

        List<Artifact> transferable = Character.artifacts.Where(a => a != null && a.transferable).ToList();
        if (transferable.Count == 0) return 0f;

        if (board == null || board.hexes == null) return 0f;

        List<Character> friendlies = board.hexes.Values
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && ch.hex != null && ch != Character &&
                         (ch.GetOwner() == Character.GetOwner() ||
                          (ch.GetAlignment() == Character.GetAlignment() && ch.GetAlignment() != AlignmentEnum.neutral)))
            .ToList();
        if (friendlies.Count == 0) return 0f;

        artifactTransferCandidates.Clear();
        float bestScore = 0f;
        foreach (Artifact art in transferable)
        {
            foreach (Character target in friendlies)
            {
                float score = 0f;
                float distance = Character.hex != null && target.hex != null
                    ? Vector2.Distance(Character.hex.v2, target.hex.v2)
                    : float.MaxValue;

                // Prefer sharing spells with non-mages
                if (!string.IsNullOrEmpty(art.providesSpell))
                {
                    score += target.GetMage() == 0 ? 6f : 3f;
                }

                // Skill boosts help low-skill targets more
                score += art.commanderBonus > 0 ? art.commanderBonus * 2f + Mathf.Max(0, 5 - target.GetCommander()) : 0f;
                score += art.agentBonus > 0 ? art.agentBonus * 2f + Mathf.Max(0, 5 - target.GetAgent()) : 0f;
                score += art.emmissaryBonus > 0 ? art.emmissaryBonus * 2f + Mathf.Max(0, 5 - target.GetEmmissary()) : 0f;
                score += art.mageBonus > 0 ? art.mageBonus * 2f + Mathf.Max(0, 5 - target.GetMage()) : 0f;

                // Combat bonuses are more valuable on army commanders
                if (target.IsArmyCommander())
                {
                    score += art.bonusAttack * 3f;
                    score += art.bonusDefense * 2f;
                }

                // Small penalty if target already excels in the boosted area
                if (art.commanderBonus > 0 && target.GetCommander() > 3) score -= 2f;
                if (art.agentBonus > 0 && target.GetAgent() > 3) score -= 2f;
                if (art.emmissaryBonus > 0 && target.GetEmmissary() > 3) score -= 2f;
                if (art.mageBonus > 0 && target.GetMage() > 3) score -= 2f;

                // Distance penalty so nearer recipients are favored
                if (distance < float.MaxValue)
                {
                    score -= distance * 2f;
                }
                else
                {
                    score -= 5f;
                }

                artifactTransferCandidates.Add(new ArtifactTransferCandidate(art.artifactName, target.characterName, score, distance));
                bestScore = Mathf.Max(bestScore, score);
            }
        }

        // Reward scenarios where at least one good transfer exists
        return Mathf.Max(0f, bestScore / 3f);
    }

    private void RecordScoredAction(CharacterAction action, AdvisorType advisor, float score)
    {
        if (action == null) return;
        string key = $"{action.actionName}|{advisor}";
        if (scoredActionKeys.Contains(key)) return;
        scoredActionKeys.Add(key);
        float targetDistance = -1f;
        Hex preferred = GetPreferredMovementTarget();
        if (preferred != null && Character != null && Character.hex != null)
        {
            targetDistance = Vector2.Distance(Character.hex.v2, preferred.v2);
        }
        scoredActions.Add(new AIScoredAction(action.actionName, advisor.ToString(), score, targetDistance));
    }

    private void ResetScoringData()
    {
        scoredActions.Clear();
        scoredActionKeys.Clear();
        artifactTransferCandidates.Clear();
    }

    public AIActionLogEntry BuildLogEntry()
    {
        // Refresh enemy target cache after action for post-state measurements
        CacheEnemyTargets();
        ResourceSnapshot post = CaptureSnapshot();
        Hex preferred = GetPreferredMovementTarget();
        Leader owner = Character != null ? Character.GetOwner() : null;
        Army army = Character != null ? Character.GetArmy() : null;
        TargetInfo targetInfo = GetTargetInfo(preferred);
        Dictionary<PlayableLeader, int> postVictoryPoints = CaptureVictoryPointsSnapshot();
        int preVpSelf = GetVictoryPoints(preVictoryPoints, Leader);
        int postVpSelf = GetVictoryPoints(postVictoryPoints, Leader);
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
            armyOffence = army != null ? army.GetOffence() : 0,
            armyDefence = army != null ? army.GetDefence() : 0,
            health = Character?.health ?? 0,
            preCommander = preSnapshot.commander,
            preAgent = preSnapshot.agent,
            preEmmissary = preSnapshot.emmissary,
            preMage = preSnapshot.mage,
            preArmyOffence = preSnapshot.armyOffence,
            preArmyDefence = preSnapshot.armyDefence,
            preHealth = preSnapshot.health,
            commanderDelta = (Character?.GetCommander() ?? 0) - preSnapshot.commander,
            agentDelta = (Character?.GetAgent() ?? 0) - preSnapshot.agent,
            emmissaryDelta = (Character?.GetEmmissary() ?? 0) - preSnapshot.emmissary,
            mageDelta = (Character?.GetMage() ?? 0) - preSnapshot.mage,
            armyOffenceDelta = (army != null ? army.GetOffence() : 0) - preSnapshot.armyOffence,
            armyDefenceDelta = (army != null ? army.GetDefence() : 0) - preSnapshot.armyDefence,
            healthDelta = (Character?.health ?? 0) - preSnapshot.health,
            goldBuffer = owner != null ? owner.goldAmount : 0,
            goldPerTurn = owner != null ? owner.GetGoldPerTurn() : 0,
            leather = owner != null ? owner.leatherAmount : 0,
            timber = owner != null ? owner.timberAmount : 0,
            iron = owner != null ? owner.ironAmount : 0,
            mounts = owner != null ? owner.mountsAmount : 0,
            mithril = owner != null ? owner.mithrilAmount : 0,
            leatherPerTurn = owner != null ? owner.GetLeatherPerTurn() : 0,
            timberPerTurn = owner != null ? owner.GetTimberPerTurn() : 0,
            ironPerTurn = owner != null ? owner.GetIronPerTurn() : 0,
            mountsPerTurn = owner != null ? owner.GetMountsPerTurn() : 0,
            mithrilPerTurn = owner != null ? owner.GetMithrilPerTurn() : 0,
            preGoldBuffer = preSnapshot.gold,
            preGoldPerTurn = preSnapshot.goldPerTurn,
            preLeather = preSnapshot.leather,
            preTimber = preSnapshot.timber,
            preIron = preSnapshot.iron,
            preMounts = preSnapshot.mounts,
            preMithril = preSnapshot.mithril,
            preLeatherPerTurn = preSnapshot.leatherPerTurn,
            preTimberPerTurn = preSnapshot.timberPerTurn,
            preIronPerTurn = preSnapshot.ironPerTurn,
            preMountsPerTurn = preSnapshot.mountsPerTurn,
            preMithrilPerTurn = preSnapshot.mithrilPerTurn,
            goldDelta = (owner != null ? owner.goldAmount : 0) - preSnapshot.gold,
            leatherDelta = (owner != null ? owner.leatherAmount : 0) - preSnapshot.leather,
            timberDelta = (owner != null ? owner.timberAmount : 0) - preSnapshot.timber,
            ironDelta = (owner != null ? owner.ironAmount : 0) - preSnapshot.iron,
            mountsDelta = (owner != null ? owner.mountsAmount : 0) - preSnapshot.mounts,
            mithrilDelta = (owner != null ? owner.mithrilAmount : 0) - preSnapshot.mithril,
            goldPerTurnDelta = (owner != null ? owner.GetGoldPerTurn() : 0) - preSnapshot.goldPerTurn,
            leatherPerTurnDelta = (owner != null ? owner.GetLeatherPerTurn() : 0) - preSnapshot.leatherPerTurn,
            timberPerTurnDelta = (owner != null ? owner.GetTimberPerTurn() : 0) - preSnapshot.timberPerTurn,
            ironPerTurnDelta = (owner != null ? owner.GetIronPerTurn() : 0) - preSnapshot.ironPerTurn,
            mountsPerTurnDelta = (owner != null ? owner.GetMountsPerTurn() : 0) - preSnapshot.mountsPerTurn,
            mithrilPerTurnDelta = (owner != null ? owner.GetMithrilPerTurn() : 0) - preSnapshot.mithrilPerTurn,
            economyStatus = EconomyStatus.ToString(),
            needsIndirect = needsIndirectApproach,
            nationArtifactsShare = nationPercentageArtifacts,
            nearestNpcDistance = nearestUnrevealedNpcDistance,
            nearestEnemyCharacterDistance = nearestEnemyCharacterDistance,
            nearestEnemyStrength = closestEnemy.Strength,
            nearestNonNeutralStrength = closestNonNeutralEnemy.Strength,
            preNearestEnemyStrength = preSnapshot.nearestEnemyStrength,
            preNearestNonNeutralStrength = preSnapshot.nearestNonNeutralStrength,
            nearestEnemyStrengthDelta = closestEnemy.Strength - preSnapshot.nearestEnemyStrength,
            nearestNonNeutralStrengthDelta = closestNonNeutralEnemy.Strength - preSnapshot.nearestNonNeutralStrength,
            targetOwnerName = targetInfo.name,
            targetOwnerAlignment = targetInfo.alignment,
            targetOwnerType = targetInfo.type,
            preferredTargetType = preferred != null ? preferred.GetPC() != null ? "PC" : "Hex" : "None",
            preferredTarget = preferred != null ? preferred.v2 : Vector2Int.one * -1,
            preferredTargetDistance = preferred != null && Character != null && Character.hex != null ? Vector2.Distance(Character.hex.v2, preferred.v2) : -1f,
            actionName = LastChosenAction != null ? LastChosenAction.actionName : "Pass",
            advisorType = LastAdvisor.ToString(),
            actionDifficulty = LastChosenAction != null ? LastChosenAction.difficulty : 0,
            actionGoldCost = LastChosenAction != null ? LastChosenAction.GetGoldCost() : 0,
            scoredActions = scoredActions.Select(sa => $"{sa.actionName}|{sa.advisor}|{sa.score:0.00}|{sa.targetDistance:0.00}").ToList(),
            artifactTransferCandidates = artifactTransferCandidates.Select(c => $"{c.artifactName}->{c.targetName}|{c.score:0.00}|{c.distance:0.00}").ToList(),
            victoryPointsSelfBefore = preVpSelf,
            victoryPointsSelfAfter = postVpSelf,
            victoryPointsSelfDelta = postVpSelf - preVpSelf,
            victoryPointsOpponentDeltas = BuildOpponentVpDeltas(postVictoryPoints, preVictoryPoints, Leader)
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

    public struct AIContextPrecomputedData
    {
        public EnemyTarget ClosestEnemy;
        public EnemyTarget ClosestNonNeutralEnemy;
        public float NearestUnrevealedNpcDistance;
        public Hex NearestUnrevealedNpcHex;
        public float NearestEnemyCharacterDistance;
        public Hex NearestEnemyCharacterHex;
        public bool NeedsIndirectApproach;
        public int GoldPerTurn;
        public int GoldBuffer;
        public float NationPercentageArtifacts;
        public List<ArtifactTransferCandidate> ArtifactTransferCandidates;
        public float BestArtifactTransferScore;
    }

    public readonly struct EnemyTarget
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

    private readonly struct AIScoredAction
    {
        public readonly string actionName;
        public readonly string advisor;
        public readonly float score;
        public readonly float targetDistance;

        public AIScoredAction(string actionName, string advisor, float score, float targetDistance)
        {
            this.actionName = actionName;
            this.advisor = advisor;
            this.score = score;
            this.targetDistance = targetDistance;
        }
    }

    public readonly struct ArtifactTransferCandidate
    {
        public readonly string artifactName;
        public readonly string targetName;
        public readonly float score;
        public readonly float distance;

        public ArtifactTransferCandidate(string artifactName, string targetName, float score, float distance)
        {
            this.artifactName = artifactName;
            this.targetName = targetName;
            this.score = score;
            this.distance = distance;
        }
    }

    private TargetInfo GetTargetInfo(Hex targetHex)
    {
        if (targetHex == null) return new TargetInfo(null, null, null);
        Leader rawLeader = GetEnemyLeaderOnHex(targetHex);
        if (rawLeader == null) return new TargetInfo(null, null, null);

        // If NPC has joined, prefer the joined owner
        Leader effective = rawLeader;
        if (rawLeader is NonPlayableLeader npc && npc.joined && npc.GetOwner() != null)
        {
            effective = npc.GetOwner();
        }

        string type = effective is NonPlayableLeader ? "NonPlayableLeader" : "Leader";
        return new TargetInfo(effective.characterName, effective.GetAlignment().ToString(), type);
    }

    private readonly struct TargetInfo
    {
        public readonly string name;
        public readonly string alignment;
        public readonly string type;

        public TargetInfo(string name, string alignment, string type)
        {
            this.name = name;
            this.alignment = alignment;
            this.type = type;
        }
    }

    private ResourceSnapshot CaptureSnapshot()
    {
        Leader owner = Character != null ? Character.GetOwner() : null;
        Army army = Character != null ? Character.GetArmy() : null;
        return new ResourceSnapshot
        {
            gold = owner != null ? owner.goldAmount : 0,
            goldPerTurn = owner != null ? owner.GetGoldPerTurn() : 0,
            leather = owner != null ? owner.leatherAmount : 0,
            timber = owner != null ? owner.timberAmount : 0,
            iron = owner != null ? owner.ironAmount : 0,
            mounts = owner != null ? owner.mountsAmount : 0,
            mithril = owner != null ? owner.mithrilAmount : 0,
            leatherPerTurn = owner != null ? owner.GetLeatherPerTurn() : 0,
            timberPerTurn = owner != null ? owner.GetTimberPerTurn() : 0,
            ironPerTurn = owner != null ? owner.GetIronPerTurn() : 0,
            mountsPerTurn = owner != null ? owner.GetMountsPerTurn() : 0,
            mithrilPerTurn = owner != null ? owner.GetMithrilPerTurn() : 0,
            armyOffence = army != null ? army.GetOffence() : 0,
            armyDefence = army != null ? army.GetDefence() : 0,
            commander = Character?.GetCommander() ?? 0,
            agent = Character?.GetAgent() ?? 0,
            emmissary = Character?.GetEmmissary() ?? 0,
            mage = Character?.GetMage() ?? 0,
            health = Character?.health ?? 0,
            nearestEnemyStrength = closestEnemy.Strength,
            nearestNonNeutralStrength = closestNonNeutralEnemy.Strength
        };
    }

    private struct ResourceSnapshot
    {
        public int gold;
        public int goldPerTurn;
        public int leather;
        public int timber;
        public int iron;
        public int mounts;
        public int mithril;
        public int leatherPerTurn;
        public int timberPerTurn;
        public int ironPerTurn;
        public int mountsPerTurn;
        public int mithrilPerTurn;
        public int armyOffence;
        public int armyDefence;
        public int commander;
        public int agent;
        public int emmissary;
        public int mage;
        public int health;
        public float nearestEnemyStrength;
        public float nearestNonNeutralStrength;
    }

    private Dictionary<PlayableLeader, int> CaptureVictoryPointsSnapshot()
    {
        Game game = UnityEngine.Object.FindFirstObjectByType<Game>();
        if (game == null) return new();
        return VictoryPoints.CalculateForAll(game)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RelativeScore);
    }

    private static int GetVictoryPoints(Dictionary<PlayableLeader, int> snapshot, PlayableLeader leader)
    {
        if (snapshot == null || leader == null) return 0;
        return snapshot.TryGetValue(leader, out int value) ? value : 0;
    }

    private static List<string> BuildOpponentVpDeltas(Dictionary<PlayableLeader, int> post, Dictionary<PlayableLeader, int> pre, PlayableLeader self)
    {
        List<string> result = new();
        if (post == null) return result;
        foreach (var kvp in post)
        {
            PlayableLeader leader = kvp.Key;
            if (leader == null || leader == self) continue;
            int before = pre != null && pre.TryGetValue(leader, out int v) ? v : 0;
            int delta = kvp.Value - before;
            result.Add($"{leader.characterName}|{delta}");
        }
        return result;
    }
}
