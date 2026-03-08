using System;
using UnityEngine;

public class BuyLeather : EmmissaryPCAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Militaristic;

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        isBuyCaravans = true;
        isSellCaravans = false;
        leatherCost = 5; // quantity purchased
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        goldCost = 0;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            if(playable == null) return false;
            StoresManager stores = FindFirstObjectByType<StoresManager>();
            if (stores == null) return false;
            int quantity = 5;
            int totalCost = stores.GetBuyPrice(ProducesEnum.leather, quantity);
            if (!stores.HasStock(ProducesEnum.leather, quantity)) return false;
            if (playable.goldAmount < totalCost) return false;

            playable.RemoveGold(totalCost);
            playable.AddLeather(quantity);
            stores.AdjustStock(ProducesEnum.leather, -quantity);
            if (playable == FindFirstObjectByType<Game>().player) FindFirstObjectByType<StoresManager>().RefreshStores();
            return true; 
        };
        condition = (c) => {
            if (c == null) return false;
            StoresManager stores = FindFirstObjectByType<StoresManager>();
            if (stores == null) return false;
            PlayableLeader playable = (c.GetOwner() as PlayableLeader);
            int quantity = 5;
            int totalCost = stores.GetBuyPrice(ProducesEnum.leather, quantity);
            return stores.HasStock(ProducesEnum.leather, quantity)
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

