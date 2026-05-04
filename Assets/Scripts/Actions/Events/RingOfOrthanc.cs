using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RingOfOrthanc : EventAction
{
    private const int BaseHeal = 15;
    private const int OrthancHeal = 25;
    private const int BaseHi = 1;
    private const int OrthancHi = 2;

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())))
                .Distinct()
                .ToList();

            if (allies.Count == 0) return false;

            bool isOnOrthanc = character.hex.GetPC() != null && character.hex.GetPC().pcName == "Orthanc";
            int healAmount = isOnOrthanc ? OrthancHeal : BaseHeal;
            int hiAmount = isOnOrthanc ? OrthancHi : BaseHi;

            int healedCount = 0;
            int reinforcedCount = 0;

            for (int i = 0; i < allies.Count; i++)
            {
                Character ally = allies[i];
                if (ally.health < ally.maxHealth)
                {
                    ally.Heal(healAmount);
                    healedCount++;
                }

                if (ally.IsArmyCommander() && ally.GetArmy() != null)
                {
                    ally.GetArmy().Recruit(TroopsTypeEnum.hi, hiAmount);
                    reinforcedCount++;
                }
            }

            string orthancText = isOnOrthanc ? " from Orthanc" : string.Empty;
            string healText = healedCount > 0 ? $"heals {healedCount} ally for {healAmount}" : string.Empty;
            string reinforceText = reinforcedCount > 0 ? $"reinforces {reinforcedCount} army with {hiAmount} Heavy Infantry" : string.Empty;
            string combinedText = string.Join(" and ", new[] { healText, reinforceText }.Where(s => !string.IsNullOrEmpty(s)));

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Ring of Orthanc{orthancText}: {combinedText}.", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null || character.hex.characters == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && (ch.GetOwner() == character.GetOwner() || (character.GetAlignment() != AlignmentEnum.neutral && ch.GetAlignment() == character.GetAlignment())));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
