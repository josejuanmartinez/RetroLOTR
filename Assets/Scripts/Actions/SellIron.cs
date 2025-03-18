using System;

public class SellIron : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.goldAmount += 10;
            if(playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
