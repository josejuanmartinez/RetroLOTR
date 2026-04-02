using System;
using System.Collections.Generic;
using System.Linq;

public class ReleaseCharacter : AgentAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            return FindReleaseTarget(actor) != null;
        };

        effect = (actor) => true;

        async System.Threading.Tasks.Task<bool> releaseAsync(Character actor)
        {
            if (originalEffect != null && !originalEffect(actor)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;

            List<Character> captives = actor.GetActiveCaptives()
                .Where(target => actor.CanReleaseCaptive(target))
                .ToList();
            if (captives.Count < 1) return false;

            Character target = null;
            bool isAI = !actor.isPlayerControlled;
            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Select captive to release",
                    "Ok",
                    "Cancel",
                    captives.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(actor) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = captives.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = FindReleaseTarget(actor);
            }

            return target != null && actor.ReleaseCaptive(target);
        }

        base.Initialize(c, condition, effect, releaseAsync);
    }

    private Character FindReleaseTarget(Character actor)
    {
        return actor.GetActiveCaptives()
            .Where(target => actor.CanReleaseCaptive(target))
            .OrderBy(target => target.GetKidnapRansomValue())
            .FirstOrDefault();
    }
}
