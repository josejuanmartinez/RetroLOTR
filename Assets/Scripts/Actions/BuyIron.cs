using System;
using UnityEngine;

public class BuyIron : EmmissaryPCAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Militaristic;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        isBuyCaravans = true;
        isSellCaravans = false;
        ironCost = 5; // quantity purchased
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        goldCost = 0;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if(c.GetOwner() is not PlayableLeader) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            StoresManager stores = FindFirstObjectByType<StoresManager>();
            if (stores == null) return false;
            int quantity = 5;
            int totalCost = stores.GetBuyPrice(ProducesEnum.iron, quantity);
            if (!stores.HasStock(ProducesEnum.iron, quantity)) return false;
            if (playable.goldAmount < totalCost) return false;

            playable.AddIron(quantity);
            stores.AdjustStock(ProducesEnum.iron, -quantity);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return true; 
        };
        condition = (c) => {
            if (c == null) return false;
            StoresManager stores = FindFirstObjectByType<StoresManager>();
            if (stores == null) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            int quantity = 5;
            int totalCost = stores.GetBuyPrice(ProducesEnum.iron, quantity);
            return stores.HasStock(ProducesEnum.iron, quantity)
                && playable != null
                && playable.goldAmount >= totalCost
                && (originalCondition == null || originalCondition(c));
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

