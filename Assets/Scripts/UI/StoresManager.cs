using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UI;

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
    
    public Image leatherImage;
    public Image mountsImage;
    public Image timberImage;
    public Image ironImage;
    public Image steelImage;
    public Image mithrilImage;
    public Image goldImage;

    public TextMeshProUGUI leatherAmount;
    public TextMeshProUGUI mountsAmount;
    public TextMeshProUGUI timberAmount;
    public TextMeshProUGUI ironAmount;
    public TextMeshProUGUI steelAmount;
    public TextMeshProUGUI mithrilAmount;
    public TextMeshProUGUI goldAmount;

    [Header("Gain Animation")]
    [SerializeField] private float gainPulseDuration = 0.9f;
    [SerializeField] private float gainPulseHoldDuration = 0.18f;
    [SerializeField] private float gainPulseScaleMultiplier = 1.32f;
    [SerializeField] private Color gainPulseColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private float gainImagePulseScaleMultiplier = 1.42f;
    [SerializeField] private Color gainImagePulseColor = new Color(0.7f, 1f, 0.7f, 1f);

    private readonly Dictionary<ProducesEnum, ResourceMarketState> market = new();
    private readonly Dictionary<TextMeshProUGUI, Coroutine> gainPulseCoroutines = new();
    private readonly Dictionary<TextMeshProUGUI, Color> defaultTextColors = new();
    private readonly Dictionary<TextMeshProUGUI, Vector3> defaultTextScales = new();
    private readonly Dictionary<Image, Coroutine> gainImagePulseCoroutines = new();
    private readonly Dictionary<Image, Color> defaultImageColors = new();
    private readonly Dictionary<Image, Vector3> defaultImageScales = new();
    private bool marketInitialized = false;
    private int turnCounter = 0;
    private bool pricePopupShownThisFrame = false;
    private int shownLeather = int.MinValue;
    private int shownMounts = int.MinValue;
    private int shownTimber = int.MinValue;
    private int shownIron = int.MinValue;
    private int shownSteel = int.MinValue;
    private int shownMithril = int.MinValue;
    private int shownGold = int.MinValue;

    public event Action MarketChanged;

    private void Awake()
    {
        EnsureMarketInitialized();
    }

    private void OnDisable()
    {
        foreach (var entry in gainPulseCoroutines)
        {
            if (entry.Value != null) StopCoroutine(entry.Value);
        }
        gainPulseCoroutines.Clear();
        foreach (var entry in gainImagePulseCoroutines)
        {
            if (entry.Value != null) StopCoroutine(entry.Value);
        }
        gainImagePulseCoroutines.Clear();

        RestoreDefaultVisual(leatherAmount);
        RestoreDefaultVisual(mountsAmount);
        RestoreDefaultVisual(timberAmount);
        RestoreDefaultVisual(ironAmount);
        RestoreDefaultVisual(steelAmount);
        RestoreDefaultVisual(mithrilAmount);
        RestoreDefaultVisual(goldAmount);
        RestoreDefaultImageVisual(leatherImage);
        RestoreDefaultImageVisual(mountsImage);
        RestoreDefaultImageVisual(timberImage);
        RestoreDefaultImageVisual(ironImage);
        RestoreDefaultImageVisual(steelImage);
        RestoreDefaultImageVisual(mithrilImage);
        RestoreDefaultImageVisual(goldImage);
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
        if (playableLeader == null) return;

        RefreshResourceLabel(leatherAmount, playableLeader.leatherAmount, ref shownLeather);
        RefreshResourceLabel(mountsAmount, playableLeader.mountsAmount, ref shownMounts);
        RefreshResourceLabel(timberAmount, playableLeader.timberAmount, ref shownTimber);
        RefreshResourceLabel(ironAmount, playableLeader.ironAmount, ref shownIron);
        RefreshResourceLabel(steelAmount, playableLeader.steelAmount, ref shownSteel);
        RefreshResourceLabel(mithrilAmount, playableLeader.mithrilAmount, ref shownMithril);
        RefreshResourceLabel(goldAmount, playableLeader.goldAmount, ref shownGold);
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

    private void NotifyMarketChanged(bool pricesChanged)
    {
        MarketChanged?.Invoke();
        if (!pricesChanged) return;

        if (pricePopupShownThisFrame) return;
        pricePopupShownThisFrame = true;
        MessageDisplay.ShowMessage("Prices in the caravans changed. Please check the <sprite name=\"caravans\"> icon above.", Color.yellow);
        pricePopupShownThisFrame = false;
    }

    private void RefreshResourceLabel(TextMeshProUGUI label, int value, ref int shownValue)
    {
        if (label == null) return;
        CacheDefaults(label);
        shownValue = value;
        label.text = value.ToString();
    }

    public void PulseResourceGain(ProducesEnum resourceType, int delta)
    {
        if (delta <= 0) return;
        TextMeshProUGUI label = GetResourceLabel(resourceType);
        Image image = GetResourceImage(resourceType);
        PlayGainPulse(label, delta);
        PlayImageGainPulse(image, delta);
    }

    public void PulseGoldGain(int delta)
    {
        if (delta <= 0) return;
        PlayGainPulse(goldAmount, delta);
        PlayImageGainPulse(goldImage, delta);
    }

    private void CacheDefaults(TextMeshProUGUI label)
    {
        if (label == null) return;
        if (!defaultTextColors.ContainsKey(label))
        {
            defaultTextColors[label] = label.color;
        }
        if (!defaultTextScales.ContainsKey(label))
        {
            defaultTextScales[label] = label.transform.localScale;
        }
    }

    private void RestoreDefaultVisual(TextMeshProUGUI label)
    {
        if (label == null) return;
        if (defaultTextColors.TryGetValue(label, out Color color))
        {
            label.color = color;
        }
        if (defaultTextScales.TryGetValue(label, out Vector3 scale))
        {
            label.transform.localScale = scale;
        }
    }

    private void CacheImageDefaults(Image image)
    {
        if (image == null) return;
        if (!defaultImageColors.ContainsKey(image))
        {
            defaultImageColors[image] = image.color;
        }
        if (!defaultImageScales.ContainsKey(image))
        {
            defaultImageScales[image] = image.transform.localScale;
        }
    }

    private void RestoreDefaultImageVisual(Image image)
    {
        if (image == null) return;
        if (defaultImageColors.TryGetValue(image, out Color color))
        {
            image.color = color;
        }
        if (defaultImageScales.TryGetValue(image, out Vector3 scale))
        {
            image.transform.localScale = scale;
        }
    }

    private void PlayGainPulse(TextMeshProUGUI label, int delta)
    {
        if (label == null) return;
        if (gainPulseCoroutines.TryGetValue(label, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }

        gainPulseCoroutines[label] = StartCoroutine(AnimateGainPulse(label, delta));
    }

    private void PlayImageGainPulse(Image image, int delta)
    {
        if (image == null) return;
        if (gainImagePulseCoroutines.TryGetValue(image, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }

        gainImagePulseCoroutines[image] = StartCoroutine(AnimateImageGainPulse(image, delta));
    }

    private System.Collections.IEnumerator AnimateGainPulse(TextMeshProUGUI label, int delta)
    {
        if (label == null) yield break;
        CacheDefaults(label);
        Color baseColor = defaultTextColors[label];
        Vector3 baseScale = defaultTextScales[label];

        float amountBoost = Mathf.Clamp(delta, 1, 8) * 0.02f;
        Vector3 peakScale = baseScale * (gainPulseScaleMultiplier + amountBoost);
        float upDownDuration = Mathf.Max(0.01f, gainPulseDuration);
        float upDuration = upDownDuration * 0.35f;
        float downDuration = upDownDuration * 0.65f;
        float elapsed = 0f;

        while (elapsed < upDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / upDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            label.transform.localScale = Vector3.Lerp(baseScale, peakScale, eased);
            label.color = Color.Lerp(baseColor, gainPulseColor, eased);
            yield return null;
        }

        if (gainPulseHoldDuration > 0f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < gainPulseHoldDuration)
            {
                holdElapsed += Time.unscaledDeltaTime;
                label.transform.localScale = peakScale;
                label.color = gainPulseColor;
                yield return null;
            }
        }

        elapsed = 0f;
        while (elapsed < downDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / downDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            label.transform.localScale = Vector3.Lerp(peakScale, baseScale, eased);
            label.color = Color.Lerp(gainPulseColor, baseColor, eased);
            yield return null;
        }

        label.transform.localScale = baseScale;
        label.color = baseColor;
        gainPulseCoroutines[label] = null;
    }

    private System.Collections.IEnumerator AnimateImageGainPulse(Image image, int delta)
    {
        if (image == null) yield break;
        CacheImageDefaults(image);
        Color baseColor = defaultImageColors[image];
        Vector3 baseScale = defaultImageScales[image];

        float amountBoost = Mathf.Clamp(delta, 1, 8) * 0.02f;
        Vector3 peakScale = baseScale * (gainImagePulseScaleMultiplier + amountBoost);
        float upDownDuration = Mathf.Max(0.01f, gainPulseDuration);
        float upDuration = upDownDuration * 0.35f;
        float downDuration = upDownDuration * 0.65f;
        float elapsed = 0f;

        while (elapsed < upDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / upDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            image.transform.localScale = Vector3.Lerp(baseScale, peakScale, eased);
            image.color = Color.Lerp(baseColor, gainImagePulseColor, eased);
            yield return null;
        }

        if (gainPulseHoldDuration > 0f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < gainPulseHoldDuration)
            {
                holdElapsed += Time.unscaledDeltaTime;
                image.transform.localScale = peakScale;
                image.color = gainImagePulseColor;
                yield return null;
            }
        }

        elapsed = 0f;
        while (elapsed < downDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / downDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            image.transform.localScale = Vector3.Lerp(peakScale, baseScale, eased);
            image.color = Color.Lerp(gainImagePulseColor, baseColor, eased);
            yield return null;
        }

        image.transform.localScale = baseScale;
        image.color = baseColor;
        gainImagePulseCoroutines[image] = null;
    }

    private TextMeshProUGUI GetResourceLabel(ProducesEnum resourceType)
    {
        return resourceType switch
        {
            ProducesEnum.leather => leatherAmount,
            ProducesEnum.mounts => mountsAmount,
            ProducesEnum.timber => timberAmount,
            ProducesEnum.iron => ironAmount,
            ProducesEnum.steel => steelAmount,
            ProducesEnum.mithril => mithrilAmount,
            _ => null
        };
    }

    private Image GetResourceImage(ProducesEnum resourceType)
    {
        return resourceType switch
        {
            ProducesEnum.leather => leatherImage,
            ProducesEnum.mounts => mountsImage,
            ProducesEnum.timber => timberImage,
            ProducesEnum.iron => ironImage,
            ProducesEnum.steel => steelImage,
            ProducesEnum.mithril => mithrilImage,
            _ => null
        };
    }
}
