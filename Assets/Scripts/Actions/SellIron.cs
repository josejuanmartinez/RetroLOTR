using System;

public class SellIron : EmmissaryPCAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Economic;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        isSellCaravans = true;
        isBuyCaravans = false;
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if (playable == null) return false;
            StoresManager stores = FindFirstObjectByType<StoresManager>();
            if (stores == null) return false;
            int quantity = 5;
            int payout = stores.GetSellPrice(ProducesEnum.iron, quantity);
            stores.AdjustStock(ProducesEnum.iron, quantity);
            if(playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return true; 
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c));
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

