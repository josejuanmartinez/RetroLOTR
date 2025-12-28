using System;
using UnityEngine;

public class StealGold : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            int toSteal = Math.Min(pc.owner.goldAmount, UnityEngine.Random.Range(1, c.GetAgent()));
            if (toSteal < 1) return false;
            Leader actorOwner = c.GetOwner();
            if (actorOwner == null) return false;
            if (actorOwner == FindFirstObjectByType<Game>().player)
            {
                actorOwner.AddGold(toSteal);
            }
            else
            {
                actorOwner.goldAmount += toSteal;
            }
            pc.owner.RemoveGold(toSteal);
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"-{toSteal} <sprite name=\"gold\"/> stolen!", Color.red);
            if (actorOwner == FindFirstObjectByType<Game>().player)
            {
                MessageDisplay.ShowMessage($"+{toSteal} <sprite name=\"gold\"/> stolen!", Color.green);
                FindFirstObjectByType<StoresManager>().RefreshStores();
            }
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.hex.GetPC() != null;
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

