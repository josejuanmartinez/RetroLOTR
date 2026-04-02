using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AskRansom : AgentAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            return FindRansomTarget(actor) != null;
        };

        effect = (actor) => true;

        async System.Threading.Tasks.Task<bool> ransomAsync(Character actor)
        {
            if (originalEffect != null && !originalEffect(actor)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;

            List<Character> captives = actor.GetActiveCaptives()
                .Where(target => actor.CanDemandRansom(target))
                .ToList();
            if (captives.Count < 1) return false;

            Character target = null;
            bool isAI = !actor.isPlayerControlled;
            if (!isAI)
            {
                List<string> options = captives
                    .Select(x => $"{x.characterName} [{x.GetKidnapRansomValue()} gold]")
                    .ToList();
                string selected = await SelectionDialog.Ask(
                    "Select captive for ransom demand",
                    "Ok",
                    "Cancel",
                    options,
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(actor) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = captives.FirstOrDefault(x => selected.StartsWith(x.characterName, StringComparison.Ordinal));
            }
            else
            {
                target = FindRansomTarget(actor);
            }

            if (target == null || !actor.CanDemandRansom(target)) return false;

            Leader targetOwner = target.kidnappedOriginalOwner != null ? target.kidnappedOriginalOwner : target.GetOwner();
            if (targetOwner == null || targetOwner.killed) return false;

            int ransomCost = target.GetKidnapRansomValue();
            bool accepts;

            if (!targetOwner.isPlayerControlled)
            {
                accepts = actor.ShouldAcceptRansom(target, ransomCost);
            }
            else
            {
                List<string> responseOptions = new() { $"Pay {ransomCost} gold", "Refuse ransom" };
                string answer = await SelectionDialog.Ask(
                    $"{actor.characterName} demands {ransomCost} gold to release {target.characterName}.",
                    "Choose",
                    "Decline",
                    responseOptions,
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(target) : null);
                accepts = string.Equals(answer, responseOptions[0], StringComparison.Ordinal);
            }

            if (!accepts)
            {
                MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{targetOwner.characterName} refused to pay ransom for {target.characterName}.", Color.yellow);
                return true;
            }

            if (targetOwner.goldAmount < ransomCost)
            {
                MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{targetOwner.characterName} cannot afford the ransom for {target.characterName}.", Color.yellow);
                return true;
            }

            targetOwner.RemoveGold(ransomCost, targetOwner == FindFirstObjectByType<Game>()?.player);
            actor.GetOwner()?.AddGold(ransomCost);
            actor.ReleaseCaptive(target);
            MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{targetOwner.characterName} paid {ransomCost} gold for {target.characterName}.", Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, ransomAsync);
    }

    private Character FindRansomTarget(Character actor)
    {
        return actor.GetActiveCaptives()
            .Where(target => actor.CanDemandRansom(target))
            .OrderByDescending(target => target.GetKidnapRansomValue())
            .ThenByDescending(target => target.GetTotalSkillLevel())
            .FirstOrDefault();
    }
}
