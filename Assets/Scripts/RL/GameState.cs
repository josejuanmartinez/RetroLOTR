using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
[RequireComponent(typeof(Game))]
/**
 * 
 * HELPER FOR ML AGENTS PURPOSES (REINFORCEMENT LEARNING)
 * 
 */
public class GameState : MonoBehaviour
{
    private Game game;
    private Board board;
    private HexPathRenderer hexPathRenderer;
    private List<Leader> allLeaders;
    private List<Character> allCharacters;

    // Cached sized lists for performance
    private List<Leader> sizedLeadersList = new();
    private List<Character> sizedCharactersList = new();
    private List<Artifact> sizedArtifactsList = new();

    public void Awake()
    {
        game = GetComponent<Game>();
        board = FindFirstObjectByType<Board>();
        hexPathRenderer = FindFirstObjectByType<HexPathRenderer>();
    }

    public void InitializeGameState()
    {
        allLeaders = new List<Leader> { game.player };
        allLeaders.AddRange(game.competitors);
        allLeaders.AddRange(game.npcs);
        allCharacters = allLeaders.SelectMany(x => x.controlledCharacters).ToList();

        sizedLeadersList = CreateLeaders();
        sizedCharactersList = CreateCharacters();
        sizedArtifactsList = CreateArtifacts();
    }

    List<Artifact> CreateArtifacts()
    {
        // Clear and refill the list to avoid allocations
        sizedArtifactsList.Clear();

        // Add existing artifacts (up to max)
        var artifacts = game.artifacts ?? new List<Artifact>();
        int count = Mathf.Min(artifacts.Count, Game.MAX_ARTIFACTS);
        for (int i = 0; i < count; i++)
            sizedArtifactsList.Add(artifacts[i]);

        // Fill with nulls if needed
        for (int i = count; i < Game.MAX_ARTIFACTS; i++)
            sizedArtifactsList.Add(null);

        Assert.IsTrue(sizedArtifactsList.Count == Game.MAX_ARTIFACTS, "Artifact list size mismatch!");
        return sizedArtifactsList;
    }

    List<Leader> CreateLeaders()
    {
        // Clear and refill the list to avoid allocations
        sizedLeadersList.Clear();

        // Add existing leaders (up to max)
        int count = Mathf.Min(allLeaders.Count, Game.MAX_LEADERS);
        for (int i = 0; i < count; i++)
            sizedLeadersList.Add(allLeaders[i]);

        // Fill with nulls if needed
        for (int i = count; i < Game.MAX_LEADERS; i++)
            sizedLeadersList.Add(null);

        Assert.IsTrue(sizedLeadersList.Count == Game.MAX_LEADERS, "Leader list size mismatch!");
        return sizedLeadersList;
    }
    List<Character> CreateCharacters()
    {
        // Clear and refill the list to avoid allocations
        sizedCharactersList.Clear();

        // Add existing characters (up to max)
        int maxCharacters = GetMaxCharacters();
        int count = Mathf.Min(allCharacters.Count, maxCharacters);
        for (int i = 0; i < count; i++)
            sizedCharactersList.Add(allCharacters[i]);

        // Fill with nulls if needed
        for (int i = count; i < maxCharacters; i++)
            sizedCharactersList.Add(null);

        Assert.IsTrue(sizedCharactersList.Count == maxCharacters, "Character list size mismatch!");
        return sizedCharactersList;
    }

    /************* RUNTIME ***************/

    public int GetMaxLeaders() => Game.MAX_LEADERS;
    
    public int GetIndexOfLeader(Leader leader) => allLeaders.IndexOf(leader);
    public int GetMaxX() => Game.MAX_BOARD_WIDTH;
    public int GetMaxY() => Game.MAX_BOARD_HEIGHT;
    public int GetMaxCharacters() => Game.MAX_CHARACTERS;
    public int GetMaxArtifacts() => Game.MAX_ARTIFACTS;
    public int GetTurn() => game.turn;
    public int GetMaxTurns() => Game.MAX_TURNS;

    public int GetMaxMovement() => game.cavalryMovement;

    /******************** REWARDS ********************/
    public int GetFriendlyPoints(Leader leader)
    {
        return allLeaders
            .Where(x => x.GetAlignment() != AlignmentEnum.neutral &&
                       x.GetAlignment() == leader.GetAlignment() &&
                       x != leader)
            .Sum(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints());
    }
    public int GetEnemyPoints(Leader leader)
    {
        return allLeaders
            .Where(x => (x.GetAlignment() == AlignmentEnum.neutral ||
                        x.GetAlignment() != leader.GetAlignment()) &&
                       x != leader)
            .Sum(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints());
    }
    public Leader GetWinner()
    {
        int maxPoints = allLeaders.Max(x => x.GetAllPoints());
        return allLeaders.FirstOrDefault(x => x.GetAllPoints() == maxPoints);
    }

