using System;

public class StealMithril : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.mithrilAmount += 5;
            c.hex.GetPC().owner.mithrilAmount -= 5;
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (c.hex.GetPC() != null && c.hex.GetPC().mithril > 0 && c.hex.GetPC().owner.mithrilAmount >= 5 && (originalCondition == null || originalCondition(c)));
        };
        base.Initialize(c, condition, effect);
    }
}
