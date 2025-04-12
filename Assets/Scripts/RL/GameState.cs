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
    private Dictionary<Character, List<Hex>> relevantHexesCache = new();
    private List<Leader> leadersCache;
    private List<Character> allCharactersCache;
    private int boardSize;
    private int maxX;
    private int maxY;
    private int leadersNum;

    public void Awake()
    {
        game = GetComponent<Game>();
        board = FindFirstObjectByType<Board>();
        hexPathRenderer = FindFirstObjectByType<HexPathRenderer>();
    }

    private void UpdateCaches()
    {
        leadersCache = new List<Leader> { game.player };
        leadersCache.AddRange(game.competitors);
        leadersCache.AddRange(game.npcs);
        allCharactersCache = leadersCache.SelectMany(x => x.controlledCharacters).ToList();
        boardSize = board.GetHexes().Count;
        maxX = board.width;
        maxY = board.height;
        leadersNum = leadersCache.Count;
        relevantHexesCache.Clear();
    }

    public void ResetGame()
    {
        UpdateCaches();
    }

    public List<Hex> GetRelevantHexes(Character c)
    {
        if (relevantHexesCache.TryGetValue(c, out var cachedHexes))
        {
            return cachedHexes;
        }

        List<Hex> relevantHexes = new();
        var hexes = board.GetHexes();
        var characterRange = hexPathRenderer.FindAllHexesInRange(c);

        foreach (var hex in hexes)
        {
            if (hex.characters.Count > 0 || 
                hex.armies.Count > 0 || 
                hex.GetPC() != null || 
                hex.hiddenArtifacts.Count > 0 || 
                characterRange.Contains(hex))
            {
                relevantHexes.Add(hex);
            }
        }

        relevantHexesCache[c] = relevantHexes;
        return relevantHexes;
    }

    public int GetLeadersNum() => leadersNum;

    public List<Leader> GetLeaders() => leadersCache;

    public int GetIndexOfLeader(Leader leader) => leadersCache.IndexOf(leader);

    public int GetBoardSize() => boardSize;

    public int GetMaxX() => maxX;

    public int GetMaxY() => maxY;

    public int GetAllCharactersNum() => allCharactersCache.Count;

    public List<Character> GetAllCharacters() => allCharactersCache;

    public List<Artifact> GetAllArtifacts() => game.artifacts;

    public int GetAllArtifactsNum() => game.artifacts.Count;

    public int GetTurn() => game.turn;

    public int GetFriendlyPoints(Leader leader)
    {
        return leadersCache
            .Where(x => x.GetAlignment() != AlignmentEnum.neutral && 
                       x.GetAlignment() == leader.GetAlignment() && 
                       x != leader)
            .Sum(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints());
    }

    public int GetEnemyPoints(Leader leader)
    {
        return leadersCache
            .Where(x => (x.GetAlignment() == AlignmentEnum.neutral || 
                        x.GetAlignment() != leader.GetAlignment()) && 
                       x != leader)
            .Sum(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints());
    }

    public Leader GetWinner()
    {
        int maxPoints = leadersCache.Max(x => x.GetAllPoints());
        return leadersCache.FirstOrDefault(x => x.GetAllPoints() == maxPoints);
    }
}
