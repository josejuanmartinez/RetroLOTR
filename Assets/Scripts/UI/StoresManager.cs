using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Analytics;

public class StoresManager : MonoBehaviour
{
    // Gold gained per unit when selling resources
    public const int LeatherSellValue = 1;
    public const int TimberSellValue = 2;
    public const int IronSellValue = 3;
    public const int SteelSellValue = 4;
    public const int MountsSellValue = 3;
    public const int MithrilSellValue = 7;

    private class ResourceMarketState
    {
        public readonly int BaseSellValue;
        public readonly int ReferenceStock;
        public int CurrentStock;
        public readonly string SpriteName;

        public ResourceMarketState(string spriteName, int baseSellValue, int referenceStock)
        {
            SpriteName = spriteName;
            BaseSellValue = baseSellValue;
            ReferenceStock = referenceStock;
            CurrentStock = referenceStock;
        }
    }

    public TextMeshProUGUI leatherAmount;
    public TextMeshProUGUI mountsAmount;
    public TextMeshProUGUI timberAmount;
    public TextMeshProUGUI ironAmount;
    public TextMeshProUGUI steelAmount;
    public TextMeshProUGUI mithrilAmount;
    public TextMeshProUGUI goldAmount;

    private readonly Dictionary<ProducesEnum, ResourceMarketState> market = new();
    private bool marketInitialized = false;
    private int turnCounter = 0;
    private bool pricePopupShownThisFrame = false;

    public event Action MarketChanged;

    private void Awake()
    {
        EnsureMarketInitialized();
    }

    private void EnsureMarketInitialized()
    {
        if (marketInitialized) return;

        market[ProducesEnum.leather] = new ResourceMarketState("leather", LeatherSellValue, 25);
        market[ProducesEnum.timber] = new ResourceMarketState("timber", TimberSellValue, 25);
        market[ProducesEnum.iron] = new ResourceMarketState("iron", IronSellValue, 25);
        market[ProducesEnum.steel] = new ResourceMarketState("steel", SteelSellValue, 25);
        market[ProducesEnum.mounts] = new ResourceMarketState("mounts", MountsSellValue, 25);
        market[ProducesEnum.mithril] = new ResourceMarketState("mithril", MithrilSellValue, 10);

        marketInitialized = true;
    }

    public int GetCurrentStock(ProducesEnum resourceType)
    {
        EnsureMarketInitialized();
        return market.TryGetValue(resourceType, out var state) ? state.CurrentStock : 0;
    }

    public bool HasStock(ProducesEnum resourceType, int quantity)
    {
        return GetCurrentStock(resourceType) >= quantity;
    }

    private float GetSupplyFactor(ProducesEnum resourceType)
    {
        EnsureMarketInitialized();
        ResourceMarketState state = market[resourceType];
        return (float)state.ReferenceStock / Mathf.Max(1, state.CurrentStock);
    }

    public int GetSellPricePerUnit(ProducesEnum resourceType)
    {
        EnsureMarketInitialized();
        ResourceMarketState state = market[resourceType];
        float supplyFactor = GetSupplyFactor(resourceType);
        int price = Mathf.CeilToInt(state.BaseSellValue * supplyFactor);
        return Mathf.Max(1, price);
    }

    public int GetSellPrice(ProducesEnum resourceType, int quantity)
    {
        return GetSellPricePerUnit(resourceType) * quantity;
    }

    public int GetBuyPricePerUnit(ProducesEnum resourceType)
    {
        EnsureMarketInitialized();
        ResourceMarketState state = market[resourceType];
        int baseBuyPrice = Mathf.CeilToInt(state.BaseSellValue * 1.25f);
        float supplyFactor = GetSupplyFactor(resourceType);
        int price = Mathf.CeilToInt(baseBuyPrice * supplyFactor);
        return Mathf.Max(1, price);
    }

    public int GetBuyPrice(ProducesEnum resourceType, int quantity)
    {
        return GetBuyPricePerUnit(resourceType) * quantity;
    }

    public void AdjustStock(ProducesEnum resourceType, int delta)
    {
        EnsureMarketInitialized();
        if (!market.ContainsKey(resourceType)) return;
        if (market[resourceType].CurrentStock > 50) return;
        int oldBuyPrice = GetBuyPricePerUnit(resourceType);
        int oldSellPrice = GetSellPricePerUnit(resourceType);

        market[resourceType].CurrentStock += delta;

        int newBuyPrice = GetBuyPricePerUnit(resourceType);
        int newSellPrice = GetSellPricePerUnit(resourceType);

        bool pricesChanged = oldBuyPrice != newBuyPrice || oldSellPrice != newSellPrice;
        NotifyMarketChanged(pricesChanged);
    }

