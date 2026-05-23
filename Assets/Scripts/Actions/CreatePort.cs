using System;
using UnityEngine;

public class CreatePort : CommanderPCAction
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
            if (pc == null || pc.hasPort) return false;

            pc.hasPort = true;
            MessageDisplayNoUI.ShowMessage(pc.hex, actor, $"A port was built at {pc.pcName}.", Color.cyan);
            pc.hex.RedrawPC();
            pc.hex.RedrawArmies();
            pc.hex.RedrawCharacters();
            return true;
        };

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            PC pc = actor.hex.GetPC();
            if (pc == null || pc.hasPort || pc.owner == null) return false;
            if (pc.owner != actor.GetOwner()) return false;
            return actor.GetCommander() >= 2;
        };

        asyncEffect = async (actor) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
