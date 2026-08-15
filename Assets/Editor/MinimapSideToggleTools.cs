using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One-off migration: the Grid/Hint/Regions buttons on Layout > Bottom > Minimap > MinimapSide
/// were plain Buttons faking a toggled look (dimmed/brightened label on click). Run "Convert
/// Grid Hint Regions Buttons To Toggles" once to replace them with real UI.Toggle components
/// with a checkbox, wired to the same underlying bool state.
/// </summary>
public static class MinimapSideToggleTools
{
    private const string PrefabPath = "Assets/GameObjects/Layout.prefab";
    private static readonly Color AccentColor = new(0f, 0.6089034f, 0.754717f, 1f); // matches the sidebar icons

    [MenuItem("Tools/RetroLOTR/Minimap/Convert Grid Hint Regions Buttons To Toggles")]
    public static void ConvertMinimapSideButtonsToToggles()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"MinimapSideToggleTools: {PrefabPath} not found.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform gridButton = FindChildByName(root.transform, "GridButton");
            Transform hintButton = FindChildByName(root.transform, "HintButton");
            Transform regionsButton = FindChildByName(root.transform, "RegionsButton");

            if (gridButton == null || hintButton == null || regionsButton == null)
            {
                Debug.LogError("MinimapSideToggleTools: could not find GridButton/HintButton/RegionsButton under MinimapSide.");
                return;
            }

            Sprite backgroundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite checkmarkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

            Toggle gridToggle = ConvertButtonToToggle(gridButton.gameObject, backgroundSprite, checkmarkSprite);
            HexGridToggle gridBridge = gridButton.GetComponent<HexGridToggle>();
            if (gridBridge == null) gridBridge = gridButton.gameObject.AddComponent<HexGridToggle>();
            WirePersistentBoolListener(gridToggle, new UnityAction<bool>(gridBridge.SetHexGridEnabled));

            Toggle regionsToggle = ConvertButtonToToggle(regionsButton.gameObject, backgroundSprite, checkmarkSprite);
            RegionsViewToggle regionsBridge = regionsButton.GetComponent<RegionsViewToggle>();
            if (regionsBridge == null) regionsBridge = regionsButton.gameObject.AddComponent<RegionsViewToggle>();
            WirePersistentBoolListener(regionsToggle, new UnityAction<bool>(regionsBridge.SetRegionsViewEnabled));

            Toggle hintToggle = ConvertButtonToToggle(hintButton.gameObject, backgroundSprite, checkmarkSprite);
            OpportunityHintsToggle hintBridge = hintButton.GetComponent<OpportunityHintsToggle>();
            if (hintBridge == null) hintBridge = hintButton.gameObject.AddComponent<OpportunityHintsToggle>();
            WirePersistentBoolListener(hintToggle, new UnityAction<bool>(hintBridge.SetHintsEnabled));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("MinimapSideToggleTools: converted Grid/Hint/Regions buttons to toggles.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Replaces a Button (with a TMP label as its targetGraphic) with a Toggle driving a small
    // checkbox inserted to the left of that same label.
    private static Toggle ConvertButtonToToggle(GameObject buttonGo, Sprite backgroundSprite, Sprite checkmarkSprite)
    {
        Button button = buttonGo.GetComponent<Button>();
        Graphic label = button != null ? button.targetGraphic : null;
        if (button != null) Object.DestroyImmediate(button, true);

        Toggle toggle = buttonGo.GetComponent<Toggle>();
        if (toggle == null) toggle = buttonGo.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.isOn = false;

        GameObject checkboxGo = new("Checkbox", typeof(RectTransform));
        checkboxGo.transform.SetParent(buttonGo.transform, false);
        checkboxGo.transform.SetAsFirstSibling();
        RectTransform checkboxRt = (RectTransform)checkboxGo.transform;
        checkboxRt.anchorMin = new Vector2(0f, 0.5f);
        checkboxRt.anchorMax = new Vector2(0f, 0.5f);
        checkboxRt.pivot = new Vector2(0f, 0.5f);
        checkboxRt.sizeDelta = new Vector2(18f, 18f);
        checkboxRt.anchoredPosition = new Vector2(2f, 0f);

        Image background = checkboxGo.AddComponent<Image>();
        background.sprite = backgroundSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(1f, 1f, 1f, 0.6f);

        GameObject checkmarkGo = new("Checkmark", typeof(RectTransform));
        checkmarkGo.transform.SetParent(checkboxGo.transform, false);
        RectTransform checkmarkRt = (RectTransform)checkmarkGo.transform;
        checkmarkRt.anchorMin = Vector2.zero;
        checkmarkRt.anchorMax = Vector2.one;
        checkmarkRt.offsetMin = Vector2.zero;
        checkmarkRt.offsetMax = Vector2.zero;

        Image checkmark = checkmarkGo.AddComponent<Image>();
        checkmark.sprite = checkmarkSprite;
        checkmark.color = AccentColor;

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;

        if (label != null)
        {
            RectTransform labelRt = label.rectTransform;
            labelRt.offsetMin = new Vector2(24f, labelRt.offsetMin.y);
        }

        return toggle;
    }

    private static void WirePersistentBoolListener(Toggle toggle, UnityAction<bool> call)
    {
        int index = toggle.onValueChanged.GetPersistentEventCount();
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, call);
        toggle.onValueChanged.SetPersistentListenerState(index, UnityEventCallState.RuntimeOnly);
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindChildByName(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
