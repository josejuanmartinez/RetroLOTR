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
    [SerializeField] private TextMeshProUGUI optionDescriptionLabel;
    [SerializeField] private TextMeshProUGUI title;

    private readonly List<DialogRequest> queuedRequests = new();
    private readonly List<TMP_Dropdown.OptionData> dropdownOptions = new();
    private int activeIndex = -1;
    private DialogRequest activeRequest;
    private GameObject optionDescriptionPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        EnsureOptionDescriptionUi();

        if (noButton != null)
        {
            noButton.onClick.AddListener(() => Resolve(GetSelectedOptionText()));
        }
        if (dropdown != null)
        {
            dropdown.onValueChanged.AddListener(_ =>
            {
                UpdateCloseButtonState();
                UpdateCaptionColor();
                UpdateOptionDescription();
            });
        }

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

    private string GetSelectedOptionText()
    {
        if (dropdown == null || activeRequest == null || !HasValidSelection())
        {
            return string.Empty;
        }

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
        requestToResolve?.tcs.TrySetResult(answer);
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
        DebugLogHierarchyState("HideInstant before");
        content.SetActive(false);
        IsShowing = false;
        activeRequest = null;
        SetOptionDescriptionVisible(false);
        UpdatePortrait(null);
        DebugLogHierarchyState("HideInstant after");
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

    private void ShowInternal(DialogRequest request)
    {
        if (request == null) return;
        DebugLogHierarchyState("ShowInternal before");
        content.SetActive(true);
        EnsureDialogHierarchyActive();
        IsShowing = true;
        activeRequest = request;

        bool hasCustomTitle = !string.IsNullOrWhiteSpace(request.title);
        if (messageLabel != null)
        {
            messageLabel.text = request.message;
        }
        if (title != null)
        {
            title.text = hasCustomTitle ? request.title : string.Empty;
            title.gameObject.SetActive(!string.IsNullOrWhiteSpace(title.text));
        }
        UpdatePortrait(request.portrait);
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
        UpdateOptionDescription();
        UpdateCloseButtonState();
        DebugLogHierarchyState("ShowInternal after");
    }

    private void EnsureDialogHierarchyActive()
    {
        SetUiObjectActive(content, true);
        SetRectScale(content, Vector3.one);
        SetUiObjectActive(messageLabel != null ? messageLabel.gameObject : null, true);
        SetUiObjectActive(noButton != null ? noButton.gameObject : null, true);
        SetUiObjectActive(dropdown != null ? dropdown.gameObject : null, true);
        SetUiObjectActive(optionDescriptionPanel, true);

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
        DebugLogHierarchyState("EnsureDialogHierarchyActive");
    }

    private void DebugLogHierarchyState(string prefix)
    {
        string contentState = DescribeObject(content);
        string imageState = DescribeObject(FindDialogChild("Image"));
        string bgState = DescribeObject(portraitCanvasGroup != null ? portraitCanvasGroup.gameObject : FindDialogChild("CharacterImageBg"));
        string portraitState = DescribeObject(portraitImage != null ? portraitImage.gameObject : FindDialogChild("CharacterImage"));
        string dropdownState = DescribeObject(dropdown != null ? dropdown.gameObject : FindDialogChild("Dropdown"));
        string buttonState = DescribeObject(noButton != null ? noButton.gameObject : FindDialogChild("CloseButton"));
        Debug.Log($"[SelectionDialog] {prefix} | content={contentState} | image={imageState} | bg={bgState} | portrait={portraitState} | dropdown={dropdownState} | close={buttonState}");
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
        activeIndex = -1;
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

    private void UpdateOptionDescription()
    {
        if (optionDescriptionLabel == null)
        {
            return;
        }

        string description = string.Empty;
        if (activeRequest?.optionDescriptions != null && HasValidSelection())
        {
            int optionIndex = dropdown.value - 1;
            if (optionIndex >= 0 && optionIndex < activeRequest.optionDescriptions.Count)
            {
                description = activeRequest.optionDescriptions[optionIndex];
            }
        }

        optionDescriptionLabel.text = description ?? string.Empty;
        SetOptionDescriptionVisible(!string.IsNullOrWhiteSpace(optionDescriptionLabel.text));
    }

    private void EnsureOptionDescriptionUi()
    {
        if (optionDescriptionLabel != null)
        {
            GameObject existingParent = optionDescriptionLabel.transform.parent != null
                ? optionDescriptionLabel.transform.parent.gameObject
                : null;

            // In the current scene the serialized description label may live directly under
            // the main dialog branch. Never treat a broad container like CharacterImageBg/Image/Content
            // as the optional description panel, or hiding the description will hide the whole dialog.
            if (existingParent != null
                && !IsProtectedDialogContainer(existingParent))
            {
                optionDescriptionPanel = existingParent;
            }
            else
            {
                optionDescriptionPanel = null;
            }
            SetOptionDescriptionVisible(false);
            return;
        }

        if (dropdown == null)
        {
            return;
        }

        RectTransform parent = dropdown.transform.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        optionDescriptionPanel = new GameObject("OptionDescriptionPanel", typeof(RectTransform), typeof(Image));
        optionDescriptionPanel.transform.SetParent(parent, false);

        RectTransform panelRect = optionDescriptionPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 52f);
        panelRect.sizeDelta = new Vector2(-10f, 74f);

        Image panelImage = optionDescriptionPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        GameObject labelObject = new("OptionDescriptionLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(optionDescriptionPanel.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 8f);
        labelRect.offsetMax = new Vector2(-10f, -8f);

        optionDescriptionLabel = labelObject.GetComponent<TextMeshProUGUI>();
        if (messageLabel != null)
        {
            optionDescriptionLabel.font = messageLabel.font;
            optionDescriptionLabel.fontSharedMaterial = messageLabel.fontSharedMaterial;
        }
        optionDescriptionLabel.fontSize = 14f;
        optionDescriptionLabel.enableAutoSizing = true;
        optionDescriptionLabel.fontSizeMin = 10f;
        optionDescriptionLabel.fontSizeMax = 14f;
        optionDescriptionLabel.color = Color.white;
        optionDescriptionLabel.alignment = TextAlignmentOptions.TopLeft;
        optionDescriptionLabel.enableWordWrapping = true;
        optionDescriptionLabel.raycastTarget = false;
        optionDescriptionLabel.text = string.Empty;

        SetOptionDescriptionVisible(false);
    }

    private void SetOptionDescriptionVisible(bool visible)
    {
        if (optionDescriptionPanel != null)
        {
            optionDescriptionPanel.SetActive(visible);
        }
        else if (optionDescriptionLabel != null)
        {
            optionDescriptionLabel.gameObject.SetActive(visible);
        }
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

    public Sprite GetCharacterIllustration(Character character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterName)) return null;
        return illustrations != null ? illustrations.GetIllustrationByName(character.characterName) : null;
    }
}
