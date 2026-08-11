using System;
using System.Linq;
using UnityEngine;

// Dwarven "secret door" theme: opens a passage straight into the nearest Underground hex from
// anywhere on the surface. Complements EndlessStairs (which requires already being underground
// and offers a choice among the 3 closest other underground hexes) — this is the entry point.
public class DoorsOfTheDeepDelvedAction : EventAction
{

    private static void MoveCharacterToHex(Character character, Hex targetHex)
    {
        if (character == null || targetHex == null || character.hex == targetHex) return;

        Hex previousHex = character.hex;
        if (previousHex != null)
        {
            if (previousHex.characters.Contains(character)) previousHex.characters.Remove(character);
            if (character.IsArmyCommander() && previousHex.armies != null && character.GetArmy() != null && previousHex.armies.Contains(character.GetArmy()))
                previousHex.armies.Remove(character.GetArmy());
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
        }

        if (!targetHex.characters.Contains(character)) targetHex.characters.Add(character);
        if (character.IsArmyCommander() && targetHex.armies != null && character.GetArmy() != null && !targetHex.armies.Contains(character.GetArmy()))
            targetHex.armies.Add(character.GetArmy());

        character.hex = targetHex;
        character.RefreshKidnappedCharactersPosition();
        Character.RefreshArtifactPcVisibilityForHex(targetHex);

        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();

        if (character.GetOwner() == Game.Instance?.player)
        {
            targetHex.RevealArea(1, true);
        }
    }

    private static Hex FindNearestUndergroundHex(Character character)
    {
        Board board = Board.Instance;
        if (board == null || character == null || character.hex == null) return null;

        Hex from = character.hex;
        return board.GetHexes()
            .Where(h => h != null && h != from && h.IsUnderground())
            .OrderBy(h => Vector2.Distance(from.v2, h.v2))
            .FirstOrDefault();
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

            Hex target = FindNearestUndergroundHex(character);
            if (target == null) return false;

            Hex origin = character.hex;
            MoveCharacterToHex(character, target);
            MessageDisplayNoUI.ShowMessage(origin, character, $"{character.characterName} finds a door delved deep into the dark...", Color.yellow);
            MessageDisplayNoUI.ShowMessage(target, character, $"{character.characterName} emerges from the Underground!", Color.yellow);

            if (character.GetOwner() == Game.Instance?.player)
            {
                target.LookAt();
            }
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.killed || character.hex == null) return false;
            if (character.IsArmyCommander()) return false;
            if (character.hex.IsUnderground()) return false;
            return FindNearestUndergroundHex(character) != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
