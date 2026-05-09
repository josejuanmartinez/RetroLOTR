using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnderTheWhiteHand : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
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

            List<Character> commanders = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch)
                    && ch.GetArmy() != null)
                .Distinct()
                .ToList();

            if (commanders.Count == 0) return false;

            int upgradedCount = 0;
            foreach (Character commander in commanders)
            {
                Army army = commander.GetArmy();
                army.hi++;
                if (army.ma > 0)
                {
                    army.ma = Math.Max(0, army.ma - 1);
                    army.hi++;
                }
                upgradedCount++;
            }

            // Also upgrade nearest allied army's ma → hi
            Character nearest = commanders.FirstOrDefault(ch => ch.GetArmy()?.ma > 0);
            string nearestMsg = "";
            if (nearest != null)
            {
                nearestMsg = $" {nearest.characterName}'s banner unit converted to Uruk-hai Heavy Infantry.";
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Under the White Hand: {upgradedCount} allied army commander(s) in radius {Radius} gain +1 Heavy Infantry.{nearestMsg}",
                Color.white);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null
                && character.hex != null
                && character.hex.GetHexesInRadius(Radius)
                    .Any(h => h != null && h.characters != null
                        && h.characters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
