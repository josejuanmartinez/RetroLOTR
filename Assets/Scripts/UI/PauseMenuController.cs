using RetroLOTR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>ESC pause menu. Save/load are intentionally exposed but disabled until persistence exists.</summary>
public sealed class PauseMenuController : MonoBehaviour
{
    private static PauseMenuController instance;
    private GameObject root;
    private CanvasGroup rootGroup;
    private RectTransform config;
    private MenuBackgroundRotator backdrop;

    public static void Toggle()
    {
        if (instance == null)
        {
            Game game = Game.Instance;
            if (game == null) return;
            instance = game.gameObject.GetComponent<PauseMenuController>();
            if (instance == null) instance = game.gameObject.AddComponent<PauseMenuController>();
        }
        instance.SetVisible(instance.root == null || !instance.root.activeSelf);
    }

    private void SetVisible(bool visible)
    {
        if (root == null) Build();
        root.SetActive(visible);
        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.interactable = visible;
        rootGroup.blocksRaycasts = visible;
        if (visible)
        {
            SkinManager skin = FindFirstObjectByType<SkinManager>();
            backdrop.SetSkin(skin != null ? skin.CurrentSkin : Skins.Default);
            Sounds.Instance?.PlayUiClick();
        }
        else
        {
            if (config != null) config.gameObject.SetActive(false);
            Sounds.Instance?.PlayUiExit();
        }
    }

    private void Build()
    {
        root = new GameObject("PauseScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootGroup = root.GetComponent<CanvasGroup>();
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 8000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        backdrop = new GameObject("Video Screenshot Backdrop").AddComponent<MenuBackgroundRotator>(); backdrop.Initialise(root.transform);
        RectTransform veil = StartScreenController.CreatePanel("Pause Veil", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.015f, 0.025f, 0.76f));
        veil.GetComponent<Image>().raycastTarget = true;

        RectTransform bar = StartScreenController.CreatePanel("Pause Menu Bar", root.transform, new Vector2(0.67f, 0.5f), new Vector2(0.94f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.065f, 0.95f));
        bar.sizeDelta = new Vector2(0f, 760f);
        VerticalLayoutGroup layout = bar.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(36, 36, 38, 38); layout.spacing = 13; layout.childAlignment = TextAnchor.UpperCenter; layout.childControlWidth = true; layout.childControlHeight = false;
        StartScreenController.CreateLabel("GAME PAUSED", bar, 35, FontStyles.Bold, 58);
        CreateDisabledButton("Save — coming soon", bar);
        CreateDisabledButton("Load — coming soon", bar);
        StartScreenController.CreateButton("Config", bar, ToggleConfig);
        StartScreenController.CreateButton("Autoplay Turn", bar, AutoplayTurn);
        StartScreenController.CreateButton("Return to Start", bar, ReturnToStart);
        StartScreenController.CreateButton("Quit", bar, Quit);
        StartScreenController.CreateLabel("ESC to resume", bar, 13, FontStyles.Italic, 24);

        config = StartScreenController.CreatePanel("Configuration", root.transform, new Vector2(0.32f, 0.5f), new Vector2(0.62f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.065f, 0.95f));
        config.sizeDelta = new Vector2(0f, 460f);
        VerticalLayoutGroup configLayout = config.gameObject.AddComponent<VerticalLayoutGroup>(); configLayout.padding = new RectOffset(34, 34, 34, 34); configLayout.spacing = 13; configLayout.childAlignment = TextAnchor.UpperCenter; configLayout.childControlWidth = true; configLayout.childControlHeight = false;
        StartScreenController.CreateLabel("CONFIGURATION", config, 28, FontStyles.Bold, 45);
        CreateVolume("Music", config, () => Music.Instance != null ? Music.Instance.musicVolume : 0.5f, v => { if (Music.Instance != null) { Music.Instance.musicVolume = v; if (Music.Instance.musicAudioSource != null) Music.Instance.musicAudioSource.volume = v; } });
        CreateVolume("Sound", config, () => Sounds.Instance?.soundAudioSource != null ? Sounds.Instance.soundAudioSource.volume : 0.8f, v => { if (Sounds.Instance?.soundAudioSource != null) Sounds.Instance.soundAudioSource.volume = v; });
        CreateVolume("Ambience", config, () => Music.Instance != null ? Music.Instance.ambientVolume : 0.4f, v => { if (Music.Instance != null) { Music.Instance.ambientVolume = v; if (Music.Instance.ambientAudioSource != null) Music.Instance.ambientAudioSource.volume = v; } });
        StartScreenController.CreateButton("Change Skin", config, ChangeSkin, 48f);
        config.gameObject.SetActive(false);
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static void CreateDisabledButton(string label, RectTransform parent)
    {
        Button button = StartScreenController.CreateButton(label, parent, () => Sounds.Instance?.PlayUiDenied());
        button.interactable = false;
    }

    private static void CreateVolume(string name, RectTransform parent, System.Func<float> value, UnityEngine.Events.UnityAction<float> set)
    {
        StartScreenController.CreateLabel(name, parent, 16, FontStyles.Bold, 22);
        GameObject go = new(name + " Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement)); go.transform.SetParent(parent, false); go.GetComponent<LayoutElement>().preferredHeight = 26f;
        Slider slider = go.GetComponent<Slider>(); slider.minValue = 0f; slider.maxValue = 1f; slider.value = value(); slider.onValueChanged.AddListener(set);
        Image background = go.AddComponent<Image>(); background.color = new Color(0.14f, 0.15f, 0.20f, 1f);
        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(go.transform, false); fill.GetComponent<Image>().color = new Color(0.78f, 0.55f, 0.19f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>();
    }

    private void ToggleConfig() { config.gameObject.SetActive(!config.gameObject.activeSelf); Sounds.Instance?.PlayUiClick(); }
    private void ChangeSkin()
    {
        SkinManager manager = FindFirstObjectByType<SkinManager>();
        if (manager == null) return;
        manager.ChangeSkin(manager.CurrentSkin == Skins.Default ? Skins.Bakshi : Skins.Default);
        backdrop.SetSkin(manager.CurrentSkin);
        Sounds.Instance?.PlayUiClick();
    }

    private void AutoplayTurn()
    {
        SetVisible(false);
        Game.Instance?.AutoplayOneTurn();
    }

    private static void ReturnToStart()
    {
        GameConfig.ScenarioChosen = false; GameConfig.ScenarioToLoad = null; GameConfig.SkipIntro = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void Quit() { Sounds.Instance?.PlayUiExit(); Application.Quit(); }
}
