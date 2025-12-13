using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

public static class AIContextDataBuilder
{
    public static AIContext.AIContextPrecomputedData Build(PlayableLeader leader, Character character, float maxMilliseconds = -1f)
    {
        Stopwatch stopwatch = null;
        if (maxMilliseconds > 0f)
        {
            stopwatch = Stopwatch.StartNew();
        }

        Board board = Object.FindFirstObjectByType<Board>();
        var data = new AIContext.AIContextPrecomputedData
        {
            GoldPerTurn = leader != null ? leader.GetGoldPerTurn() : 0,
            GoldBuffer = leader != null ? leader.goldAmount : 0,
            NationPercentageArtifacts = CalculateNationArtifacts(leader),
            ClosestEnemy = new AIContext.EnemyTarget(null, float.MaxValue, false, 0f),
            ClosestNonNeutralEnemy = new AIContext.EnemyTarget(null, float.MaxValue, false, 0f),
            NearestUnrevealedNpcDistance = float.MaxValue,
            NearestEnemyCharacterDistance = float.MaxValue,
            ArtifactTransferCandidates = new List<AIContext.ArtifactTransferCandidate>(),
            BestArtifactTransferScore = 0f
        };

        if (board == null || character == null || character.hex == null) return data;
        if (ShouldStop(stopwatch, maxMilliseconds)) return data;

        CacheEnemyTargets(board, character, leader, ref data, stopwatch, maxMilliseconds);
        if (ShouldStop(stopwatch, maxMilliseconds)) return data;

        CacheNpcTargets(board, character, ref data, stopwatch, maxMilliseconds);
        if (ShouldStop(stopwatch, maxMilliseconds)) return data;

        BuildArtifactTransfers(board, leader, character, ref data, stopwatch, maxMilliseconds);

        return data;
    }

    private static void CacheEnemyTargets(Board board, Character character, PlayableLeader leader, ref AIContext.AIContextPrecomputedData data, Stopwatch stopwatch, float maxMilliseconds)
    {
        IEnumerable<Hex> hexes = board.hexes != null ? board.hexes.Values : Enumerable.Empty<Hex>();
        float myStrength = character.IsArmyCommander() && character.GetArmy() != null ? character.GetArmy().GetOffence() : 0f;

        foreach (Hex hex in hexes)
        {
            if (ShouldStop(stopwatch, maxMilliseconds)) return;

            bool hasEnemyCharacter = hex.characters.Any(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner(), leader));
            Leader enemyLeader = GetEnemyLeaderOnHex(hex, leader);
            if (enemyLeader == null) continue;

            bool isNeutral = enemyLeader.GetAlignment() == AlignmentEnum.neutral;
            float distance = Vector2.Distance(character.hex.v2, hex.v2);
            float distanceScore = distance + (isNeutral ? 2f : 0f);
            float strength = EstimateEnemyStrength(hex, leader);

            if (distanceScore < data.ClosestEnemy.Score)
            {
                data.ClosestEnemy = new AIContext.EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (!isNeutral && distance < data.ClosestNonNeutralEnemy.Distance)
            {
                data.ClosestNonNeutralEnemy = new AIContext.EnemyTarget(hex, distance, isNeutral, strength);
            }

            if (hasEnemyCharacter && distance < data.NearestEnemyCharacterDistance)
            {
                data.NearestEnemyCharacterDistance = distance;
                data.NearestEnemyCharacterHex = hex;
            }
        }

        AIContext.EnemyTarget best = data.ClosestNonNeutralEnemy.Hex != null ? data.ClosestNonNeutralEnemy : data.ClosestEnemy;
        if (best.Hex != null && best.Strength > myStrength * 1.1f) data.NeedsIndirectApproach = true;
    }

    private static void CacheNpcTargets(Board board, Character character, ref AIContext.AIContextPrecomputedData data, Stopwatch stopwatch, float maxMilliseconds)
    {
        Game game = GameObject.FindFirstObjectByType<Game>();
        if (board == null || character == null || character.hex == null || game == null) return;

        foreach (Hex hex in board.hexes.Values)
        {
            if (ShouldStop(stopwatch, maxMilliseconds)) return;

            PC pc = hex.GetPC();
            if (pc == null) continue;
            if (pc.owner is not NonPlayableLeader npc) continue;
            if (npc.IsRevealedToLeader(game.currentlyPlaying)) continue;

            float distance = Vector2.Distance(character.hex.v2, hex.v2);
            if (distance < data.NearestUnrevealedNpcDistance)
            {
                data.NearestUnrevealedNpcDistance = distance;
                data.NearestUnrevealedNpcHex = hex;
            }
        }
    }

