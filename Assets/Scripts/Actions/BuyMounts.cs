using System;
using UnityEngine;

public class BuyMounts : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.AddMounts(5);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
