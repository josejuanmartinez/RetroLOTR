using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class TheGreyCompany : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static List<Character> GetEligibleTargets(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();

        return character.hex.GetHexesInRadius(3)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsAllied(character, ch) && ch.race == RacesEnum.Dunedain)
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return GetEligibleTargets(character).Count > 0;
        };

        async Task<bool> greyCompanyAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;

            List<Character> targets = GetEligibleTargets(character);
            if (targets.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character commanderTarget;
            Character agentTarget;
            Character emmissaryTarget;

            if (!isAI)
            {
                string commanderName = await SelectionDialog.Ask(
                    "Dunedain gains +1 Commander",
                    "Ok",
                    "Cancel",
                    targets.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(commanderName)) return false;
                commanderTarget = targets.FirstOrDefault(x => x.characterName == commanderName);
                if (commanderTarget == null) return false;

                List<Character> remainingForAgent = targets.Where(x => x != commanderTarget).ToList();
                if (remainingForAgent.Count == 0)
                {
                    commanderTarget.AddCommander(1);
                    MessageDisplayNoUI.ShowMessage(character.hex, character, $"{commanderTarget.characterName} joins the Grey Company and gains +1 Commander.", Color.green);
                    return true;
                }

                string agentName = await SelectionDialog.Ask(
                    "Dunedain gains +1 Agent",
                    "Ok",
                    "Skip",
                    remainingForAgent.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                agentTarget = string.IsNullOrWhiteSpace(agentName) ? null : remainingForAgent.FirstOrDefault(x => x.characterName == agentName);

                List<Character> remainingForEmmissary = remainingForAgent.Where(x => x != agentTarget).ToList();
                if (remainingForEmmissary.Count == 0)
                {
                    emmissaryTarget = null;
                }
                else
                {
                    string emmissaryName = await SelectionDialog.Ask(
                        "Dunedain gains +1 Emmissary",
                        "Ok",
                        "Skip",
                        remainingForEmmissary.Select(x => x.characterName).ToList(),
                        false,
                        SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                    emmissaryTarget = string.IsNullOrWhiteSpace(emmissaryName) ? null : remainingForEmmissary.FirstOrDefault(x => x.characterName == emmissaryName);
                }
            }
            else
            {
                commanderTarget = targets.OrderByDescending(x => x.GetCommander()).FirstOrDefault();
                agentTarget = targets.Where(x => x != commanderTarget).OrderByDescending(x => x.GetAgent()).FirstOrDefault();
                emmissaryTarget = targets.Where(x => x != commanderTarget && x != agentTarget).OrderByDescending(x => x.GetEmmissary()).FirstOrDefault();
            }

            int affected = 0;
            List<string> summary = new();

            if (commanderTarget != null)
            {
                commanderTarget.AddCommander(1);
                affected++;
                summary.Add($"{commanderTarget.characterName} +1 Commander");
            }

            if (agentTarget != null)
            {
                agentTarget.AddAgent(1);
                affected++;
                summary.Add($"{agentTarget.characterName} +1 Agent");
            }

            if (emmissaryTarget != null)
            {
                emmissaryTarget.AddEmmissary(1);
                affected++;
                summary.Add($"{emmissaryTarget.characterName} +1 Emmissary");
            }

            if (affected == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Grey Company rallies {affected} Dunedain: {string.Join(", ", summary)}.", Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, greyCompanyAsync);
    }
}