    private static void BuildArtifactTransfers(Board board, PlayableLeader leader, Character character, ref AIContext.AIContextPrecomputedData data, Stopwatch stopwatch, float maxMilliseconds)
    {
        if (board == null || character == null || character.hex == null || leader == null) return;

        List<Artifact> transferable = character.artifacts.Where(a => a != null && a.transferable).ToList();
        if (transferable.Count == 0) return;

        List<Character> friendlies = board.hexes.Values
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && ch.hex != null && ch != character &&
                         (ch.GetOwner() == character.GetOwner() ||
                          (ch.GetAlignment() == character.GetAlignment() && ch.GetAlignment() != AlignmentEnum.neutral)))
            .ToList();
        if (friendlies.Count == 0) return;

        data.ArtifactTransferCandidates.Clear();
        float bestScore = 0f;
        foreach (Artifact art in transferable)
        {
            foreach (Character target in friendlies)
            {
                if (ShouldStop(stopwatch, maxMilliseconds)) return;

                float score = 0f;
                float distance = character.hex != null && target.hex != null
                    ? Vector2.Distance(character.hex.v2, target.hex.v2)
                    : float.MaxValue;

                if (!string.IsNullOrEmpty(art.providesSpell))
                {
                    score += target.GetMage() == 0 ? 6f : 3f;
                }

                score += art.commanderBonus > 0 ? art.commanderBonus * 2f + Mathf.Max(0, 5 - target.GetCommander()) : 0f;
                score += art.agentBonus > 0 ? art.agentBonus * 2f + Mathf.Max(0, 5 - target.GetAgent()) : 0f;
                score += art.emmissaryBonus > 0 ? art.emmissaryBonus * 2f + Mathf.Max(0, 5 - target.GetEmmissary()) : 0f;
                score += art.mageBonus > 0 ? art.mageBonus * 2f + Mathf.Max(0, 5 - target.GetMage()) : 0f;

                if (target.IsArmyCommander())
                {
                    score += art.bonusAttack * 3f;
                    score += art.bonusDefense * 2f;
                }

                if (art.commanderBonus > 0 && target.GetCommander() > 3) score -= 2f;
                if (art.agentBonus > 0 && target.GetAgent() > 3) score -= 2f;
                if (art.emmissaryBonus > 0 && target.GetEmmissary() > 3) score -= 2f;
                if (art.mageBonus > 0 && target.GetMage() > 3) score -= 2f;

                if (distance < float.MaxValue)
                {
                    score -= distance * 2f;
                }
                else
                {
                    score -= 5f;
                }

                data.ArtifactTransferCandidates.Add(new AIContext.ArtifactTransferCandidate(art.artifactName, target.characterName, score, distance));
                bestScore = Mathf.Max(bestScore, score);
            }
        }

        data.BestArtifactTransferScore = Mathf.Max(0f, bestScore / 3f);
    }

    private static bool IsEnemy(Leader other, PlayableLeader leader)
    {
        if (other == null || leader == null) return false;
        if (other == leader) return false;

        AlignmentEnum myAlignment = leader.GetAlignment();
        AlignmentEnum otherAlignment = other.GetAlignment();

        if (myAlignment == otherAlignment && myAlignment != AlignmentEnum.neutral) return false;

        return otherAlignment != myAlignment || otherAlignment == AlignmentEnum.neutral;
    }

    private static Leader GetEnemyLeaderOnHex(Hex hex, PlayableLeader leader)
    {
        if (hex == null) return null;

        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner, leader)) return pc.owner;

        Character enemyCharacter = hex.characters.FirstOrDefault(c => c != null && c.GetOwner() != null && IsEnemy(c.GetOwner(), leader));
        if (enemyCharacter != null) return enemyCharacter.GetOwner();

        return null;
    }

    private static float EstimateEnemyStrength(Hex hex, PlayableLeader leader)
    {
        if (hex == null) return 0f;

        int strength = 0;
        PC pc = hex.GetPC();
        if (pc != null && pc.owner != null && IsEnemy(pc.owner, leader))
        {
            strength = Mathf.Max(strength, pc.GetDefense());
        }

        if (hex.armies != null)
        {
            foreach (Army army in hex.armies)
            {
                if (army == null || army.commander == null) continue;
                if (army.commander.GetOwner() == null) continue;
                if (!IsEnemy(army.commander.GetOwner(), leader)) continue;
                strength = Mathf.Max(strength, army.GetDefence());
            }
        }

        return strength;
    }

    private static float CalculateNationArtifacts(PlayableLeader leader)
    {
        if (leader == null) return 0f;
        Game game = GameObject.FindFirstObjectByType<Game>();
        float totalArtifacts = game != null ? game.artifacts.Count * 1f : 1f;
        return leader.controlledCharacters.Sum(ch => ch != null ? ch.artifacts.Count * 1f : 0f) / Mathf.Max(1f, totalArtifacts);
    }

    private static bool ShouldStop(Stopwatch stopwatch, float maxMilliseconds)
    {
        return stopwatch != null && maxMilliseconds > 0f && stopwatch.Elapsed.TotalMilliseconds >= maxMilliseconds;
    }
}
