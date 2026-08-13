using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartupLoadingScreenPrefabGenerator
{
    private const string SourcePrefabPath = "Assets/GameObjects/LeaderSelection.prefab";
    private const string PrefabPath = "Assets/GameObjects/StartupLoadingScreen.prefab";
    private const string ScenePath = "Assets/Scenes/InGame.unity";

    [InitializeOnLoadMethod]
    private static void GenerateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
                && FindInScene<StartupLoadingScreen>(scene) != null) return;
            Generate();
        };
    }

    [MenuItem("Tools/RetroLOTR/Rebuild Startup Loading Screen Prefab")]
    public static void Generate()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        Slider sourceSlider = source != null ? source.GetComponentInChildren<Slider>(true) : null;
        if (sourceSlider == null)
            throw new System.InvalidOperationException("LeaderSelection Progress slider was not found.");

        GameObject root = new("StartupLoadingScreen", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(StartupLoadingScreen));
        root.layer = 5;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject veil = new("Loading Veil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        veil.layer = 5;
        veil.transform.SetParent(root.transform, false);
        RectTransform veilRect = veil.GetComponent<RectTransform>();
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
        veil.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.035f, 0.96f);

        GameObject widget = Object.Instantiate(sourceSlider.transform.parent.gameObject, root.transform, false);
        widget.name = "Progress Widget";
        widget.SetActive(true);
        Canvas nestedCanvas = widget.GetComponent<Canvas>();
        if (nestedCanvas != null) Object.DestroyImmediate(nestedCanvas);
        RectTransform widgetRect = widget.GetComponent<RectTransform>();
        widgetRect.localScale = Vector3.one;
        widgetRect.anchorMin = new Vector2(0.5f, 0.5f);
        widgetRect.anchorMax = new Vector2(0.5f, 0.5f);
        widgetRect.anchoredPosition = Vector2.zero;
        widgetRect.sizeDelta = new Vector2(1100f, 140f);

        Slider slider = widget.GetComponentInChildren<Slider>(true);
        slider.interactable = false;
        slider.value = 0f;
        TextMeshProUGUI[] labels = widget.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI title = labels.FirstOrDefault(label => label.gameObject.name == "Title");
        TextMeshProUGUI status = labels.FirstOrDefault(label => label.gameObject.name == "Text");
        if (title != null) title.text = "> Loading Runeboard <";
        if (status != null) status.text = "> Preparing the Runeboard <";
        foreach (TextMeshProUGUI label in labels) label.raycastTarget = false;

        SerializedObject controller = new(root.GetComponent<StartupLoadingScreen>());
        controller.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        controller.FindProperty("progressBar").objectReferenceValue = slider;
        controller.FindProperty("statusText").objectReferenceValue = status != null ? status : title;
        controller.ApplyModifiedPropertiesWithoutUndo();
        root.GetComponent<RectTransform>().localScale = Vector3.one;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StartupLoadingScreen existing = FindInScene<StartupLoadingScreen>(scene);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "StartupLoadingScreen";
        instance.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {PrefabPath} from the existing LeaderSelection Progress widget and added it to {ScenePath}.");
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
