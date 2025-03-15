using System;

public class BuyLeather : EmmissaryPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        PlayableLeader playable = (c.GetOwner() as PlayableLeader);
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            playable.leatherAmount += 5;
            if(playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c));
        };
        base.Initialize(c, condition, effect);
    }
}
