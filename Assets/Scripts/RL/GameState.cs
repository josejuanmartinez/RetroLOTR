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
        int count = Mathf.Min(artifacts.Count, game.maxArtifacts);
        for (int i = 0; i < count; i++)
            sizedArtifactsList.Add(artifacts[i]);

        // Fill with nulls if needed
        for (int i = count; i < game.maxArtifacts; i++)
            sizedArtifactsList.Add(null);

        Assert.IsTrue(sizedArtifactsList.Count == game.maxArtifacts, "Artifact list size mismatch!");
        return sizedArtifactsList;
    }

    List<Leader> CreateLeaders()
    {
        // Clear and refill the list to avoid allocations
        sizedLeadersList.Clear();

        // Add existing leaders (up to max)
        int count = Mathf.Min(allLeaders.Count, game.maxLeaders);
        for (int i = 0; i < count; i++)
            sizedLeadersList.Add(allLeaders[i]);

        // Fill with nulls if needed
        for (int i = count; i < game.maxLeaders; i++)
            sizedLeadersList.Add(null);

        Assert.IsTrue(sizedLeadersList.Count == game.maxLeaders, "Leader list size mismatch!");
        return sizedLeadersList;
    }
    List<Character> CreateCharacters()
    {
        // Clear and refill the list to avoid allocations
        sizedCharactersList.Clear();

        // Add existing characters (up to max)
        int maxCharacters = GetAllCharactersNum();
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

    public int GetMaxObservationSpace() => game.maxObservationSpace;

    public List<Hex> GetRelevantHexes(Character c)
    {
        // Pre-allocate exactly 500 elements for maximum efficiency
        List<Hex> relevantHexes = new(game.maxRelevantHexes);

        // Use direct access to source collections with index-based insertion
        var inRangeHexes = hexPathRenderer.FindAllHexesInRange(c);

        var artifactHexes = board.hexesWithArtifacts;
        var characterHexes = board.hexesWithCharacters;
        var pcHexes = board.hexesWithPCs;

        // Add items directly to pre-sized list using index
        for (int i = 0; i < inRangeHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
            relevantHexes.Add(inRangeHexes[i]);

        for (int i = 0; i < artifactHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
            relevantHexes.Add(artifactHexes[i]);

        for (int i = 0; i < characterHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
            relevantHexes.Add(characterHexes[i]);

        for (int i = 0; i < pcHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
            relevantHexes.Add(pcHexes[i]);

        // Fill remaining slots with null (if any)
        int remainingHexes = game.maxRelevantHexes - relevantHexes.Count;
        for (int i = 0; i < remainingHexes; i++)
            relevantHexes.Add(null);

        Assert.IsTrue(relevantHexes.Count == game.maxRelevantHexes, "Relevant hexes list size mismatch!");
        return relevantHexes;
    }

    public int GetLeadersNum() => game.maxLeaders;

    public List<Leader> GetLeaders() => sizedLeadersList;
    
    public int GetIndexOfLeader(Leader leader) => allLeaders.IndexOf(leader);
    public int GetBoardSize() => game.maxBoardWidth * game.maxBoardHeight;
    public int GetMaxX() => game.maxBoardWidth;
    public int GetMaxY() => game.maxBoardHeight;
    public int GetAllCharactersNum() => game.maxLeaders * game.maxCharactersPerPlayer;

    public List<Character> GetAllCharacters() => sizedCharactersList;    

    public List<Artifact> GetAllArtifacts() => sizedArtifactsList;
    public int GetAllArtifactsNum() => game.maxArtifacts;
    public int GetTurn() => game.turn;

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
}