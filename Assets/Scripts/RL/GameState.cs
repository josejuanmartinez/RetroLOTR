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

    public List<Hex> GetRelevantHexes(Character c)
    {
        int maxRelevantHexes = game.maxCharacters + game.maxArtifacts + game.maxPCs;
        // Pre-allocate exactly 190 elements for maximum efficiency
        List<Hex> relevantHexes = new(maxRelevantHexes);

        // Use direct access to source collections with index-based insertion
        // var inRangeHexes = hexPathRenderer.FindAllHexesInRange(c);

        var artifactHexes = board.hexesWithArtifacts;
        var characterHexes = board.hexesWithCharacters;
        var pcHexes = board.hexesWithPCs;

        // Add items directly to pre-sized list using index
        //for (int i = 0; i < inRangeHexes.Count && relevantHexes.Count < game.maxRelevantHexes; i++)
        //    relevantHexes.Add(inRangeHexes[i]);

        for (int i = 0; i < artifactHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(artifactHexes[i]);

        for (int i = 0; i < characterHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(characterHexes[i]);

        for (int i = 0; i < pcHexes.Count && relevantHexes.Count < maxRelevantHexes; i++)
            relevantHexes.Add(pcHexes[i]);

        // Fill remaining slots with null (if any)
        int remainingHexes = maxRelevantHexes - relevantHexes.Count;
        for (int i = 0; i < remainingHexes; i++)
            relevantHexes.Add(null);

        Debug.Log("Max relevant hexes: " + maxRelevantHexes);

        Assert.IsTrue(relevantHexes.Count == maxRelevantHexes, "Relevant hexes list size mismatch!");
        return relevantHexes;
    }

    public int GetMaxLeaders() => game.maxLeaders;
    
    public int GetIndexOfLeader(Leader leader) => allLeaders.IndexOf(leader);
    public int GetMaxX() => game.maxBoardWidth;
    public int GetMaxY() => game.maxBoardHeight;
    public int GetMaxCharacters() => game.maxCharacters;
    public int GetMaxArtifacts() => game.maxArtifacts;
    public int GetTurn() => game.turn;
    public int GetMaxTurns() => game.maxTurns;

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
}