using System;
using UnityEngine;

public class StealIron : AgentPCAction
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
            int toSteal = Math.Min(pc.owner.ironAmount, UnityEngine.Random.Range(1, c.GetAgent()));
            if (toSteal < 1) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.AddIron(toSteal);
            pc.owner.RemoveIron(toSteal);
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"-{toSteal} <sprite name=\"iron\"/> stolen!", Color.red);
            MessageDisplay.ShowMessage($"+{toSteal} <sprite name=\"iron\"/> stolen!", Color.green);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return true; 
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return ( c.hex.GetPC() != null && c.hex.GetPC().iron > 0);
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

