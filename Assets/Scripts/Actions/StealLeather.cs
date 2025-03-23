using System;

public class StealLeather : AgentPCAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.GetPC() == null) return false;
            int toSteal = Math.Min(c.hex.GetPC().owner.leatherAmount, UnityEngine.Random.Range(1, c.GetAgent()));
            if (toSteal < 1) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            playable.AddLeather(toSteal);
            c.hex.GetPC().owner.RemoveLeather(toSteal);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return originalEffect == null || originalEffect(c); 
        };
        condition = (c) => {
            return (c.hex.GetPC() != null && c.hex.GetPC().leather > 0 && (originalCondition == null || originalCondition(c)));
        };
        base.Initialize(c, condition, effect);
    }
}
