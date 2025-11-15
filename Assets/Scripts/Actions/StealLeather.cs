using System;
using UnityEngine;

public class StealLeather : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PC pc = c.hex.GetPC();
            if (pc == null) return false;
            int toSteal = Math.Min(pc.owner.leatherAmount, UnityEngine.Random.Range(1, c.GetAgent()));
            if (toSteal < 1) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.AddLeather(toSteal);
            pc.owner.RemoveLeather(toSteal);
            MessageDisplayNoUI.ShowMessage(pc.hex, c, $"-{toSteal} <sprite name=\"leather\"/> stolen!", Color.red);
            MessageDisplay.ShowMessage($"+{toSteal} <sprite name=\"leather\"/> stolen!", Color.green);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (c.hex.GetPC() != null && c.hex.GetPC().leather > 0 && (originalCondition == null || originalCondition(c)));
        };
        base.Initialize(c, condition, effect);
    }
}
