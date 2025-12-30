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

    private TaskCompletionSource<string> pendingRequest;
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
    public static Task<string> Ask(string message, string yesString, string noString, List<string> options, bool isAI)
    {
        if (Instance == null)
        {
            Debug.LogError("Selection dialog  was called before its instance was created.");
            return Task.FromResult(string.Empty);
        }

        return Instance.Show(message, yesString, noString, options, isAI);
    }

    private Task<string> Show(string message, string yesString, string noString, List<string> options, bool isAI)
    {
        if (pendingRequest != null && !pendingRequest.Task.IsCompleted)
        {
            Debug.LogWarning("Selection dialog already running. Previous request cancelled.");
            pendingRequest.TrySetResult(string.Empty);
        }

        if(options.Count < 1)
        {
            Debug.LogWarning("Unable to show Selection Dialog: options < 1");
            pendingRequest.TrySetResult(string.Empty);
        }

        pendingRequest = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(isAI) {
            int index = Random.Range(0, options.Count);
            pendingRequest.TrySetResult(options[index]);
        } 
        else
        {
            var request = new DialogRequest
            {
                message = message,
                yesString = yesString,
                noString = noString,
                options = options
            };
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
        return pendingRequest.Task;
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
        pendingRequest?.TrySetResult(answer);
        pendingRequest = null;
        pendingDisplay = null;
    }

    private void HideInstant()
    {
        content.SetActive(false);
        IsShowing = false;
    }

    private void ShowInternal(DialogRequest request)
    {
        if (request == null) return;
        content.SetActive(true);
        IsShowing = true;

        messageLabel.text = request.message;
        yesButtonText.text = request.yesString;
        noButtonText.text = request.noString;
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
        pendingRequest?.TrySetResult(string.Empty);
        pendingRequest = null;
        pendingDisplay = null;
        HideInstant();
    }
}
