using System.Collections.Generic;
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
    private const string CardPrefabPath = "Assets/GameObjects/Reusable/Card.prefab";
    private const string PrefabPath = "Assets/GameObjects/StartupLoadingScreen.prefab";
    private const string ScenePath = "Assets/Scenes/InGame.unity";
    private const int CardSlotCount = 3;

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

    // No longer exposed as a menu item — it self-heals via GenerateOnce above, and a deliberate
    // rebuild is a one-off script run, not a permanent menu entry.
    public static void Generate()
    {
        // Destroying the previous StartupLoadingScreen instance below while it (or its
        // components) is selected in the Inspector makes the Inspector redraw against
        // half-destroyed objects, throwing MissingReferenceException. Clear the selection
        // for the duration to avoid it.
        Object previousSelection = Selection.activeObject;
        Selection.activeObject = null;
        try
        {
            GenerateInternal();
        }
        finally
        {
            // previousSelection may itself have been the object destroyed inside GenerateInternal —
            // Unity's overloaded null-check catches that case, so only restore if it's still alive.
            if (previousSelection != null) Selection.activeObject = previousSelection;
        }
    }

    private static void GenerateInternal()
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

        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab == null)
            throw new System.InvalidOperationException($"{CardPrefabPath} was not found.");

        GameObject cardsRow = new("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Canvas));
        cardsRow.layer = 5;
        cardsRow.transform.SetParent(root.transform, false);
        RectTransform cardsRowRect = cardsRow.GetComponent<RectTransform>();
        cardsRowRect.localScale = Vector3.one;
        cardsRowRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRowRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRowRect.anchoredPosition = new Vector2(0f, 260f);
        cardsRowRect.sizeDelta = new Vector2(1200f, 400f);
        HorizontalLayoutGroup cardsLayout = cardsRow.GetComponent<HorizontalLayoutGroup>();
        cardsLayout.childAlignment = TextAnchor.MiddleCenter;
        cardsLayout.childControlWidth = false;
        cardsLayout.childControlHeight = false;
        cardsLayout.childScaleWidth = false;
        cardsLayout.childScaleHeight = false;
        Canvas cardsRowCanvas = cardsRow.GetComponent<Canvas>();
        cardsRowCanvas.overrideSorting = true;
        cardsRowCanvas.sortingOrder = short.MaxValue;

        List<CardDataProvider> cardProviders = new();
        for (int i = 0; i < CardSlotCount; i++)
        {
            GameObject cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, cardsRow.transform);
            cardInstance.name = $"Card {i + 1}";
            CardDataProvider provider = cardInstance.AddComponent<CardDataProvider>();
            provider.cardName = "Gandalf";
            provider.startAsToken = false;
            provider.suppressHoverEffects = true;
            provider.showRequirementWarnings = false;
            provider.showCloseIcon = false;
            cardInstance.AddComponent<CardShineEffect>();
            cardProviders.Add(provider);
        }

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

        TextMeshProUGUI templateLabel = status != null ? status : title;
        GameObject pressAnyKey = templateLabel != null
            ? Object.Instantiate(templateLabel.gameObject, root.transform, false)
            : new GameObject("PressAnyKey", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        pressAnyKey.name = "PressAnyKey";
        pressAnyKey.transform.SetParent(root.transform, false);
        RectTransform pressAnyKeyRect = pressAnyKey.GetComponent<RectTransform>();
        pressAnyKeyRect.localScale = Vector3.one;
        pressAnyKeyRect.anchorMin = new Vector2(1f, 0f);
        pressAnyKeyRect.anchorMax = new Vector2(1f, 0f);
        pressAnyKeyRect.pivot = new Vector2(1f, 0f);
        pressAnyKeyRect.anchoredPosition = new Vector2(-34.21338f, 0f);
        pressAnyKeyRect.sizeDelta = new Vector2(928.5166f, 50f);
        TextMeshProUGUI pressAnyKeyLabel = pressAnyKey.GetComponent<TextMeshProUGUI>();
        pressAnyKeyLabel.text = "Click any key to continue";
        pressAnyKeyLabel.alignment = TextAlignmentOptions.BottomRight;
        pressAnyKeyLabel.raycastTarget = false;
        pressAnyKey.SetActive(false); // StartupLoadingScreen reveals this once assets are ready

        SerializedObject controller = new(root.GetComponent<StartupLoadingScreen>());
        controller.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        controller.FindProperty("progressBar").objectReferenceValue = slider;
        controller.FindProperty("statusText").objectReferenceValue = status != null ? status : title;
        SerializedProperty cardProvidersProp = controller.FindProperty("cardProviders");
        cardProvidersProp.ClearArray();
        for (int i = 0; i < cardProviders.Count; i++)
        {
            cardProvidersProp.InsertArrayElementAtIndex(i);
            cardProvidersProp.GetArrayElementAtIndex(i).objectReferenceValue = cardProviders[i];
        }
        controller.FindProperty("continuePromptText").objectReferenceValue = pressAnyKeyLabel;
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
