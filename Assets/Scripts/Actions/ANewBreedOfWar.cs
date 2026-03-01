using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ANewBreedOfWar : CommanderAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex
                .GetFriendlyArmies(character.GetOwner())
                .Any(x => x != null && x.GetArmy() != null && x.GetArmy().ma > 0);
        };

        async Task<bool> transformAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> armies = character.hex
                .GetFriendlyArmies(character.GetOwner())
                .Where(x => x != null && x.GetArmy() != null && x.GetArmy().ma > 0)
                .ToList();
            if (armies.Count < 1) return false;

            bool isAI = !character.isPlayerControlled;
            Character targetCommander = null;
            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied army",
                    "Ok",
                    "Cancel",
                    armies.Select(x => x.characterName).ToList(),
                    isAI,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                targetCommander = armies.Find(x => x.characterName == selected);
            }
            else
            {
                targetCommander = armies
                    .OrderByDescending(x => x.GetArmy().ma)
                    .ThenByDescending(x => x.GetArmy().GetSize())
                    .FirstOrDefault();
            }

            Army targetArmy = targetCommander != null ? targetCommander.GetArmy() : null;
            if (targetArmy == null || targetArmy.ma < 1) return false;

            targetArmy.ma -= 1;
            targetArmy.Recruit(TroopsTypeEnum.hi, 1);

            character.hex.RedrawCharacters();
            character.hex.RedrawArmies();

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{targetCommander.characterName}'s army transforms 1 MA into 1 HI.",
                Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, transformAsync);
    }
}
