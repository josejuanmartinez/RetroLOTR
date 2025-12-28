using System;
using UnityEngine;

public class RemoveFortifications : CommanderPCAction
{
    public override void Initialize(
        Character c,
        Func<Character, bool> condition = null,
        Func<Character, bool> effect = null,
        Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (actor) =>
        {
            if (originalEffect != null && !originalEffect(actor)) return false;
            PC pc = actor.hex.GetPC();
            if (pc == null || pc.fortSize <= FortSizeEnum.NONE) return false;

            pc.DecreaseFort();
            MessageDisplayNoUI.ShowMessage(pc.hex, actor, $"Fortifications at {pc.pcName} were reduced.", Color.yellow);
            return true;
        };

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            PC pc = actor.hex.GetPC();
            return pc != null && pc.fortSize > FortSizeEnum.NONE;
        };

        asyncEffect = async (actor) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
