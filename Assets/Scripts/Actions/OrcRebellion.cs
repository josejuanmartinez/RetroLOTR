using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class OrcRebellion : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsValidCommander(Character source, Character target)
    {
        return target != null
            && !target.killed
            && IsAllied(source, target)
            && target.IsArmyCommander()
            && target.GetArmy() != null
            && target.GetArmy().GetSize() > 0;
    }

    private static bool RemoveWeakestUnit(Army army, out string removedType)
    {
        removedType = null;
        if (army == null) return false;

        var candidates = new List<(string key, string label, int count, Action removeOne)>
        {
            ("ma", "Men-at-Arms", army.ma, () => army.ma = Mathf.Max(0, army.ma - 1)),
            ("ar", "Archers", army.ar, () => army.ar = Mathf.Max(0, army.ar - 1)),
            ("li", "Light Infantry", army.li, () => army.li = Mathf.Max(0, army.li - 1)),
            ("hi", "Heavy Infantry", army.hi, () => army.hi = Mathf.Max(0, army.hi - 1)),
            ("lc", "Light Cavalry", army.lc, () => army.lc = Mathf.Max(0, army.lc - 1)),
            ("hc", "Heavy Cavalry", army.hc, () => army.hc = Mathf.Max(0, army.hc - 1)),
            ("ca", "Catapults", army.ca, () => army.ca = Mathf.Max(0, army.ca - 1)),
            ("ws", "Warships", army.ws, () => army.ws = Mathf.Max(0, army.ws - 1))
        };

        var weakest = candidates
            .Where(x => x.count > 0)
            .OrderBy(x => x.count)
            .FirstOrDefault();

        if (weakest.count <= 0) return false;

        weakest.removeOne();
        removedType = weakest.label;
        return true;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;
            return character.hex.characters.Any(ch => IsValidCommander(character, ch));
        };

        async Task<bool> rebellionAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            List<Character> commanders = character.hex.characters
                .Where(ch => IsValidCommander(character, ch))
                .Distinct()
                .ToList();
            if (commanders.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select allied army commander",
                    "Ok",
                    "Cancel",
                    commanders.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = commanders.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = commanders
                    .OrderBy(x => x.GetArmy().GetSize())
                    .ThenBy(x => x.GetCommander())
                    .FirstOrDefault();
            }

            if (target == null || target.GetArmy() == null) return false;

            target.AddCommander(1);
            bool removed = RemoveWeakestUnit(target.GetArmy(), out string removedType);
            if (!removed) return false;

            target.hex?.RedrawArmies();
            target.hex?.RedrawCharacters();

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"{target.characterName} gains +1 Commander permanently and loses 1 {removedType} unit.",
                Color.yellow);
            return true;
        }

        base.Initialize(c, condition, effect, rebellionAsync);
    }
}
