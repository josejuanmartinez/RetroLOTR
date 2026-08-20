using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PauseMenuPrefabGenerator
{
    private const string PrefabPath = "Assets/GameObjects/PauseMenu.prefab";
    private const string ScenePath = "Assets/Scenes/InGame.unity";

    [InitializeOnLoadMethod]
    private static void GenerateOnceInOpenEditor()
    {
        EditorApplication.delayCall += GenerateIfNeededInActiveScene;
    }

    private static void GenerateIfNeededInActiveScene()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && FindInScene<PauseMenuController>(scene) != null) return;
        Generate();
    }

    // No longer exposed as a menu item — it self-heals via GenerateOnceInOpenEditor above, and a
    // deliberate rebuild is a one-off script run, not a permanent menu entry.
    public static void Generate()
    {
        GameObject source = BuildPrefabHierarchy();
        PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        Object.DestroyImmediate(source);

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RemoveOldPauseMenus(scene);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "PauseMenu";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {PrefabPath} and added it to {ScenePath}.");
    }

    private static GameObject BuildPrefabHierarchy()
    {
        GameObject prefabRoot = new("PauseMenu", typeof(PauseMenuController));
        GameObject screen = new("PauseScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        screen.transform.SetParent(prefabRoot.transform, false);
        RectTransform screenRect = screen.GetComponent<RectTransform>();
        screenRect.anchorMin = Vector2.zero;
        screenRect.anchorMax = Vector2.one;
        screenRect.offsetMin = Vector2.zero;
        screenRect.offsetMax = Vector2.zero;
        Canvas canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8000;
        CanvasScaler scaler = screen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject backdropObject = CreateRect("Video Screenshot Backdrop", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        MenuBackgroundRotator backdrop = backdropObject.AddComponent<MenuBackgroundRotator>();
        Image back = CreateImage("Backdrop A", backdropObject.transform, Color.white, false);
        Image front = CreateImage("Backdrop B", backdropObject.transform, new Color(1f, 1f, 1f, 0f), false);
        backdrop.Configure(back, front);
        CreateImage("Pause Veil", screen.transform, new Color(0.01f, 0.015f, 0.025f, 0.76f), true);

        RectTransform bar = CreatePanel("Pause Menu Bar", screen.transform, new Vector2(0.67f, 0.5f), new Vector2(0.94f, 0.5f), new Color(0.035f, 0.04f, 0.065f, 0.95f));
        bar.sizeDelta = new Vector2(0f, 760f);
        VerticalLayoutGroup layout = bar.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 38, 38);
        layout.spacing = 13;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        CreateLabel("Title", "GAME PAUSED", bar, 35, FontStyles.Bold, 58);
        Button save = CreateButton("SaveButton", "Save — coming soon", bar, 58);
        Button load = CreateButton("LoadButton", "Load — coming soon", bar, 58);
        Button configButton = CreateButton("ConfigButton", "Config", bar, 58);
        Button autoplay = CreateButton("AutoplayButton", "Autoplay Turn", bar, 58);
        Button returnToStart = CreateButton("ReturnToStartButton", "Return to Start", bar, 58);
        Button quit = CreateButton("QuitButton", "Quit", bar, 58);
        CreateLabel("Footer", "ESC to resume", bar, 13, FontStyles.Italic, 24);

        RectTransform config = CreatePanel("Configuration", screen.transform, new Vector2(0.32f, 0.5f), new Vector2(0.62f, 0.5f), new Color(0.035f, 0.04f, 0.065f, 0.95f));
        config.sizeDelta = new Vector2(0f, 460f);
        VerticalLayoutGroup configLayout = config.gameObject.AddComponent<VerticalLayoutGroup>();
        configLayout.padding = new RectOffset(34, 34, 34, 34);
        configLayout.spacing = 13;
        configLayout.childAlignment = TextAnchor.UpperCenter;
        configLayout.childControlWidth = true;
        configLayout.childControlHeight = false;
        CreateLabel("Title", "CONFIGURATION", config, 28, FontStyles.Bold, 45);
        Slider music = CreateVolume("Music", config);
        Slider sound = CreateVolume("Sound", config);
        Slider ambience = CreateVolume("Ambience", config);
        Button changeSkin = CreateButton("ChangeSkinButton", "Change Skin", config, 48);
        config.gameObject.SetActive(false);

        SerializedObject controller = new(prefabRoot.GetComponent<PauseMenuController>());
        SetReference(controller, "root", screen);
        SetReference(controller, "rootGroup", screen.GetComponent<CanvasGroup>());
        SetReference(controller, "config", config);
        SetReference(controller, "backdrop", backdrop);
        SetReference(controller, "saveButton", save);
        SetReference(controller, "loadButton", load);
        SetReference(controller, "configButton", configButton);
        SetReference(controller, "autoplayButton", autoplay);
        SetReference(controller, "returnToStartButton", returnToStart);
        SetReference(controller, "quitButton", quit);
        SetReference(controller, "changeSkinButton", changeSkin);
        SetReference(controller, "musicSlider", music);
        SetReference(controller, "soundSlider", sound);
        SetReference(controller, "ambienceSlider", ambience);
        controller.ApplyModifiedPropertiesWithoutUndo();
        screen.SetActive(false);
        return prefabRoot;
    }

    private static void SetReference(SerializedObject owner, string property, Object value)
    {
        owner.FindProperty(property).objectReferenceValue = value;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, string text, RectTransform parent, float size, FontStyles style, float height)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.88f, 0.67f);
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(string name, string text, RectTransform parent, float height)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        go.GetComponent<Image>().color = new Color(0.20f, 0.16f, 0.10f, 0.94f);
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.4f);
        colors.pressedColor = new Color(0.62f, 0.44f, 0.18f);
        button.colors = colors;
        CreateLabel("Label", text, go.GetComponent<RectTransform>(), 22, FontStyles.Bold, height);
        return button;
    }

    private static Slider CreateVolume(string name, RectTransform parent)
    {
        CreateLabel(name + "Label", name, parent, 16, FontStyles.Bold, 22);
        GameObject go = new(name + "Slider", typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 26f;
        go.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.20f, 1f);
        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        GameObject fill = CreateRect("Fill", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill.AddComponent<Image>().color = new Color(0.78f, 0.55f, 0.19f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        return slider;
    }

    private static void RemoveOldPauseMenus(Scene scene)
    {
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            if (sceneRoot.name == "PauseMenu" || sceneRoot.GetComponent<PauseMenuController>() != null)
                Object.DestroyImmediate(sceneRoot);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            T found = sceneRoot.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
