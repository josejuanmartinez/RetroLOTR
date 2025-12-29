using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
    public static ConfirmationDialog Instance { get; private set; }
    public static bool IsShowing { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI yesButtonText;
    [SerializeField] private TextMeshProUGUI noButtonText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Defaults")]
    [TextArea]
    [SerializeField] private string fallbackMessage = "Are you sure?";
    [SerializeField] private string defaultYesLabel = "Yes";
    [SerializeField] private string defaultNoLabel = "No";
    [SerializeField] private string defaultOkLabel = "OK";

    private TaskCompletionSource<bool> pendingRequest;
    private Action pendingOnClose;
    private readonly List<DialogRequest> queuedRequests = new();
    private int activeIndex = -1;
    private Coroutine waitForMessagesRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        yesButton.onClick.AddListener(() => Resolve(true));
        noButton.onClick.AddListener(() => Resolve(false));
        if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
        if (nextButton != null) nextButton.onClick.AddListener(ShowNext);

        DontDestroyOnLoad(gameObject);
        HideInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Opens a confirmation dialog with a custom message and button labels.
    /// </summary>
    public static Task<bool> Ask(string message, string yesString, string noString, Action onClose = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(message, yesString, noString, false, onClose);
    }

    /// <summary>
    /// Opens a confirmation dialog that uses the configured fallback message.
    /// </summary>
    public static Task<bool> Ask(string yesString, string noString, Action onClose = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(Instance.fallbackMessage, yesString, noString, false, onClose);
    }

    /// <summary>
    /// Opens a Yes/No dialog using the default button texts.
    /// </summary>
    public static Task<bool> AskYesNo(string message, Action onClose = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(message, Instance.defaultYesLabel, Instance.defaultNoLabel, false, onClose);
    }

    /// <summary>
    /// Opens a single-button OK dialog.
    /// </summary>
    public static Task<bool> AskOk(string message, Action onClose = null)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        string okLabel = string.IsNullOrWhiteSpace(Instance.defaultOkLabel) ? "OK" : Instance.defaultOkLabel;
        return Instance.Show(message, okLabel, string.Empty, true, onClose);
    }

    private Task<bool> Show(string message, string yesString, string noString, bool singleButton = false, Action onClose = null)
    {
        var request = new DialogRequest
        {
            message = message,
            yesString = yesString,
            noString = noString,
            singleButton = singleButton,
            onClose = onClose,
            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        queuedRequests.Add(request);
        if (activeIndex < 0) activeIndex = 0;

        ShowActive();
        return request.tcs.Task;
    }

    private void Resolve(bool answer)
    {
        HideInstant();
        pendingRequest?.TrySetResult(answer);
        pendingOnClose?.Invoke();
        pendingRequest = null;
        pendingOnClose = null;

        if (activeIndex >= 0 && activeIndex < queuedRequests.Count)
        {
            queuedRequests.RemoveAt(activeIndex);
            if (queuedRequests.Count == 0)
            {
                activeIndex = -1;
            }
            else
            {
                activeIndex = Mathf.Clamp(activeIndex, 0, queuedRequests.Count - 1);
            }
        }

        ShowActive();
    }

    private void HideInstant()
    {
        content.SetActive(false);
        IsShowing = false;
    }

    private void ShowActive()
    {
        if (queuedRequests.Count == 0)
        {
            HideInstant();
            return;
        }

        if (ShouldDelayDialog())
        {
            HideInstant();
            StartWaitForMessages();
            return;
        }

        activeIndex = Mathf.Clamp(activeIndex, 0, queuedRequests.Count - 1);
        var activeRequest = queuedRequests[activeIndex];

        pendingRequest = activeRequest.tcs;
        pendingOnClose = activeRequest.onClose;

        content.SetActive(true);
        IsShowing = true;

        messageLabel.text = string.IsNullOrWhiteSpace(activeRequest.message) ? fallbackMessage : activeRequest.message;
        yesButtonText.text = string.IsNullOrWhiteSpace(activeRequest.yesString) ? defaultYesLabel : activeRequest.yesString;
        yesButton.gameObject.SetActive(true);

        bool showNo = !activeRequest.singleButton;
        noButton.gameObject.SetActive(showNo);
        if (showNo)
        {
            noButtonText.text = string.IsNullOrWhiteSpace(activeRequest.noString) ? defaultNoLabel : activeRequest.noString;
        }

        bool canPrev = activeIndex > 0;
        bool canNext = activeIndex < queuedRequests.Count - 1;
        if (previousButton != null) previousButton.gameObject.SetActive(canPrev);
        if (nextButton != null) nextButton.gameObject.SetActive(canNext);
    }

    private bool ShouldDelayDialog()
    {
        return MessageDisplay.IsBusy() || MessageDisplayNoUI.IsBusy();
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
        ShowActive();
    }

    private void ShowPrevious()
    {
        if (queuedRequests.Count < 2) return;
        activeIndex = Mathf.Max(0, activeIndex - 1);
        ShowActive();
    }

    private void ShowNext()
    {
        if (queuedRequests.Count < 2) return;
        activeIndex = Mathf.Min(queuedRequests.Count - 1, activeIndex + 1);
        ShowActive();
    }

    private class DialogRequest
    {
        public string message;
        public string yesString;
        public string noString;
        public bool singleButton;
        public Action onClose;
        public TaskCompletionSource<bool> tcs;
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
        pendingRequest?.TrySetResult(false);
        pendingOnClose?.Invoke();
        pendingRequest = null;
        pendingOnClose = null;
        queuedRequests.Clear();
        activeIndex = -1;
        HideInstant();
    }
}
