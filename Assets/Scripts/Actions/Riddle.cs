using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RiddleAction : CharacterAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.characters.Any(x => x != null && !x.killed);
        };

        effect = (character) => true;

        asyncEffect = async (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> targets = character.hex.characters
                .Where(x => x != null && !x.killed)
                .Distinct()
                .ToList();
            if (targets.Count < 1) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select character",
                    "Ok",
                    "Cancel",
                    targets.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = targets.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = targets.FirstOrDefault(x => x == character)
                    ?? targets.FirstOrDefault(x => x.IsRefusingDuels())
                    ?? targets.OrderByDescending(x => x.GetCommander() + x.GetMage() + x.GetAgent()).FirstOrDefault();
            }

            if (target == null) return false;

            target.ClearStatusEffect(StatusEffectEnum.RefusingDuels);
            character.GainDuelSupremacy(1);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"{target.characterName} no longer refuses duels, and {character.characterName} will win any duel forced on them this turn.", Color.cyan);
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
