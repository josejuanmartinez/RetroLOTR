using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
    public static ConfirmationDialog Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI yesButtonText;
    [SerializeField] private TextMeshProUGUI noButtonText;

    [Header("Defaults")]
    [TextArea]
    [SerializeField] private string fallbackMessage = "Are you sure?";
    [SerializeField] private string defaultYesLabel = "Yes";
    [SerializeField] private string defaultNoLabel = "No";
    [SerializeField] private string defaultOkLabel = "OK";

    private TaskCompletionSource<bool> pendingRequest;
    private Action pendingOnClose;

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
        if (pendingRequest != null && !pendingRequest.Task.IsCompleted)
        {
            Debug.LogWarning("Confirmation dialog already running. Previous request cancelled.");
            pendingRequest.TrySetResult(false);
            pendingOnClose?.Invoke();
        }

        pendingRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingOnClose = onClose;

        content.SetActive(true);

        messageLabel.text = message;
        yesButtonText.text = yesString;
        yesButton.gameObject.SetActive(true);

        bool showNo = !singleButton;
        noButton.gameObject.SetActive(showNo);
        if (showNo)
        {
            noButtonText.text = string.IsNullOrWhiteSpace(noString) ? defaultNoLabel : noString;
        }

        return pendingRequest.Task;
    }

    private void Resolve(bool answer)
    {
        HideInstant();
        pendingRequest?.TrySetResult(answer);
        pendingOnClose?.Invoke();
        pendingRequest = null;
        pendingOnClose = null;
    }

    private void HideInstant()
    {
        content.SetActive(false);
    }
}
