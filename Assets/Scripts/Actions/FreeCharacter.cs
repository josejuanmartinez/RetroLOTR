using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FreeCharacter : AgentAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            return FindRescueTarget(actor) != null;
        };

        effect = (actor) => true;

        async System.Threading.Tasks.Task<bool> freeAsync(Character actor)
        {
            if (originalEffect != null && !originalEffect(actor)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;

            List<Character> captives = FindFriendlyCaptives(actor);
            if (captives.Count < 1) return false;

            Character target = null;
            bool isAI = !actor.isPlayerControlled;
            if (!isAI)
            {
                List<string> options = captives
                    .Select(x => $"{x.characterName} held by {x.kidnappedBy.characterName}")
                    .ToList();
                string selected = await SelectionDialog.Ask(
                    "Select captive to free",
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
                target = FindRescueTarget(actor);
            }

            if (target == null || target.kidnappedBy == null) return false;

            Character kidnapper = target.kidnappedBy;
            int rescueRoll = actor.GetAgent() + UnityEngine.Random.Range(0, 6) + Mathf.Max(0, actor.GetCommander() / 2);
            int guardRoll = Mathf.Max(1, kidnapper.GetAgent()) + UnityEngine.Random.Range(0, 6) + Mathf.Max(kidnapper.GetCommander(), kidnapper.GetMage(), kidnapper.GetEmmissary()) / 2;
            if (kidnapper.GetAgent() > 0) guardRoll += 2;

            if (rescueRoll > guardRoll)
            {
                target.ReleaseFromKidnap(false);
                MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{actor.characterName} freed {target.characterName} from {kidnapper.characterName}!", Color.green);
                return true;
            }

            int margin = guardRoll - rescueRoll;
            if (margin >= 4)
            {
                int killThreshold = 3 + Mathf.Max(0, kidnapper.GetAgent());
                int killRoll = UnityEngine.Random.Range(0, 10);
                if (killRoll < killThreshold)
                {
                    MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{actor.characterName} was slain while trying to free {target.characterName}!", Color.red);
                    actor.Killed(kidnapper.GetOwner());
                }
                else
                {
                    int woundDamage = Mathf.Clamp(15 + margin * 10, 20, 90);
                    MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{actor.characterName} was caught trying to free {target.characterName}.", Color.red);
                    actor.Wounded(kidnapper.GetOwner(), woundDamage);
                }
            }

            return false;
        }

        base.Initialize(c, condition, effect, freeAsync);
    }

    private List<Character> FindFriendlyCaptives(Character actor)
    {
        if (actor == null || actor.hex == null || actor.GetOwner() == null) return new List<Character>();

        return actor.hex.characters
            .Where(target =>
                target != null
                && !target.killed
                && target != actor
                && target.IsKidnapped()
                && target.kidnappedBy != null
                && target.kidnappedBy.GetOwner() != actor.GetOwner()
                && target.GetOwner() == actor.GetOwner())
            .ToList();
    }

    private Character FindRescueTarget(Character actor)
    {
        return FindFriendlyCaptives(actor)
            .OrderByDescending(target => target.GetTotalSkillLevel())
            .ThenByDescending(target => target.GetAgent())
            .FirstOrDefault();
    }
}
