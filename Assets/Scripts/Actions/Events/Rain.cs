using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Rain : EventAction
{
    private const int Radius = 2;
    private const int MovementPenalty = 4;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            Game game = FindFirstObjectByType<Game>();
            if (board == null || game == null) return false;

            List<Character> allUnits = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            int burningCleared = 0;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Character unit = allUnits[i];
                if (!unit.HasStatusEffect(StatusEffectEnum.Burning)) continue;
                unit.ClearStatusEffect(StatusEffectEnum.Burning);
                burningCleared++;
            }

            List<Character> slowedUnits = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            for (int i = 0; i < slowedUnits.Count; i++)
            {
                slowedUnits[i].moved += MovementPenalty;
            }

            bool obscuredVision = false;
            if (character.GetOwner() == game.player)
            {
                List<Hex> radiusHexes = character.hex.GetHexesInRadius(Radius);
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    Hex targetHex = radiusHexes[i];
                    if (targetHex == null) continue;
                    targetHex.ClearScoutingAll();
                    targetHex.MarkDarknessByPlayer();
                }

                character.hex.ObscureArea(Radius, true, character.GetOwner());
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    radiusHexes[i]?.RefreshVisibilityRendering();
                }
                obscuredVision = true;
            }

            if (burningCleared == 0 && slowedUnits.Count == 0 && !obscuredVision) return false;

            string visionText = obscuredVision ? " Visibility is reduced in radius 2." : string.Empty;
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Rain clears Burning from {burningCleared} unit(s) and reduces movement for {slowedUnits.Count} unit(s) in radius 2.{visionText}",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = FindFirstObjectByType<Board>();
            Game game = FindFirstObjectByType<Game>();
            if (board == null || game == null) return false;

            bool anyBurning = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null && !ch.killed && ch.HasStatusEffect(StatusEffectEnum.Burning));

            bool anyNearbyUnits = character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));

            bool canReduceVisibility = character.GetOwner() == game.player;
            return anyBurning || anyNearbyUnits || canReduceVisibility;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
