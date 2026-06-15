using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dramatic full-screen "TURN X" cinematic banner shown at the start of each player turn.
/// Self-creates its own Canvas. Call TurnBanner.Show(turnNumber, bannerSprite) to trigger.
/// </summary>
public class TurnBanner : MonoBehaviour
{
    public static TurnBanner Instance { get; private set; }

    private CanvasGroup rootGroup;
    private RectTransform topBarRect, bottomBarRect;
    private RectTransform lineLeftRect, lineRightRect;
    private TextMeshProUGUI turnText;
    private RectTransform textRect;
    private TextMeshProUGUI dateText;
    private RectTransform dateRect;
    private Image leftBannerImg, rightBannerImg;
    private RectTransform leftBannerRect, rightBannerRect;

    private const float BarHeight = 88f;
    private const float LineThickness = 3f;
    private const float LineMaxHalfWidth = 320f;
    private const float BannerWidth = 110f;
    private const float BannerHeight = 220f;
    private const float BannerRestX = 460f;
    private const float BannerStartX = 700f;
    private const float EnterDuration = 0.38f;
    private const float FlashDuration = 0.45f;
    private const float HoldDuration = 1.1f;
    private const float ExitDuration = 0.28f;

    private static readonly Color GoldColor = new(1f, 0.82f, 0.1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<TurnBanner>() != null) return;
        new GameObject("[TurnBanner]").AddComponent<TurnBanner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        rootGroup = gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        // Semi-transparent dim overlay
        var bgRect = MakeRect("Bg", transform);
        Stretch(bgRect);
        bgRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);

        // Top letterbox bar
        topBarRect = MakeRect("TopBar", transform);
        topBarRect.gameObject.AddComponent<Image>().color = Color.black;
        topBarRect.anchorMin = new Vector2(0, 1);
        topBarRect.anchorMax = new Vector2(1, 1);
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.sizeDelta = new Vector2(0, BarHeight);
        topBarRect.anchoredPosition = new Vector2(0, BarHeight);

        // Bottom letterbox bar
        bottomBarRect = MakeRect("BotBar", transform);
        bottomBarRect.gameObject.AddComponent<Image>().color = Color.black;
        bottomBarRect.anchorMin = new Vector2(0, 0);
        bottomBarRect.anchorMax = new Vector2(1, 0);
        bottomBarRect.pivot = new Vector2(0.5f, 0f);
        bottomBarRect.sizeDelta = new Vector2(0, BarHeight);
        bottomBarRect.anchoredPosition = new Vector2(0, -BarHeight);

        // Left banner — slides in from the left
        leftBannerRect = MakeRect("BannerL", transform);
        leftBannerImg = leftBannerRect.gameObject.AddComponent<Image>();
        leftBannerImg.preserveAspect = true;
        leftBannerImg.color = Color.white;
        leftBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        leftBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        leftBannerRect.pivot = new Vector2(0.5f, 0.5f);
        leftBannerRect.sizeDelta = new Vector2(BannerWidth, BannerHeight);
        leftBannerRect.anchoredPosition = new Vector2(-BannerStartX, 0f);

        // Right banner — slides in from the right (mirrored)
        rightBannerRect = MakeRect("BannerR", transform);
        rightBannerImg = rightBannerRect.gameObject.AddComponent<Image>();
        rightBannerImg.preserveAspect = true;
        rightBannerImg.color = Color.white;
        rightBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        rightBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        rightBannerRect.pivot = new Vector2(0.5f, 0.5f);
        rightBannerRect.sizeDelta = new Vector2(BannerWidth, BannerHeight);
        rightBannerRect.anchoredPosition = new Vector2(BannerStartX, 0f);
        rightBannerRect.localScale = new Vector3(-1f, 1f, 1f); // mirror horizontally

        // Center group
        var centerRect = MakeRect("Center", transform);
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(900f, 180f);
        centerRect.anchoredPosition = Vector2.zero;

        // Big turn text
        textRect = MakeRect("TurnText", centerRect);
        textRect.anchorMin = new Vector2(0f, 0.25f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        textRect.localScale = Vector3.zero;

        turnText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        turnText.fontSize = 100;
        turnText.fontStyle = FontStyles.Bold;
        turnText.color = GoldColor;
        turnText.alignment = TextAlignmentOptions.Center;

        // Decorative gold lines
        lineLeftRect = MakeRect("LineL", centerRect);
        lineLeftRect.gameObject.AddComponent<Image>().color = GoldColor;
        lineLeftRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineLeftRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineLeftRect.pivot = new Vector2(1f, 0.5f);
        lineLeftRect.sizeDelta = new Vector2(0f, LineThickness);
        lineLeftRect.anchoredPosition = new Vector2(-16f, -52f);

        lineRightRect = MakeRect("LineR", centerRect);
        lineRightRect.gameObject.AddComponent<Image>().color = GoldColor;
        lineRightRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRightRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRightRect.pivot = new Vector2(0f, 0.5f);
        lineRightRect.sizeDelta = new Vector2(0f, LineThickness);
        lineRightRect.anchoredPosition = new Vector2(16f, -52f);

        // Small date subtitle, below the gold lines
        dateRect = MakeRect("DateText", centerRect);
        dateRect.anchorMin = new Vector2(0.5f, 0.5f);
        dateRect.anchorMax = new Vector2(0.5f, 0.5f);
        dateRect.pivot = new Vector2(0.5f, 0.5f);
        dateRect.sizeDelta = new Vector2(760f, 44f);
        dateRect.anchoredPosition = new Vector2(0f, -84f);
        dateRect.localScale = Vector3.zero;

        dateText = dateRect.gameObject.AddComponent<TextMeshProUGUI>();
        dateText.fontSize = 34;
        dateText.fontStyle = FontStyles.Italic;
        dateText.color = GoldColor;
        dateText.alignment = TextAlignmentOptions.Center;
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    public static void Show(int turnNumber, Sprite bannerSprite = null)
    {
        if (Instance == null) return;
        Instance.StopAllCoroutines();
        Instance.StartCoroutine(Instance.PlayAnimation(turnNumber, bannerSprite));
        Sounds.Instance?.PlayPositive();
    }

    private IEnumerator PlayAnimation(int turnNumber, Sprite bannerSprite)
    {
        bool hasBanner = bannerSprite != null;

        turnText.text = $"TURN {turnNumber}";
        turnText.color = GoldColor;
        dateText.text = MiddleEarthCalendar.GetDateFromTurn(turnNumber).ToString();
        rootGroup.alpha = 1f;
        textRect.localScale = Vector3.zero;
        dateRect.localScale = Vector3.zero;
        topBarRect.anchoredPosition = new Vector2(0, BarHeight);
        bottomBarRect.anchoredPosition = new Vector2(0, -BarHeight);
        lineLeftRect.sizeDelta = new Vector2(0, LineThickness);
        lineRightRect.sizeDelta = new Vector2(0, LineThickness);

        // Reset banners
        leftBannerImg.sprite = bannerSprite;
        rightBannerImg.sprite = bannerSprite;
        Color bannerHidden = new(1f, 1f, 1f, 0f);
        leftBannerImg.color = bannerHidden;
        rightBannerImg.color = bannerHidden;
        leftBannerRect.anchoredPosition = new Vector2(-BannerStartX, 0f);
        rightBannerRect.anchoredPosition = new Vector2(BannerStartX, 0f);
        leftBannerImg.enabled = hasBanner;
        rightBannerImg.enabled = hasBanner;

        // Phase 1: bars slide in, text punches in, banners sweep in from sides
        float t = 0f;
        while (t < EnterDuration)
        {
            float p = t / EnterDuration;

            float barEase = EaseOutCubic(p);
            topBarRect.anchoredPosition = new Vector2(0, Mathf.Lerp(BarHeight, 0f, barEase));
            bottomBarRect.anchoredPosition = new Vector2(0, Mathf.Lerp(-BarHeight, 0f, barEase));

            float textP = EaseOutBack(Mathf.Clamp01((p - 0.25f) / 0.75f));
            textRect.localScale = Vector3.one * textP;

            float lineP = EaseOutCubic(Mathf.Clamp01((p - 0.55f) / 0.45f));
            lineLeftRect.sizeDelta = new Vector2(lineP * LineMaxHalfWidth, LineThickness);
            lineRightRect.sizeDelta = new Vector2(lineP * LineMaxHalfWidth, LineThickness);
            dateRect.localScale = Vector3.one * lineP;

            if (hasBanner)
            {
                float bannerP = EaseOutCubic(Mathf.Clamp01(p / 0.8f));
                float bannerX = Mathf.Lerp(BannerStartX, BannerRestX, bannerP);
                leftBannerRect.anchoredPosition = new Vector2(-bannerX, 0f);
                rightBannerRect.anchoredPosition = new Vector2(bannerX, 0f);
                Color bannerColor = new(1f, 1f, 1f, bannerP);
                leftBannerImg.color = bannerColor;
                rightBannerImg.color = bannerColor;
            }

            t += Time.deltaTime;
            yield return null;
        }
        topBarRect.anchoredPosition = Vector2.zero;
        bottomBarRect.anchoredPosition = Vector2.zero;
        textRect.localScale = Vector3.one;
        dateRect.localScale = Vector3.one;
        lineLeftRect.sizeDelta = new Vector2(LineMaxHalfWidth, LineThickness);
        lineRightRect.sizeDelta = new Vector2(LineMaxHalfWidth, LineThickness);
        if (hasBanner)
        {
            leftBannerRect.anchoredPosition = new Vector2(-BannerRestX, 0f);
            rightBannerRect.anchoredPosition = new Vector2(BannerRestX, 0f);
            leftBannerImg.color = Color.white;
            rightBannerImg.color = Color.white;
        }

        // Phase 2: gold shimmer flash
        t = 0f;
        while (t < FlashDuration)
        {
            float arc = Mathf.Sin((t / FlashDuration) * Mathf.PI);
            turnText.color = Color.Lerp(GoldColor, Color.white, arc * 0.65f);
            t += Time.deltaTime;
            yield return null;
        }
        turnText.color = GoldColor;

        // Phase 3: hold
        yield return new WaitForSeconds(HoldDuration);

        // Phase 4: fade everything out
        t = 0f;
        while (t < ExitDuration)
        {
            rootGroup.alpha = 1f - EaseInCubic(t / ExitDuration);
            t += Time.deltaTime;
            yield return null;
        }
        rootGroup.alpha = 0f;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
    private static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
