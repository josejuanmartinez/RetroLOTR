using System;

public class StealMithril : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        PlayableLeader playable = (c.GetOwner() as PlayableLeader);
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            playable.mithrilAmount += 5;
            c.hex.pc.owner.mithrilAmount -= 5;
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (c.hex.pc != null && c.hex.pc.mithril > 0 && c.hex.pc.owner.mithrilAmount >= 5 && (originalCondition == null || originalCondition(c)));
        };
        base.Initialize(c, condition, effect);
    }
}
