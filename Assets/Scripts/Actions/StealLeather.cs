using System;

public class StealLeather : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.leatherAmount += 5;
            c.hex.GetPC().owner.leatherAmount -= 5;
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (c.hex.GetPC() != null && c.hex.GetPC().leather > 0 && c.hex.GetPC().owner.leatherAmount >= 5 && (originalCondition == null || originalCondition(c)));
        };
        base.Initialize(c, condition, effect);
    }
}
