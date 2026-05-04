using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class TheSecondSending : EventAction
{
    private const int InsightTurns = 3;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            return board.hexes.Values.Any(h => h != null && !h.IsWaterTerrain());
        };

        async Task<bool> sendingAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Board board = UnityEngine.Object.FindFirstObjectByType<Board>();
            if (board == null || board.hexes == null) return false;

            List<Hex> validHexes = board.hexes.Values
                .Where(h => h != null && !h.IsWaterTerrain())
                .ToList();

            if (validHexes.Count == 0) return false;

            Hex previousHex = character.hex;
            Hex targetHex = validHexes[UnityEngine.Random.Range(0, validHexes.Count)];

            if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
            if (character.IsArmyCommander() && previousHex.armies != null && character.GetArmy() != null && previousHex.armies.Contains(character.GetArmy()))
                previousHex.armies.Remove(character.GetArmy());
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();

            if (!targetHex.characters.Contains(character)) targetHex.characters.Add(character);
            if (character.IsArmyCommander() && targetHex.armies != null && character.GetArmy() != null && !targetHex.armies.Contains(character.GetArmy()))
                targetHex.armies.Add(character.GetArmy());

            character.hex = targetHex;
            character.RefreshKidnappedCharactersPosition();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
            Character.RefreshArtifactPcVisibilityForHex(targetHex);

            targetHex.RedrawCharacters();
            targetHex.RedrawArmies();

            if (character.GetOwner() == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            {
                targetHex.LookAt();
                targetHex.RevealArea(1, true);
            }

            character.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, InsightTurns);

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Second Sending transports {character.characterName} to a distant hex and grants Arcane Insight for {InsightTurns} turns.", new Color(0.5f, 0.4f, 0.7f));
            return true;
        }

        base.Initialize(c, condition, effect, sendingAsync);
    }
}
