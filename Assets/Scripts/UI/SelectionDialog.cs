using System.Collections.Generic;
using System.Collections;
using TMPro;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SelectionDialog : MonoBehaviour
{
    public static SelectionDialog Instance { get; private set; }
    public static bool IsShowing { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Button noButton;
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private Image portraitImage;
    [SerializeField] private CanvasGroup portraitCanvasGroup;
    [SerializeField] private Illustrations illustrations;
    [SerializeField] private TextMeshProUGUI title;

    [Header("Option Buttons — replaces dropdown when assigned")]
    [SerializeField] private Transform optionButtonsContainer;
    [SerializeField] private GameObject optionButtonPrefab;

    [Header("Typewriter")]
    [SerializeField] private TypewriterEffect messageTypewriter;

    private readonly List<DialogRequest> queuedRequests = new();
    private readonly List<TMP_Dropdown.OptionData> dropdownOptions = new();
    private DialogRequest activeRequest;
    private readonly List<Button> optionButtons = new();
    private int selectedButtonIndex = -1;
    private Coroutine buttonAnimCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        BindUiReferences();
        WireUiListeners();

        DontDestroyOnLoad(gameObject);
        HideInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Opens a confirmation dialog with a custom message and button labels.
    /// </summary>
    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, bool isAI, Sprite portrait = null, string dialogTitle = null)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, null, isAI, portrait, EventIconType.MultiChoice, true, dialogTitle);
    }

    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, bool isAI, Sprite portrait, EventIconType iconType, string dialogTitle = null)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, null, isAI, portrait, iconType, true, dialogTitle);
    }

    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, List<string> optionDescriptions, bool isAI, Sprite portrait, EventIconType iconType, string dialogTitle = null)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, optionDescriptions, isAI, portrait, iconType, true, dialogTitle);
    }

    public static Task<string> AskImmediate(string message, string yesString, string noString, List<string> options, List<string> optionDescriptions, bool isAI, Sprite portrait, EventIconType iconType, string dialogTitle = null)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, optionDescriptions, isAI, portrait, iconType, false, dialogTitle);
    }

    private Task<string> Show(string message, string yesString, string noString, List<string> options, List<string> optionDescriptions, bool isAI, Sprite portrait, EventIconType iconType, bool useEventIcon, string dialogTitle = null)
    {
        BindUiReferences();
        WireUiListeners();
        if (options.Count < 1)
        {
            Debug.LogWarning("Unable to show Selection Dialog: options < 1");
            return Task.FromResult(string.Empty);
        }

        var request = new DialogRequest
        {
            title = dialogTitle,
            message = message,
            yesString = yesString,
            noString = noString,
            options = options,
            optionDescriptions = optionDescriptions,
            portrait = portrait,
            tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        if (isAI)
        {
            int index = Random.Range(0, options.Count);
            request.tcs.TrySetResult(options[index]);
        }
        else if (!useEventIcon)
        {
            OpenRequest(request);
        }
        else
        {
            EventIconsManager iconsManager = EventIconsManager.FindManager();
            if (iconsManager == null)
            {
                OpenRequest(request);
            }
            else
            {
                EventIcon icon = null;
                icon = iconsManager.AddEventIcon(
                    iconType,
                    false,
                    () =>
                    {
                        OpenRequest(request);
                        Instance?.StartCoroutine(ConsumeIconNextFrame(icon));
                    });
            }
        }
        return request.tcs.Task;
    }

    private static IEnumerator ConsumeIconNextFrame(EventIcon icon)
    {
        yield return null;
        icon?.ConsumeAndDestroy();
        Canvas.ForceUpdateCanvases();
        EventSystem.current?.UpdateModules();
    }

    private void OpenRequest(DialogRequest request)
    {
        if (request == null) return;

        if (activeRequest != null && activeRequest != request)
        {
            queuedRequests.Add(request);
            return;
        }

        activeRequest = request;
        Debug.Log($"[SelectionDialog] OpenRequest -> '{request.message}'");
        ShowInternal(request);
    }

    public void CloseCurrentSelection()
    {
        Resolve(GetSelectedOptionText());
    }

    private string GetSelectedOptionText()
    {
        if (activeRequest == null || !HasValidSelection()) return string.Empty;

        if (UseButtonList)
        {
            return selectedButtonIndex >= 0 && selectedButtonIndex < activeRequest.options.Count
                ? activeRequest.options[selectedButtonIndex]
                : string.Empty;
        }

        if (dropdown == null) return string.Empty;
        int optionIndex = dropdown.value - 1;
        return optionIndex >= 0 && optionIndex < activeRequest.options.Count
            ? activeRequest.options[optionIndex]
            : string.Empty;
    }

    private void Resolve(string answer)
    {
        Debug.Log($"[SelectionDialog] Resolve -> '{answer}'");
        DialogRequest requestToResolve = activeRequest;
        HideInstant();
        requestToResolve?.tcs?.TrySetResult(answer);
        activeRequest = null;

        if (queuedRequests.Count > 0)
        {
            DialogRequest nextRequest = queuedRequests[0];
            queuedRequests.RemoveAt(0);
            OpenRequest(nextRequest);
        }
    }

    private void HideInstant()
    {
        content.SetActive(false);
        IsShowing = false;
        activeRequest = null;
        ClearOptionButtons();
        UpdatePortrait(null);
    }

    private void ShowActive()
    {
        if (queuedRequests.Count == 0)
        {
            HideInstant();
            return;
        }

        OpenRequest(queuedRequests[0]);
    }

    private bool UseButtonList => optionButtonsContainer != null;

    private void ShowInternal(DialogRequest request)
    {
        if (request == null) return;
        BindUiReferences();
        WireUiListeners();
        content.SetActive(true);
        EnsureDialogHierarchyActive();
        IsShowing = true;
        activeRequest = request;

        bool hasCustomTitle = !string.IsNullOrWhiteSpace(request.title);
        if (messageLabel != null)
        {
            if (messageTypewriter != null) messageTypewriter.Clear();
            messageLabel.text = request.message;
        }
        if (title != null)
        {
            title.text = hasCustomTitle ? FormatTitle(request.title) : string.Empty;
            title.gameObject.SetActive(!string.IsNullOrWhiteSpace(title.text));
        }
        UpdatePortrait(request.portrait);

        if (UseButtonList)
        {
            if (dropdown != null) dropdown.gameObject.SetActive(false);
            selectedButtonIndex = -1;
            BuildOptionButtons(request.options, request.optionDescriptions);
        }
        else
        {
            if (dropdown != null) dropdown.gameObject.SetActive(true);
            dropdownOptions.Clear();
            string placeholderText = string.IsNullOrWhiteSpace(request.yesString) ? "Select an option" : request.yesString;
            dropdownOptions.Add(new TMP_Dropdown.OptionData(placeholderText));
            request.options.ForEach(x =>
            {
                Color optionColor = GetReadableRandomColor();
                dropdownOptions.Add(new TMP_Dropdown.OptionData(FormatOptionLabel(x, optionColor)) { color = optionColor });
            });
            dropdown.ClearOptions();
            dropdown.AddOptions(dropdownOptions);
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            UpdateCaptionColor();
        }

        UpdateCloseButtonState();
    }

    private void EnsureDialogHierarchyActive()
    {
        SetUiObjectActive(content, true);
        SetRectScale(content, Vector3.one);
        SetUiObjectActive(messageLabel != null ? messageLabel.gameObject : null, true);
        SetUiObjectActive(noButton != null ? noButton.gameObject : null, true);
        SetUiObjectActive(dropdown != null ? dropdown.gameObject : null, true);
        GameObject imageRoot = FindDialogChild("Image");
        SetUiObjectActive(imageRoot, true);
        SetRectScale(imageRoot, Vector3.one);

        if (portraitCanvasGroup != null)
        {
            SetUiObjectActive(portraitCanvasGroup.gameObject, true);
            portraitCanvasGroup.alpha = 1f;
            portraitCanvasGroup.interactable = true;
            portraitCanvasGroup.blocksRaycasts = true;
            SetRectScale(portraitCanvasGroup.gameObject, Vector3.one);
        }
        else
        {
            GameObject portraitRoot = FindDialogChild("CharacterImageBg");
            SetUiObjectActive(portraitRoot, true);
            SetRectScale(portraitRoot, Vector3.one);
        }

        if (portraitImage != null)
        {
            SetUiObjectActive(portraitImage.gameObject, true);
            SetRectScale(portraitImage.gameObject, Vector3.one);
        }
    }

    private static string DescribeObject(GameObject target)
    {
        if (target == null) return "null";
        Transform t = target.transform;
        return $"{target.name}(activeSelf={target.activeSelf},activeInHierarchy={target.activeInHierarchy},scale={t.localScale})";
    }

    private static void SetUiObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private GameObject FindDialogChild(string name)
    {
        if (content == null || string.IsNullOrWhiteSpace(name)) return null;

        Transform[] children = content.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i].gameObject;
            }
        }

        return null;
    }

    private static void SetRectScale(GameObject target, Vector3 scale)
    {
        if (target == null) return;
        if (target.transform.localScale != scale)
        {
            target.transform.localScale = scale;
        }
    }

    private class DialogRequest
    {
        public string title;
        public string message;
        public string yesString;
        public string noString;
        public List<string> options;
        public List<string> optionDescriptions;
        public Sprite portrait;
        public TaskCompletionSource<string> tcs;
    }

    private sealed class CloseButtonFallback : MonoBehaviour, IPointerClickHandler
    {
        private SelectionDialog dialog;

        public void Bind(SelectionDialog owner)
        {
            dialog = owner;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            dialog?.CloseCurrentSelection();
        }
    }

    public static void CloseAll()
    {
        if (Instance == null) return;
        Instance.ForceClose();
    }

    private void ForceClose()
    {
        DialogRequest requestToClose = activeRequest;
        requestToClose?.tcs?.TrySetResult(string.Empty);
        for (int i = 0; i < queuedRequests.Count; i++)
        {
            queuedRequests[i]?.tcs?.TrySetResult(string.Empty);
        }
        queuedRequests.Clear();
        activeRequest = null;
        HideInstant();
    }

    private void UpdatePortrait(Sprite portrait)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }
        if (portraitCanvasGroup != null)
        {
            portraitCanvasGroup.alpha = 1f;
        }
    }

    private bool HasValidSelection()
    {
        if (UseButtonList)
            return selectedButtonIndex >= 0 && selectedButtonIndex < (activeRequest?.options.Count ?? 0);

        return dropdown != null
            && dropdown.options != null
            && dropdown.options.Count > 1
            && dropdown.value > 0;
    }

    private void UpdateCloseButtonState()
    {
        if (noButton != null)
        {
            noButton.interactable = HasValidSelection();
        }
    }

    private void BindUiReferences()
    {
        if (content == null)
        {
            content = FindDialogChild("Content");
        }

        if (messageLabel == null)
        {
            messageLabel = FindTextChild("Text");
        }

        if (noButton == null)
        {
            GameObject closeButton = FindDialogChild("CloseButton") ?? FindDialogChild("NoButton");
            if (closeButton != null)
            {
                noButton = closeButton.GetComponent<Button>();
            }
        }

        if (dropdown == null)
        {
            GameObject dropdownObject = FindDialogChild("Dropdown");
            if (dropdownObject != null)
            {
                dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            }
        }

        if (portraitImage == null)
        {
            GameObject portraitObject = FindDialogChild("CharacterImage");
            if (portraitObject != null)
            {
                portraitImage = portraitObject.GetComponent<Image>();
            }
        }

        if (portraitCanvasGroup == null)
        {
            GameObject portraitBg = FindDialogChild("CharacterImageBg");
            if (portraitBg != null)
            {
                portraitCanvasGroup = portraitBg.GetComponent<CanvasGroup>();
            }
        }

        if (title == null)
        {
            title = FindTextChild("Title");
        }

        if (illustrations == null)
        {
            illustrations = FindFirstObjectByType<Illustrations>();
        }

        if (optionButtonsContainer == null)
        {
            GameObject containerObj = FindDialogChild("OptionsContainer") ?? FindDialogChild("OptionButtons");
            if (containerObj != null) optionButtonsContainer = containerObj.transform;
        }

        if (optionButtonsContainer != null)
        {
            RectTransform containerRect = optionButtonsContainer.GetComponent<RectTransform>();
            if (containerRect != null)
                containerRect.pivot = new Vector2(containerRect.pivot.x, 0f);

            ContentSizeFitter csf = optionButtonsContainer.GetComponent<ContentSizeFitter>()
                ?? optionButtonsContainer.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            VerticalLayoutGroup vlg = optionButtonsContainer.GetComponent<VerticalLayoutGroup>()
                ?? optionButtonsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.reverseArrangement = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
        }
    }

    private void WireUiListeners()
    {
        if (noButton != null)
        {
            noButton.onClick.RemoveAllListeners();
            noButton.onClick.AddListener(CloseCurrentSelection);
            EnsureCloseButtonFallback(noButton.gameObject);
        }

        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.onValueChanged.AddListener(_ =>
            {
                UpdateCloseButtonState();
                UpdateCaptionColor();
            });
        }
    }

    private void EnsureCloseButtonFallback(GameObject closeButtonObject)
    {
        if (closeButtonObject == null) return;

        CloseButtonFallback fallback = closeButtonObject.GetComponent<CloseButtonFallback>();
        if (fallback == null)
        {
            fallback = closeButtonObject.AddComponent<CloseButtonFallback>();
        }
        fallback.Bind(this);
    }

    private TextMeshProUGUI FindTextChild(string name)
    {
        GameObject child = FindDialogChild(name);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void UpdateCaptionColor()
    {
        if (dropdown?.captionText == null)
        {
            return;
        }

        dropdown.captionText.color = HasValidSelection()
            ? dropdown.options[dropdown.value].color
            : Color.white;
    }

    private bool IsProtectedDialogContainer(GameObject target)
    {
        if (target == null) return false;
        if (target == content) return true;
        if (portraitCanvasGroup != null && target == portraitCanvasGroup.gameObject) return true;

        string name = target.name;
        return string.Equals(name, "Content", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Image", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "CharacterImageBg", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Color GetReadableRandomColor()
    {
        float hue = Random.value;
        float saturation = Random.Range(0.55f, 1f);
        float value = Random.Range(0.8f, 1f);
        return Color.HSVToRGB(hue, saturation, value);
    }

    private static string FormatOptionLabel(string text, Color color)
    {
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{colorHex}>{text}</color>";
    }

    private static string FormatTitle(string text)
    {
        return $"<sprite name=\"ring2\"> {text} <sprite name=\"ring2\">";
    }

    // ── Typewriter helpers ────────────────────────────────────────────────────

    private void EnsureMessageTypewriter()
    {
        if (messageLabel == null) return;
        if (messageTypewriter == null)
        {
            messageTypewriter = messageLabel.GetComponent<TypewriterEffect>()
                ?? messageLabel.gameObject.AddComponent<TypewriterEffect>();
        }
        messageTypewriter.textMeshPro = messageLabel;
        messageTypewriter.typingSpeed = 28f;
    }

    // ── Option button list ────────────────────────────────────────────────────

    private void BuildOptionButtons(List<string> options, List<string> descriptions = null)
    {
        ClearOptionButtons();
        if (options == null || optionButtonsContainer == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            Color color = GetReadableRandomColor();
            string desc = descriptions != null && i < descriptions.Count ? descriptions[i] : string.Empty;
            optionButtons.Add(CreateOptionButton(options[i], desc, color, i));
        }

        if (Application.isPlaying)
        {
            if (buttonAnimCoroutine != null) StopCoroutine(buttonAnimCoroutine);
            buttonAnimCoroutine = StartCoroutine(AnimateButtonsIn());
        }
        else
        {
            foreach (Button btn in optionButtons)
            {
                CanvasGroup cg = btn != null ? btn.GetComponent<CanvasGroup>() : null;
                if (cg != null) { cg.alpha = 1f; btn.transform.localScale = Vector3.one; }
            }
        }
    }

    private void ClearOptionButtons()
    {
        if (buttonAnimCoroutine != null) { StopCoroutine(buttonAnimCoroutine); buttonAnimCoroutine = null; }
        foreach (Button btn in optionButtons)
        {
            if (btn == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(btn.gameObject); continue; }
#endif
            Destroy(btn.gameObject);
        }
        optionButtons.Clear();
        selectedButtonIndex = -1;
    }

    private Button CreateOptionButton(string text, string description, Color textColor, int index)
    {
        bool hasDesc = !string.IsNullOrWhiteSpace(description);
        string colorHex = ColorUtility.ToHtmlStringRGB(textColor);
        string labelText = hasDesc
            ? $"<color=#{colorHex}>{text}</color>\n<color=#9C9C9C><size=11>{description}</size></color>"
            : $"<color=#{colorHex}>{text}</color>";

        GameObject obj;
        if (optionButtonPrefab != null)
        {
            obj = Instantiate(optionButtonPrefab, optionButtonsContainer, false);
            obj.name = $"Option_{index}";
        }
        else
        {
            obj = new($"Option_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(optionButtonsContainer, false);

            Image bg = obj.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.06f, 0.04f, 0.88f);

            Button btn0 = obj.GetComponent<Button>();
            btn0.targetGraphic = bg;
            ColorBlock cb0 = btn0.colors;
            cb0.normalColor      = new Color(0.08f, 0.06f, 0.04f, 0.88f);
            cb0.highlightedColor = new Color(0.32f, 0.22f, 0.08f, 0.95f);
            cb0.selectedColor    = new Color(0.42f, 0.30f, 0.10f, 1f);
            cb0.pressedColor     = new Color(0.55f, 0.40f, 0.12f, 1f);
            cb0.fadeDuration     = 0.08f;
            btn0.colors = cb0;

            GameObject arrowObj = new("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowObj.transform.SetParent(obj.transform, false);
            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0f, 0f);
            arrowRect.anchorMax = new Vector2(0f, 1f);
            arrowRect.pivot     = new Vector2(0f, 0.5f);
            arrowRect.offsetMin = new Vector2(8f,  0f);
            arrowRect.offsetMax = new Vector2(24f, 0f);
            TextMeshProUGUI arrowTmp0 = arrowObj.GetComponent<TextMeshProUGUI>();
            arrowTmp0.text          = ">";
            arrowTmp0.fontSize      = 13f;
            arrowTmp0.alignment     = TextAlignmentOptions.Midline;
            arrowTmp0.raycastTarget = false;
            if (messageLabel != null) { arrowTmp0.font = messageLabel.font; arrowTmp0.fontSharedMaterial = messageLabel.fontSharedMaterial; }

            GameObject labelObj = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(obj.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(26f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            TextMeshProUGUI labelTmp0 = labelObj.GetComponent<TextMeshProUGUI>();
            labelTmp0.color           = Color.white;
            labelTmp0.fontSize        = 14f;
            labelTmp0.fontSizeMin     = 10f;
            labelTmp0.fontSizeMax     = 14f;
            labelTmp0.enableAutoSizing = true;
            labelTmp0.alignment       = TextAlignmentOptions.MidlineLeft;
            labelTmp0.raycastTarget   = false;
            if (messageLabel != null) { labelTmp0.font = messageLabel.font; labelTmp0.fontSharedMaterial = messageLabel.fontSharedMaterial; }
        }

        CanvasGroup cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        LayoutElement le = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        le.preferredHeight = hasDesc ? 54f : 36f;
        le.minHeight       = hasDesc ? 44f : 28f;

        Transform arrowChild = obj.transform.Find("Arrow");
        if (arrowChild != null)
        {
            TextMeshProUGUI arrowTmp = arrowChild.GetComponent<TextMeshProUGUI>();
            if (arrowTmp != null) arrowTmp.color = textColor;
        }

        Transform labelChild = obj.transform.Find("Label");
        if (labelChild != null)
        {
            TextMeshProUGUI labelTmp = labelChild.GetComponent<TextMeshProUGUI>();
            if (labelTmp != null) labelTmp.text = labelText;
        }

        Button btn = obj.GetComponent<Button>();
        int capturedIndex = index;
        btn.onClick.AddListener(() => SelectOptionButton(capturedIndex));

        return btn;
    }

    private void SelectOptionButton(int index)
    {
        selectedButtonIndex = index;
        UpdateButtonSelectionVisuals();
        UpdateCloseButtonState();
    }

    private void UpdateButtonSelectionVisuals()
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] == null) continue;
            Image bg = optionButtons[i].GetComponent<Image>();
            if (bg == null) continue;
            bg.color = i == selectedButtonIndex
                ? new Color(0.42f, 0.30f, 0.10f, 1f)
                : new Color(0.08f, 0.06f, 0.04f, 0.88f);
        }
    }

    private IEnumerator AnimateButtonsIn()
    {
        yield return null; // let layout settle
        for (int i = 0; i < optionButtons.Count; i++)
        {
            Button btn = optionButtons[i];
            if (btn == null) continue;
            CanvasGroup cg = btn.GetComponent<CanvasGroup>() ?? btn.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(FadeScaleInButton(btn.transform, cg));
            yield return new WaitForSecondsRealtime(0.04f);
        }
        buttonAnimCoroutine = null;
    }

    private static IEnumerator FadeScaleInButton(Transform t, CanvasGroup cg)
    {
        float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cg.alpha = p;
            float s = Mathf.Lerp(0.92f, 1f, p);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        cg.alpha = 1f;
        t.localScale = Vector3.one;
    }

    public Sprite GetCharacterIllustration(Character character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterName)) return null;
        return illustrations != null ? illustrations.GetIllustrationByName(character.characterName) : null;
    }

