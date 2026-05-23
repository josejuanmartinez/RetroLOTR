using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class GuardCharacter : AgentAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return GetFriendlyTargets(c).Count > 0;
        };

        effect = (c) => true;

        async Task<bool> guardAsync(Character c)
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;

            List<Character> targets = GetFriendlyTargets(c);
            if (targets.Count < 1) return false;

            bool isAI = !c.isPlayerControlled;
            Character target = null;
            if (!isAI)
            {
                string choice = await SelectionDialog.Ask("Select character to guard", "Ok", "Cancel", targets.Select(x => x.characterName).ToList(), isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
                if (string.IsNullOrWhiteSpace(choice)) return false;
                target = targets.Find(x => x.characterName == choice);
            }
            else
            {
                target = targets.OrderByDescending(x => x.GetMage() + x.GetCommander() + x.GetEmmissary()).FirstOrDefault();
            }

            if (target == null) return false;

            target.guardLevel = Mathf.Max(target.guardLevel, c.GetAgent());
            target.ApplyStatusEffect(StatusEffectEnum.Guarded, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{target.characterName} is guarded ({c.GetAgent() * 10}% harder to target).", Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, guardAsync);
    }

    private List<Character> GetFriendlyTargets(Character c)
    {
        if (c?.hex?.characters == null) return new List<Character>();
        Leader owner = c.GetOwner();
        return c.hex.characters
            .Where(x => x != null && !x.killed && x.GetOwner() == owner)
            .ToList();
    }
}
