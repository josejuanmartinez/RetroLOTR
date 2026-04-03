using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionDialog : MonoBehaviour
{
    public static SelectionDialog Instance { get; private set; }
    public static bool IsShowing { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private TextMeshProUGUI yesButtonText;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI noButtonText;
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private Image portraitImage;
    [SerializeField] private CanvasGroup portraitCanvasGroup;
    [SerializeField] private Illustrations illustrations;

    private readonly List<DialogRequest> queuedRequests = new();
    private int activeIndex = -1;
    private DialogRequest pendingDisplay;
    private Coroutine waitForMessagesRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();

        yesButton.onClick.AddListener(() => Resolve(GetSelectedOptionText()));
        noButton.onClick.AddListener(() => Resolve(string.Empty));

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
    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, bool isAI, Sprite portrait = null)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, isAI, portrait, EventIconType.MultiChoice);
    }

    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, bool isAI, Sprite portrait, EventIconType iconType)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, isAI, portrait, iconType);
    }

    private Task<string> Show(string message, string yesString, string noString, List<string> options, bool isAI, Sprite portrait, EventIconType iconType)
    {
        if (options.Count < 1)
        {
            Debug.LogWarning("Unable to show Selection Dialog: options < 1");
            return Task.FromResult(string.Empty);
        }

        var request = new DialogRequest
        {
            message = message,
            yesString = yesString,
            noString = noString,
            options = options,
            portrait = portrait,
            tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        if (isAI)
        {
            int index = Random.Range(0, options.Count);
            request.tcs.TrySetResult(options[index]);
        }
        else
        {
            EventIconsManager iconsManager = EventIconsManager.FindManager();
            if (iconsManager == null)
            {
                QueueRequest(request);
            }
            else
            {
                iconsManager.AddEventIcon(
                    iconType,
                    false,
                    () => QueueRequest(request));
            }
        }
        return request.tcs.Task;
    }

    private void QueueRequest(DialogRequest request)
    {
        if (request == null) return;
        if (!queuedRequests.Contains(request))
        {
            queuedRequests.Add(request);
        }
        activeIndex = queuedRequests.IndexOf(request);
        if (ShouldDelayDialog())
        {
            pendingDisplay = request;
            HideInstant();
            StartWaitForMessages();
        }
        else
        {
            ShowInternal(request);
        }
    }

    private string GetSelectedOptionText()
    {
        if (dropdown.options.Count == 0)
        {
            return string.Empty;
        }

        return dropdown.options[dropdown.value].text;
    }

    private void Resolve(string answer)
    {
        HideInstant();
        if (activeIndex >= 0 && activeIndex < queuedRequests.Count)
        {
            queuedRequests[activeIndex].tcs.TrySetResult(answer);
            queuedRequests.RemoveAt(activeIndex);
        }
        activeIndex = -1;
        pendingDisplay = null;
    }

    private void HideInstant()
    {
        content.SetActive(false);
        IsShowing = false;
        UpdatePortrait(null);
    }

    private void ShowInternal(DialogRequest request)
    {
        if (request == null) return;
        content.SetActive(true);
        IsShowing = true;

        messageLabel.text = request.message;
        yesButtonText.text = request.yesString;
        noButtonText.text = request.noString;
        bool showNoButton = !string.IsNullOrWhiteSpace(request.noString);
        noButton.gameObject.SetActive(showNoButton);
        UpdatePortrait(request.portrait);
        dropdown.options = new ();
        request.options.ForEach(x => dropdown.options.Add(new TMP_Dropdown.OptionData(x)));
        dropdown.value = 0;
        dropdown.RefreshShownValue();
        pendingDisplay = null;
    }

    private bool ShouldDelayDialog()
    {
        bool focusPending = BoardNavigator.Instance != null && BoardNavigator.Instance.HasPendingFocus();
        return MessageDisplay.IsBusy() || MessageDisplayNoUI.IsBusy() || PopupManager.IsShowing || focusPending;
    }

    private void StartWaitForMessages()
    {
        if (waitForMessagesRoutine != null) return;
        waitForMessagesRoutine = StartCoroutine(WaitForMessages());
    }

    private IEnumerator WaitForMessages()
    {
        while (ShouldDelayDialog())
        {
            yield return null;
        }
        waitForMessagesRoutine = null;
        if (pendingDisplay != null)
        {
            ShowInternal(pendingDisplay);
        }
    }

    private class DialogRequest
    {
        public string message;
        public string yesString;
        public string noString;
        public List<string> options;
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
        if (waitForMessagesRoutine != null)
        {
            StopCoroutine(waitForMessagesRoutine);
            waitForMessagesRoutine = null;
        }
        for (int i = 0; i < queuedRequests.Count; i++)
        {
            queuedRequests[i]?.tcs?.TrySetResult(string.Empty);
        }
        queuedRequests.Clear();
        activeIndex = -1;
        pendingDisplay = null;
        HideInstant();
    }

    private void UpdatePortrait(Sprite portrait)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
        }
        if (portraitCanvasGroup != null)
        {
            portraitCanvasGroup.alpha = portrait != null ? 1f : 0f;
        }
    }

    public Sprite GetCharacterIllustration(Character character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterName)) return null;
        return illustrations != null ? illustrations.GetIllustrationByName(character.characterName) : null;
    }
}
