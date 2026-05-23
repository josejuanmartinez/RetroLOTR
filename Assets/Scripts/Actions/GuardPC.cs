using System;
using UnityEngine;

public class GuardPC : AgentAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex?.GetPC();
            if (pc == null) return false;
            pc.SetGuard(c.GetAgent());
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"{pc.pcName} is guarded ({c.GetAgent() * 10}% harder to attack).", Color.green);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            PC pc = c.hex?.GetPC();
            return pc != null && pc.owner == c.GetOwner();
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