    public void RefreshStores()
    {
        EnsureMarketInitialized();
        PlayableLeader playableLeader = FindFirstObjectByType<Game>().player;
        int leatherProduction = playableLeader.GetLeatherPerTurn();
        int mountsProduction = playableLeader.GetMountsPerTurn();
        int timberProduction = playableLeader.GetTimberPerTurn();
        int ironProduction = playableLeader.GetIronPerTurn();
        int steelProduction = playableLeader.GetSteelPerTurn();
        int mithrilProduction = playableLeader.GetMithrilPerTurn();
        int goldProduction = playableLeader.GetGoldPerTurn();

        leatherAmount.text = $"{playableLeader.leatherAmount}<br>{(leatherProduction >= 0 ? "+" : "")}{leatherProduction}";
        mountsAmount.text = $"{playableLeader.mountsAmount}<br>{(mountsProduction >= 0 ? "+" : "")}{mountsProduction}";
        timberAmount.text = $"{playableLeader.timberAmount}<br>{(timberProduction >= 0 ? "+" : "")}{timberProduction}";
        ironAmount.text = $"{playableLeader.ironAmount}<br>{(ironProduction >= 0 ? "+" : "")}{ironProduction}";
        steelAmount.text = $"{playableLeader.steelAmount}<br>{(steelProduction >= 0 ? "+" : "")}{steelProduction}";
        mithrilAmount.text = $"{playableLeader.mithrilAmount}<br>{(mithrilProduction >= 0 ? "+" : "")}{mithrilProduction}";
        goldAmount.text =$"{playableLeader.goldAmount} <br> {(goldProduction >= 0 ? "+" : "")}{goldProduction}";
    }

    // Call this once per turn to add passive stock growth to the market
    public void AdvanceTurn()
    {
        EnsureMarketInitialized();
        turnCounter++;

        ReplenishIfLow(ProducesEnum.leather, 1);
        ReplenishIfLow(ProducesEnum.timber, 1);
        ReplenishIfLow(ProducesEnum.iron, 1);
        ReplenishIfLow(ProducesEnum.steel, 1);
        ReplenishIfLow(ProducesEnum.mounts, 1);

        if (turnCounter % 5 == 0)
        {
            ReplenishIfLow(ProducesEnum.mithril, 1);
        }
    }

    private void ReplenishIfLow(ProducesEnum resourceType, int amount)
    {
        if (!market.TryGetValue(resourceType, out ResourceMarketState state)) return;
        if (state.CurrentStock < 10)
        {
            AdjustStock(resourceType, amount);
        }
    }

    public string GetMarketSummary()
    {
        EnsureMarketInitialized();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Market stocks and prices:");
        sb.AppendLine();

        foreach (KeyValuePair<ProducesEnum, ResourceMarketState> kvp in market)
        {
            ProducesEnum type = kvp.Key;
            ResourceMarketState state = kvp.Value;
            int stock = state.CurrentStock;
            int sellPrice = GetSellPricePerUnit(type);
            int buyPrice = GetBuyPricePerUnit(type);
            sb.AppendLine($"<sprite name=\"{state.SpriteName}\">x{stock} - sell for <sprite name=\"gold\">{sellPrice} buy for <sprite name=\"gold\">{buyPrice}");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task ShowMarketDialog()
    {
        EnsureMarketInitialized();
        string summary = GetMarketSummary();
        await ConfirmationDialog.AskOk(summary);
    }

    // UI-friendly entry point
    public async void ShowMarketDialogFromUI()
    {
        await ShowMarketDialog();
    }

    private async void NotifyMarketChanged(bool pricesChanged)
    {
        MarketChanged?.Invoke();
        if (!pricesChanged) return;

        if (pricePopupShownThisFrame) return;
        pricePopupShownThisFrame = true;
        try
        {
            await ConfirmationDialog.AskOk("Prices in the caravans changed. Please check the <sprite name=\"caravans\"> icon above.");
        }
        finally
        {
            pricePopupShownThisFrame = false;
        }
    }
}