#if UNITY_EDITOR
    public void EditorRenderExample()
    {
        BindUiReferences();
        WireUiListeners();

        if (content != null) content.SetActive(true);
        EnsureDialogHierarchyActive();

        var options = new List<string>
        {
            "Stand Beneath The Black Wing",
            "Vanish Beneath Doom",
            "Cry Up To Cataclysm"
        };
        var descriptions = new List<string>
        {
            "Face the world-shaking wyrm with all the strength you can gather before fire and shadow erase the field itself.",
            "Use perfect timing and a hero's nerve to slip where such colossal destruction is least likely to fall.",
            "Attempt the impossible and speak to the black doom as though pride, darkness, or tribute might stay it for a breath."
        };

        activeRequest = new DialogRequest
        {
            title = "Ancalon",
            message = "You were crossing a mountain pass when the shadow of something vast swallowed the sun, and the stone beneath your feet began to tremble with each distant wingbeat.",
            yesString = "Decide",
            noString = "Cancel",
            options = options,
            optionDescriptions = descriptions,
            portrait = null,
            tcs = null
        };

        if (title != null) { title.text = FormatTitle(activeRequest.title); title.gameObject.SetActive(true); }
        if (messageLabel != null) messageLabel.text = activeRequest.message;

        if (UseButtonList)
        {
            if (dropdown != null) dropdown.gameObject.SetActive(false);
            BuildOptionButtons(options, descriptions); // resets selectedButtonIndex to -1 internally
            selectedButtonIndex = 0;                  // re-apply after build
            UpdateButtonSelectionVisuals();
        }
        else if (dropdown != null)
        {
            dropdown.gameObject.SetActive(true);
            dropdownOptions.Clear();
            dropdownOptions.Add(new TMP_Dropdown.OptionData("Decide"));
            options.ForEach(x =>
            {
                Color c = GetReadableRandomColor();
                dropdownOptions.Add(new TMP_Dropdown.OptionData(FormatOptionLabel(x, c)) { color = c });
            });
            dropdown.ClearOptions();
            dropdown.AddOptions(dropdownOptions);
            dropdown.SetValueWithoutNotify(1);
            dropdown.RefreshShownValue();
            UpdateCaptionColor();
        }

        UpdateCloseButtonState();
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    public void EditorHide()
    {
        activeRequest = null;
        HideInstant();
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }
#endif
}
