using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HarbourAlreadyWhisperingAction : EventAction
{
    private static bool IsSeaHex(Hex hex)
    {
        if (hex == null) return false;
        return hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> enemySeaUnits = board.GetHexes()
                .Where(h => h != null && IsSeaHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetOwner() != owner && ch.GetAlignment() != character.GetAlignment() && ch.hex != null)
                .OrderBy(ch => Vector2.Distance(character.hex.v2, ch.hex.v2))
                .Take(2)
                .ToList();

            HashSet<Hex> revealed = new();
            foreach (Character target in enemySeaUnits)
            {
                if (target.hex == null || revealed.Contains(target.hex)) continue;
                target.hex.RevealArea(0, true, owner);
                owner.AddTemporarySeenHexes(new[] { target.hex });
                target.hex.RefreshVisibilityRendering();
                revealed.Add(target.hex);
            }

            int loyaltyGain = 0;
            PC pc = character.hex.GetPC();
            if (pc != null)
            {
                loyaltyGain = UnityEngine.Random.Range(2, 5);
                pc.IncreaseLoyalty(loyaltyGain, character);
            }

            if (revealed.Count == 0 && loyaltyGain == 0) return false;

            string loyaltyText = loyaltyGain > 0 ? $" {pc.pcName} gains +{loyaltyGain} loyalty." : string.Empty;
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Harbour Already Whispering: revealed {revealed.Count} nearby sea threat hex(es).{loyaltyText}",
                Color.gray);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.GetOwner() == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            bool hasSeaThreat = board.GetHexes()
                .Where(h => h != null && IsSeaHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.GetOwner() != character.GetOwner() && ch.GetAlignment() != character.GetAlignment() && ch.hex != null);

            bool hasPc = character.hex.GetPC() != null && character.hex.GetPC().loyalty < 100;
            return hasSeaThreat || hasPc;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
