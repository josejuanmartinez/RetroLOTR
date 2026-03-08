using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RhosgobelRabbits : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.IsArmyCommander()) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(x => x.GetPC() != null && x.GetPC().owner == character.GetOwner() && x.GetPC().isCapital);
        };

        async Task<bool> rabbitsAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.IsArmyCommander()) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            Hex capitalHex = board.GetHexes().Find(x => x.GetPC() != null && x.GetPC().owner == character.GetOwner() && x.GetPC().isCapital);
            if (capitalHex == null) return false;

            board.MoveCharacterOneHex(character, character.hex, capitalHex, true);
            Sounds.Instance?.PlaySpeedUp();
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rhosgobel Rabbits guide {character.characterName} back to capital.", Color.green);
            board.SelectCharacter(character);
            return true;
        }

        base.Initialize(c, condition, effect, rabbitsAsync);
    }
}
