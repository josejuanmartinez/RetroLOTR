using System;
using UnityEngine;

public class SabotagePort : AgentPCAction
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
            if (pc == null || !pc.hasPort) return false;

            pc.hasPort = false;
            RemoveWarshipsFromPcOwner(pc);
            MessageDisplayNoUI.ShowMessage(pc.hex, actor, $"{pc.pcName} port sabotaged!", Color.red);
            pc.hex.RedrawPC();
            pc.hex.RedrawArmies();
            pc.hex.RedrawCharacters();
            return true;
        };

        condition = (actor) =>
        {
            if (originalCondition != null && !originalCondition(actor)) return false;
            PC pc = actor.hex.GetPC();
            return pc != null && pc.hasPort && actor.GetAgent() >= 4;
        };

        asyncEffect = async (actor) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(actor)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }

    private static void RemoveWarshipsFromPcOwner(PC pc)
    {
        if (pc == null || pc.hex == null || pc.owner == null) return;
        foreach (Army army in pc.hex.armies)
        {
            if (army == null || army.commander == null) continue;
            if (army.commander.GetOwner() != pc.owner) continue;
            if (army.ws > 0) army.ws = 0;
        }
    }
}
