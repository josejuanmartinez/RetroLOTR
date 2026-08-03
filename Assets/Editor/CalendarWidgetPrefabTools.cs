using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds CalendarWidgetPanel.prefab (the calendar overlay, previously constructed in code
/// by CalendarWidget at runtime) and wires a disabled instance of it into the current scene's
/// DateManager. Run "Build Calendar Widget Prefab" once to create the asset, tweak colors/
/// fonts/sizes on it in the Inspector, then run "Wire Calendar Widget Into Scene" to drop a
/// disabled instance next to DateManager's canvas and assign the reference.
/// </summary>
public static class CalendarWidgetPrefabTools
{
    private const string PrefabPath = "Assets/GameObjects/CalendarWidgetPanel.prefab";
    private const string EventSpriteSheetGuid = "b23024b6cbc6da7478c414464b799f54"; // sprite sheet used previously in Layout.prefab

    private const int Columns = 6;
    private const int Rows = 5; // 6 * 5 = 30 days per month

    private static readonly Color PanelColor = new(0.10f, 0.09f, 0.07f, 0.96f);
    private static readonly Color CellColor = new(0.18f, 0.16f, 0.12f, 1f);
    private static readonly Color TextColor = new(0.92f, 0.87f, 0.72f, 1f);

    [MenuItem("Tools/RetroLOTR/Calendar/Build Calendar Widget Prefab")]
    public static void BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning($"CalendarWidgetPrefabTools: {PrefabPath} already exists. Delete it first if you want to rebuild it from scratch (your style edits will be lost).");
            return;
        }

        GameObject root = BuildHierarchy();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"CalendarWidgetPrefabTools: built {PrefabPath}. Select it and tweak colors/fonts/layout, then run 'Wire Calendar Widget Into Scene'.");
    }

    [MenuItem("Tools/RetroLOTR/Calendar/Wire Calendar Widget Into Scene")]
    public static void WireIntoScene()
    {
        DateManager dateManager = Object.FindFirstObjectByType<DateManager>(FindObjectsInactive.Include);
        if (dateManager == null)
        {
            Debug.LogError("CalendarWidgetPrefabTools: no DateManager found in the open scene.");
            return;
        }

        SerializedObject dmSo = new(dateManager);
        SerializedProperty calendarWidgetProp = dmSo.FindProperty("calendarWidget");
        CalendarWidget existing = calendarWidgetProp.objectReferenceValue as CalendarWidget;
        if (existing != null && existing.gameObject != dateManager.gameObject)
        {
            Debug.Log("CalendarWidgetPrefabTools: DateManager already has a calendarWidget assigned to a separate object; leaving it as-is.");
            return;
        }

        if (existing != null && existing.gameObject == dateManager.gameObject)
        {
            // Legacy setup: CalendarWidget used to be built at runtime and lived directly on the
            // DateManager GameObject. That copy has no wired structure under the new prefab-driven
            // script, so it no longer functions - replace it with a proper prefab instance.
            Debug.Log("CalendarWidgetPrefabTools: removing legacy in-place CalendarWidget component from DateManager.");
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"CalendarWidgetPrefabTools: {PrefabPath} not found. Run 'Build Calendar Widget Prefab' first.");
            return;
        }

        Canvas canvas = dateManager.GetComponentInParent<Canvas>(true);
        if (canvas != null) canvas = canvas.rootCanvas;
        Transform parent = canvas != null ? canvas.transform : dateManager.transform.parent;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Add Calendar Widget");
        instance.transform.SetParent(parent, false);
        instance.SetActive(false);

        CalendarWidget widget = instance.GetComponent<CalendarWidget>();

        Undo.RecordObject(dateManager, "Wire Calendar Widget Into Scene");
        calendarWidgetProp.objectReferenceValue = widget;
        dmSo.ApplyModifiedProperties();

        Scene scene = dateManager.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"CalendarWidgetPrefabTools: added disabled '{instance.name}' under '{parent?.name}' and wired it to DateManager. Save the scene ({scene.path}) to keep it.");
    }

    [MenuItem("Tools/RetroLOTR/Calendar/Setup Calendar Widget (Build + Wire)")]
    public static void Setup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();
        WireIntoScene();
    }

    private static GameObject BuildHierarchy()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        TMP_SpriteAsset eventSpriteSheet = null;
        string spriteSheetPath = AssetDatabase.GUIDToAssetPath(EventSpriteSheetGuid);
        if (!string.IsNullOrEmpty(spriteSheetPath))
            eventSpriteSheet = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(spriteSheetPath);
        if (eventSpriteSheet == null)
            Debug.LogWarning("CalendarWidgetPrefabTools: could not find the calendar event sprite sheet; assign 'Event Sprite Sheet' on the prefab manually.");

        RectTransform panel = CreateRect("CalendarWidgetPanel", null);
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-20f, -90f);
        panel.sizeDelta = new Vector2(420f, 388f);
        AddImage(panel.gameObject, PanelColor);

        // Own sorting canvas so the calendar draws on top of every other UI layer,
        // with its own raycaster so its cells still receive pointer events.
        Canvas overlay = panel.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = short.MaxValue; // 32767 - above all normal UI
        panel.gameObject.AddComponent<GraphicRaycaster>();

        VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        TextMeshProUGUI headerText = CreateText("Header", panel, font, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        SetPreferredHeight(headerText.gameObject, 34f);

        RectTransform grid = CreateRect("Grid", panel);
        GridLayoutGroup glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Columns;
        glg.spacing = new Vector2(4f, 4f);
        glg.cellSize = new Vector2(60f, 50f);
        LayoutElement gridLe = grid.gameObject.AddComponent<LayoutElement>();
        gridLe.preferredHeight = Rows * 50f + (Rows - 1) * 4f;

        List<(GameObject cellObject, Image background, TextMeshProUGUI dayLabel, TextMeshProUGUI iconLabel)> cells = new();
        for (int i = 0; i < Columns * Rows; i++)
        {
            cells.Add(BuildDayCell(grid, font, i + 1));
        }

        TextMeshProUGUI footerText = CreateText("Footer", panel, font, 15f, FontStyles.Italic, TextAlignmentOptions.Center);
        footerText.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f);
        footerText.textWrappingMode = TextWrappingModes.Normal;
        SetPreferredHeight(footerText.gameObject, 64f);

        CalendarWidget widget = panel.gameObject.AddComponent<CalendarWidget>();
        WireSerializedFields(widget, headerText, footerText, cells, eventSpriteSheet);

        return panel.gameObject;
    }

    private static (GameObject, Image, TextMeshProUGUI, TextMeshProUGUI) BuildDayCell(RectTransform parent, TMP_FontAsset font, int day)
    {
        RectTransform cellRt = CreateRect($"Day{day}", parent);
        Image bg = AddImage(cellRt.gameObject, CellColor);
        cellRt.gameObject.AddComponent<RectMask2D>(); // hard-clip cell contents (icons can't spill out)

        TextMeshProUGUI label = CreateText("Num", cellRt, font, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4f, 2f);
        labelRt.offsetMax = new Vector2(-4f, -2f);

        // Event icon(s) live in their own label that fills the cell, centered, and clips so a
        // scaled-up <sprite> can never spill into neighbouring cells or the footer.
        TextMeshProUGUI icon = CreateText("Icon", cellRt, font, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
        icon.textWrappingMode = TextWrappingModes.NoWrap;
        icon.overflowMode = TextOverflowModes.Truncate;
        RectTransform iconRt = icon.rectTransform;
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        // Shift the (vertically centered) icon ~10px lower by lowering both top and bottom edges.
        iconRt.offsetMin = new Vector2(2f, -8f);
        iconRt.offsetMax = new Vector2(-2f, -12f);

        return (cellRt.gameObject, bg, label, icon);
    }

    private static void WireSerializedFields(
        CalendarWidget widget,
        TextMeshProUGUI headerText,
        TextMeshProUGUI footerText,
        List<(GameObject cellObject, Image background, TextMeshProUGUI dayLabel, TextMeshProUGUI iconLabel)> cells,
        TMP_SpriteAsset eventSpriteSheet)
    {
        SerializedObject so = new(widget);
        so.FindProperty("headerText").objectReferenceValue = headerText;
        so.FindProperty("footerText").objectReferenceValue = footerText;
        so.FindProperty("eventSpriteSheet").objectReferenceValue = eventSpriteSheet;
        so.FindProperty("eventSpriteScalePercent").intValue = 200;

        SerializedProperty dayCellsProp = so.FindProperty("dayCells");
        dayCellsProp.arraySize = cells.Count;
        for (int i = 0; i < cells.Count; i++)
        {
            SerializedProperty elem = dayCellsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("cellObject").objectReferenceValue = cells[i].cellObject;
            elem.FindPropertyRelative("background").objectReferenceValue = cells[i].background;
            elem.FindPropertyRelative("dayLabel").objectReferenceValue = cells[i].dayLabel;
            elem.FindPropertyRelative("iconLabel").objectReferenceValue = cells[i].iconLabel;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = TextColor;
        text.raycastTarget = false;
        return text;
    }

    private static void SetPreferredHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
