using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CaravansManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public StoresManager storesManager;
    public GameObject caravans;
    public TextMeshProUGUI leatherStock;
    public TextMeshProUGUI ironStock;
    public TextMeshProUGUI steelStock;
    public TextMeshProUGUI mithrilStock;
    public TextMeshProUGUI timberStock;
    public TextMeshProUGUI mountsStock;

    public TextMeshProUGUI leatherBuyPrice;
    public TextMeshProUGUI ironBuyPrice;
    public TextMeshProUGUI steelBuyPrice;
    public TextMeshProUGUI mithrilBuyPrice;
    public TextMeshProUGUI timberBuyPrice;
    public TextMeshProUGUI mountsBuyPrice;
    public TextMeshProUGUI leatherSellPrice;
    public TextMeshProUGUI ironSellPrice;
    public TextMeshProUGUI steelSellPrice;
    public TextMeshProUGUI mithrilSellPrice;
    public TextMeshProUGUI timberSellPrice;
    public TextMeshProUGUI mountsSellPrice;
   
    void Start()
    {
        EnsureStoresManager();
    }

    void OnEnable()
    {
        EnsureStoresManager();
        if (storesManager != null) storesManager.MarketChanged += RefreshMarketTexts;
        RefreshMarketTexts();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PopupManager.IsShowing) return;
        Sounds.Instance?.PlayUiHover();
        caravans.SetActive(true);
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        Sounds.Instance?.PlayUiExit();
        caravans.SetActive(false);
    }

    void OnDisable()
    {
        if (storesManager != null) storesManager.MarketChanged -= RefreshMarketTexts;
    }

    private void EnsureStoresManager()
    {
        if (!storesManager) storesManager = FindFirstObjectByType<StoresManager>();
    }

    public void RefreshMarketTexts()
    {
        if (storesManager == null) return;

        SetStockAndPrices(ProducesEnum.leather, leatherStock, leatherBuyPrice, leatherSellPrice);
        SetStockAndPrices(ProducesEnum.iron, ironStock, ironBuyPrice, ironSellPrice);
        SetStockAndPrices(ProducesEnum.steel, steelStock, steelBuyPrice, steelSellPrice);
        SetStockAndPrices(ProducesEnum.mithril, mithrilStock, mithrilBuyPrice, mithrilSellPrice);
        SetStockAndPrices(ProducesEnum.timber, timberStock, timberBuyPrice, timberSellPrice);
        SetStockAndPrices(ProducesEnum.mounts, mountsStock, mountsBuyPrice, mountsSellPrice);
    }

    private void SetStockAndPrices(ProducesEnum resource, TextMeshProUGUI stockText, TextMeshProUGUI buyText, TextMeshProUGUI sellText)
    {
        if (stockText != null)
        {
            string spriteName = resource.ToString().ToLowerInvariant();
            stockText.text = $"<sprite name=\"{spriteName}\">{storesManager.GetCurrentStock(resource)}";
        }
        if (buyText != null) buyText.text = storesManager.GetBuyPricePerUnit(resource).ToString();
        if (sellText != null) sellText.text = storesManager.GetSellPricePerUnit(resource).ToString();
    }
}