    /*************** OBSERVATIONS *************/
    public int CountControlledHexes(Leader leader)
    {
        int count = 0;
        if (board != null)
        {
            foreach (var hex in board.GetHexes())
            {
                if (hex.characters.Any(c => c != null && !c.killed && c.GetOwner() == leader)) count++;
            }
        }
        return count;
    }

    public float GetAverageCharacterHealth(Leader leader)
    {
        var characters = leader.controlledCharacters;
        if (characters == null || characters.Count == 0) return 0;
        float totalHealth = 0; int count = 0;
        foreach (var character in characters)
        {
            if (character != null && !character.killed) { totalHealth += character.health; count++; }
        }
        return count > 0 ? totalHealth / count : 0;
    }

    public int CountStrategicLocations(Leader leader)
    {
        int count = 0;
        count += leader.controlledPcs.Count(pc => pc != null);
        return count;
    }

    public int CountArtifacts(Leader leader) => leader.controlledCharacters.Sum(c => c != null ? c.artifacts.Count : 0);

    public float EvaluateAttackScore(Hex hex, Character controlledCharacter)
    {
        float score = 0f;
        foreach (var enemy in hex.characters.Where(x => x != null && !x.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment())))
            score += 1.5f * (100 - enemy.health) / 100f;
        foreach (var army in hex.armies.Where(x => x != null && !x.killed && x.commander != null && !x.commander.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment())))
            score += 0.75f;
        var pc = hex.GetPC();
        if (pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != controlledCharacter.GetAlignment()))
            score += 1.5f * (1 - Mathf.Min(pc.GetDefense() / 10000f, 1.0f));
        return score;
    }

    public float EvaluateDefenseScore(Hex hex, Character controlledCharacter)
    {
        float score = 0f;
        foreach (var ally in hex.characters.Where(x => x != null && !x.killed && x.GetAlignment() == controlledCharacter.GetAlignment() && x != controlledCharacter))
            score += 0.5f * (1 - ally.health / 100f);
        foreach (var army in hex.armies.Where(x => x != null && !x.killed && x.commander != null && !x.commander.killed && x.GetAlignment() == controlledCharacter.GetAlignment()))
            score += 0.15f;
        var pc = hex.GetPC();
        if (pc != null && pc.owner.GetAlignment() == controlledCharacter.GetAlignment())
            score += 0.5f * (1 - Mathf.Min(pc.GetDefense() / 10000f, 1.0f));
        return score;
    }

    public float EvaluateResourceScore(Hex hex, Character controlledCharacter)
    {
        float score = 0f;
        var owner = controlledCharacter?.GetOwner();
        if (owner == null) return 0f;
        if (hex.terrainType == TerrainEnum.forest) score += 1.0f * (1 - Mathf.Min(owner.timberAmount / 2000f, 1.0f));
        else if (hex.terrainType == TerrainEnum.mountains) score += 1.0f * (1 - Mathf.Min(owner.ironAmount / 2000f, 1.0f));
        else if (hex.terrainType == TerrainEnum.grasslands) score += 1.0f * (1 - Mathf.Min(owner.leatherAmount / 2000f, 1.0f));
        return score;
    }

    public float EvaluateTerritoryScore(Hex hex, Character controlledCharacter)
    {
        float score = 0f;
        if (IsStrategicLocation(hex)) score += 0.8f;
        if (IsContestedLocation(hex, controlledCharacter)) score += 0.4f;
        return score;
    }

    public bool IsStrategicLocation(Hex hex)
    {
        if (hex.GetPC() != null) return true;
        if (hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0) return true;
        if (hex.terrainType == TerrainEnum.shore) return true;
        return false;
    }

    public bool IsContestedLocation(Hex hex, Character controlledCharacter)
    {
        bool hasAllies = false, hasEnemies = false;
        foreach (var nearbyHex in GetNearbyHexes(hex, controlledCharacter))
        {
            if (nearbyHex == null) continue;
            if (nearbyHex.characters.Any(x => x != null && !x.killed && x.GetAlignment() == controlledCharacter.GetAlignment())) hasAllies = true;
            if (nearbyHex.characters.Any(x => x != null && !x.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()))) hasEnemies = true;
            if (hasAllies && hasEnemies) return true;
        }
        return hasAllies && hasEnemies;
    }

    public List<Hex> GetNearbyHexes(Hex center, Character controlledCharacter)
    {
        var nearbyHexes = new List<Hex>();
        if (controlledCharacter?.relevantHexes == null) return nearbyHexes;
        foreach (var hex in controlledCharacter.relevantHexes)
        {
            if (hex != null && Vector2Int.Distance(hex.v2, center.v2) <= 2) nearbyHexes.Add(hex);
        }
        return nearbyHexes;
    }

    public float EvaluateArtifactScore(Hex hex)
    {
        if (hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0)
            return 2.0f * hex.hiddenArtifacts.Count;
        return 0f;
    }

    public float EvaluateSafetyScore(Hex hex, Character controlledCharacter)
    {
        float score = 0f;
        float minEnemyDistance = float.MaxValue;
        bool enemiesPresent = false;
        if (controlledCharacter?.relevantHexes != null)
        {
            foreach (var relevantHex in controlledCharacter.relevantHexes)
            {
                if (relevantHex == null) continue;
                bool hasEnemies = relevantHex.characters.Any(x => x != null && !x.killed && (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != controlledCharacter.GetAlignment()));
                if (hasEnemies)
                {
                    enemiesPresent = true;
                    float distance = Vector2Int.Distance(hex.v2, relevantHex.v2);
                    minEnemyDistance = Mathf.Min(minEnemyDistance, distance);
                }
            }
        }
        if (!enemiesPresent) return 0f;
        score = Mathf.Min(minEnemyDistance / 5f, 1.0f);
        if (controlledCharacter?.relevantHexes != null)
        {
            foreach (var relevantHex in controlledCharacter.relevantHexes)
            {
                if (relevantHex == null) continue;
                int allyCount = relevantHex.characters.Count(x => x != null && !x.killed && x.GetAlignment() == controlledCharacter.GetAlignment() && x != controlledCharacter);
                if (allyCount > 0 && Vector2Int.Distance(hex.v2, relevantHex.v2) <= 2) score += 0.2f * allyCount;
            }
        }
        return score;
    }

    public float CalculateEnemyThreat(Hex hex, Character controlledCharacter)
    {
        float threat = 0f;
        foreach (var enemy in hex.characters.Where(c => c != null && !c.killed && (c.GetAlignment() == AlignmentEnum.neutral || c.GetAlignment() != controlledCharacter.GetAlignment())))
            threat += enemy.health / 100f;
        foreach (var army in hex.armies.Where(a => a != null && !a.killed && a.commander != null && !a.commander.killed && (a.GetAlignment() == AlignmentEnum.neutral || a.GetAlignment() != controlledCharacter.GetAlignment())))
            threat += 0.5f;
        var pc = hex.GetPC();
        if (pc != null && (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != controlledCharacter.GetAlignment()))
            threat += pc.GetDefense() / 10000f;
        return Mathf.Min(threat, 1f);
    }

    public float CalculateAllySupport(Hex hex, Character controlledCharacter)
    {
        float support = 0f;
        foreach (var ally in hex.characters.Where(c => c != null && !c.killed && c.GetAlignment() == controlledCharacter.GetAlignment() && c != controlledCharacter))
            support += ally.health / 100f;
        foreach (var army in hex.armies.Where(a => a != null && !a.killed && a.commander != null && !a.commander.killed && a.GetAlignment() == controlledCharacter.GetAlignment()))
            support += 0.5f;
        var pc = hex.GetPC();
        if (pc != null && pc.owner.GetAlignment() == controlledCharacter.GetAlignment())
            support += pc.GetDefense() / 10000f;
        return Mathf.Min(support, 1f);
    }

    public float CalculateResourceValue(Hex hex)
    {
        switch (hex.terrainType)
        {
            case TerrainEnum.forest: return 0.8f;
            case TerrainEnum.mountains: return 0.9f;
            case TerrainEnum.hills: return 0.5f;
            default: return 0.2f;
        }
    }

    public Hex FindDirectPathHex(Hex strategicTargetHex, Character controlledCharacter)
    {
        Hex bestHex = null; float bestCost = float.MaxValue;
        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;
            float costToTarget = hexPathRenderer.GetPathCost(hex.v2, strategicTargetHex.v2, controlledCharacter);
            if (costToTarget < bestCost) { bestCost = costToTarget; bestHex = hex; }
        }
        return bestHex;
    }

    public Hex FindCautiousPathHex(Hex strategicTargetHex, Character controlledCharacter)
    {
        Hex bestHex = null; float bestScore = float.MinValue;
        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;
            float costToTarget = hexPathRenderer.GetPathCost(hex.v2, strategicTargetHex.v2, controlledCharacter);
            float safetyScore = EvaluateSafetyScore(hex, controlledCharacter);
            float score = -costToTarget / GetMaxMovement() + safetyScore * 2;
            if (score > bestScore) { bestScore = score; bestHex = hex; }
        }
        return bestHex;
    }

    public Hex FindAggressivePathHex(Hex strategicTargetHex, Character controlledCharacter)
    {
        Hex bestHex = null; float bestScore = float.MinValue;
        foreach (var hex in controlledCharacter.reachableHexes)
        {
            if (hex == null) continue;
            float costToTarget = hexPathRenderer.GetPathCost(hex.v2, strategicTargetHex.v2, controlledCharacter);
            float attackScore = EvaluateAttackScore(hex, controlledCharacter);
            float score = -costToTarget / GetMaxMovement() + attackScore * 3;
            if (score > bestScore) { bestScore = score; bestHex = hex; }
        }
        return bestHex;
    }
}