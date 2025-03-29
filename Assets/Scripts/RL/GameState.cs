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
    Game game;
    Board board;
    HexPathRenderer hexPathRenderer;
    List<Leader> leaders;
    List<Artifact> artifacts;

    public void Awake()
    {
        game = GetComponent<Game>();
        board = FindFirstObjectByType<Board>();
        hexPathRenderer = FindFirstObjectByType<HexPathRenderer>();
        leaders = new() { game.player };
        leaders.AddRange(game.competitors);
        leaders.AddRange(game.npcs);
        artifacts = FindObjectsByType<Artifact>(FindObjectsSortMode.None).ToList();
    }

    public void ResetGame()
    {

    }

    public List<Hex> GetRelevantHexes(Character c)
    {
        // First, determine relevant hexes
        List<Hex> relevantHexes = new ();

        foreach (var hex in board.GetHexes())
        {
            // Include if it has units
            bool hasUnits = hex.characters.Count() > 0;

            // Include if it has resources
            bool hasArmies = hex.armies.Count() > 0;

            // Artifacts
            bool hasArtifacts = hex.hiddenArtifacts.Count() > 0;

            bool hasPCs = hex.GetPC() != null;

            // Closest strategic hexes
            bool isStrategic = hexPathRenderer.FindAllHexesInRange(c).Contains(hex);

            if (hasUnits || hasArmies || hasPCs || hasArtifacts || isStrategic) relevantHexes.Add(hex);
        }
        return relevantHexes;
    }

    public int GetLeadersNum()
    {
        return leaders.Count;
    }

    public List<Leader> GetLeaders()
    {
        return leaders;
    }

    public int GetIndexOfLeader(Leader leader)
    {
        return leaders.IndexOf(leader);
    }

    public int GetBoardSize()
    {
        return board.GetHexes().Count;
    }

    public int GetMaxX()
    {
        return board.width;
    }

    public int GetMaxY()
    {
        return board.height;
    }

    public int GetAllCharactersNum()
    {
        return GetLeaders().SelectMany(x => x.controlledCharacters).Count();
    }

    public List<Character> GetAllCharacters()
    {
        return GetLeaders().SelectMany(x => x.controlledCharacters).ToList();
    }

    public List<Artifact> GetAllArtifacts()
    {
        return artifacts;
    }
    public int GetAllArtifactsNum()
    {
        return artifacts.Count;
    }

    public int GetTurn()
    {
        return game.turn;
    }

    public int GetFriendlyPoints(Leader leader)
    {
        return GetLeaders().FindAll(x => x.GetAlignment() != AlignmentEnum.neutral && x.GetAlignment() == leader.GetAlignment() && x != leader).Select(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints()).Sum();
    }
    public int GetEnemyPoints(Leader leader)
    {
        return GetLeaders().FindAll(x => (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != leader.GetAlignment())  && x != leader).Select(x => x.GetArmyPoints() + x.GetCharacterPoints() + x.GetPCPoints()).Sum();
    }

    public Leader GetWinner()
    {
        List<Leader> leaders = GetLeaders();
        int maxPoints = leaders.Max(x => x.GetAllPoints());
        return leaders.FirstOrDefault(x => x.GetAllPoints() == maxPoints);
    }
}
