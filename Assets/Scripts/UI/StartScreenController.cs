using RetroLOTR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First screen on a fresh scene. Campaign selection is shown only after Start.</summary>
public sealed class StartScreenController : MonoBehaviour
{
    [Header("Authored Start Screen")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button skinButton;
    [SerializeField] private MenuBackgroundRotator backdrop;
    [SerializeField] private TextMeshProUGUI skinValue;

    private CampaignSelectionManager campaignSelection;
    private bool wired;

    public void Show(CampaignSelectionManager selector)
    {
        campaignSelection = selector;
        campaignSelection.gameObject.SetActive(false);
        if (root == null) Build();
        WireAuthoredControls();
        root.SetActive(true);
        ApplySkin(FindFirstObjectByType<SkinManager>()?.CurrentSkin ?? Skins.Default);
    }

    private void Build()
    {
        root = new GameObject("StartScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        backdrop = new GameObject("Video Screenshot Backdrop").AddComponent<MenuBackgroundRotator>();
        backdrop.Initialise(root.transform);
        CreateImage("Dark Veil", root.transform, new Color(0.02f, 0.025f, 0.04f, 0.58f));

        RectTransform bar = CreatePanel("Menu Bar", root.transform, new Vector2(0.06f, 0.5f), new Vector2(0.32f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0.035f, 0.04f, 0.065f, 0.9f));
        VerticalLayoutGroup layout = bar.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 44, 44);
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateLabel("RETROLOTR", bar, 48, FontStyles.Bold, 72);
        CreateLabel("THE UNTOLD WAR OF THE RING", bar, 16, FontStyles.Italic, 35);
        CreateSpacer(bar, 20);
        CreateButton("Start", bar, StartCampaign);
        CreateLabel("SKIN", bar, 14, FontStyles.Bold, 26);
        skinValue = CreateButton("", bar, ToggleSkin, 44).GetComponentInChildren<TextMeshProUGUI>();
        CreateSpacer(bar, 16);
        CreateButton("Quit", bar, Quit);
        CreateLabel("© RetroLOTR", bar, 12, FontStyles.Normal, 24);
        WireAuthoredControls();
    }

    private void WireAuthoredControls()
    {
        if (wired) return;
        if (root == null) root = gameObject;
        if (startButton != null) startButton.onClick.AddListener(StartCampaign);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (skinButton != null) skinButton.onClick.AddListener(ToggleSkin);
        wired = true;
    }

    private void StartCampaign()
    {
        Sounds.Instance?.PlayUiClick();
        root.SetActive(false);
        campaignSelection.gameObject.SetActive(true);
    }

    private void ToggleSkin()
    {
        SkinManager manager = FindFirstObjectByType<SkinManager>();
        ApplySkin(manager != null && manager.CurrentSkin == Skins.Default ? Skins.Bakshi : Skins.Default);
        Sounds.Instance?.PlayUiClick();
    }

    private void ApplySkin(Skins skin)
    {
        FindFirstObjectByType<SkinManager>()?.ChangeSkin(skin);
        backdrop?.SetSkin(skin);
        if (skinValue != null) skinValue.text = $"Skin: {skin}";
    }

    private static void Quit()
    {
        Sounds.Instance?.PlayUiExit();
        Application.Quit();
    }

    internal static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    internal static void CreateImage(string name, Transform parent, Color color)
    {
        CreatePanel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color).GetComponent<Image>().raycastTarget = false;
    }

    internal static Button CreateButton(string text, Transform parent, UnityEngine.Events.UnityAction action, float height = 58f)
    {
        GameObject go = new(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.20f, 0.16f, 0.10f, 0.94f);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, 0.82f, 0.4f); colors.pressedColor = new Color(0.62f, 0.44f, 0.18f); button.colors = colors;
        button.onClick.AddListener(action);
        CreateLabel(text, go.transform as RectTransform, 22, FontStyles.Bold, 0);
        return button;
    }

    internal static TextMeshProUGUI CreateLabel(string text, RectTransform parent, float size, FontStyles style, float preferredHeight)
    {
        GameObject go = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = size; label.fontStyle = style; label.alignment = TextAlignmentOptions.Center; label.color = new Color(0.96f, 0.88f, 0.67f);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (preferredHeight > 0f) go.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
        return label;
    }

    private static void CreateSpacer(RectTransform parent, float height)
    {
        GameObject spacer = new("Spacer", typeof(RectTransform), typeof(LayoutElement)); spacer.transform.SetParent(parent, false); spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }
}
