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
}