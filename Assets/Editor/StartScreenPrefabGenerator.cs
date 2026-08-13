using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartScreenPrefabGenerator
{
    private const string PrefabPath = "Assets/GameObjects/StartScreen.prefab";
    private const string ScenePath = "Assets/Scenes/InGame.unity";
    private const string LogoPath = "Assets/Art/UI/RuneboardLogo.png";

    [InitializeOnLoadMethod]
    private static void GenerateOnceInOpenEditor()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            GenerateIfNeededInActiveScene();
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += GenerateIfNeededInActiveScene;
    }

    private static void GenerateIfNeededInActiveScene()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && FindInScene<StartScreenController>(activeScene) != null) return;
        Generate();
    }

    [MenuItem("Tools/RetroLOTR/Rebuild Start Screen Prefab")]
    public static void Generate()
    {
        ConfigureMenuBackgroundImporters();
        GameObject root = BuildPrefabHierarchy();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveLegacyCampaignSkinControls(scene);
        RemoveOldStartScreen(scene);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "StartScreen";
        instance.SetActive(false);

        Board board = FindInScene<Board>(scene);
        if (board == null) throw new System.InvalidOperationException("StartScreenPrefabGenerator: Board not found in InGame scene.");
        SerializedObject boardObject = new(board);
        boardObject.FindProperty("startScreenController").objectReferenceValue = instance.GetComponent<StartScreenController>();
        boardObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {PrefabPath}, added it to {ScenePath}, and wired Board.startScreenController.");
    }

    private static GameObject BuildPrefabHierarchy()
    {
        GameObject root = new("StartScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StartScreenController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject backdropObject = CreateRect("Video Screenshot Backdrop", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        MenuBackgroundRotator rotator = backdropObject.AddComponent<MenuBackgroundRotator>();
        Image back = CreateImage("Backdrop A", backdropObject.transform, new Color(1f, 1f, 1f, 0.9f), false);
        Image front = CreateImage("Backdrop B", backdropObject.transform, new Color(1f, 1f, 1f, 0f), false);
        rotator.Configure(back, front);
        CreateImage("Dark Veil", root.transform, new Color(0.02f, 0.025f, 0.04f, 0.58f), false);

        GameObject barObject = CreateRect("Menu Bar", root.transform, new Vector2(0.06f, 0.12f), new Vector2(0.32f, 0.88f), Vector2.zero, Vector2.zero);
        Image barImage = barObject.AddComponent<Image>();
        barImage.color = Color.white;
        barImage.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Fog.mat");
        VerticalLayoutGroup layout = barObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 44, 44);
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        RectTransform bar = barObject.GetComponent<RectTransform>();
        CreateLogo(bar);
        CreateLabel("Subtitle", "THE UNTOLD WAR OF THE RING", bar, 16, FontStyles.Italic, 35);
        CreateSpacer(bar, 20);
        Button start = CreateButton("StartButton", "Start", bar, 58);
        CreateLabel("SkinHeading", "SKIN", bar, 14, FontStyles.Bold, 26);
        Button skin = CreateButton("SkinButton", "Skin: Default", bar, 44);
        TextMeshProUGUI skinText = skin.GetComponentInChildren<TextMeshProUGUI>(true);
        CreateSpacer(bar, 16);
        Button quit = CreateButton("QuitButton", "Quit", bar, 58);
        CreateLabel("Footer", "© RetroLOTR", bar, 12, FontStyles.Normal, 24);

        SerializedObject controller = new(root.GetComponent<StartScreenController>());
        controller.FindProperty("root").objectReferenceValue = root;
        controller.FindProperty("startButton").objectReferenceValue = start;
        controller.FindProperty("quitButton").objectReferenceValue = quit;
        controller.FindProperty("skinButton").objectReferenceValue = skin;
        controller.FindProperty("backdrop").objectReferenceValue = rotator;
        controller.FindProperty("skinValue").objectReferenceValue = skinText;
        controller.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<RectTransform>().localScale = Vector3.one;
        return root;
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

    private static void CreateLogo(RectTransform parent)
    {
        GameObject go = new("Title", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 190f;
        Image image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        image.preserveAspect = true;
        image.raycastTarget = false;
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

    private static void CreateSpacer(RectTransform parent, float height)
    {
        GameObject spacer = new("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void ConfigureMenuBackgroundImporters()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/MenuBackgrounds" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }

    private static void RemoveLegacyCampaignSkinControls(Scene scene)
    {
        RetroLOTR.Scenarios.CampaignSelectionManager selector = FindInScene<RetroLOTR.Scenarios.CampaignSelectionManager>(scene);
        if (selector == null) return;
        foreach (string name in new[] { "Skin", "Dropdown", "SkinDropdown" })
        {
            Transform child = selector.transform.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void RemoveOldStartScreen(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "StartScreen" && root.GetComponent<StartScreenController>() != null)
                Object.DestroyImmediate(root);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
