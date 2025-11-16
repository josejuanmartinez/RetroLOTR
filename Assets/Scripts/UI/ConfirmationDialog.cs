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

    private TaskCompletionSource<bool> pendingRequest;

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
    public static Task<bool> Ask(string message, string yesString, string noString)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(message, yesString, noString);
    }

    /// <summary>
    /// Opens a confirmation dialog that uses the configured fallback message.
    /// </summary>
    public static Task<bool> Ask(string yesString, string noString)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(Instance.fallbackMessage, yesString, noString);
    }

    /// <summary>
    /// Opens a Yes/No dialog using the default button texts.
    /// </summary>
    public static Task<bool> AskYesNo(string message)
    {
        if (Instance == null)
        {
            Debug.LogError("ConfirmationDialog was called before its instance was created.");
            return Task.FromResult(false);
        }

        return Instance.Show(message, Instance.defaultYesLabel, Instance.defaultNoLabel);
    }

    private Task<bool> Show(string message, string yesString, string noString)
    {
        if (pendingRequest != null && !pendingRequest.Task.IsCompleted)
        {
            Debug.LogWarning("Confirmation dialog already running. Previous request cancelled.");
            pendingRequest.TrySetResult(false);
        }

        pendingRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        content.SetActive(true);

        messageLabel.text = message;
        yesButtonText.text = yesString;
        noButtonText.text = noString;

        return pendingRequest.Task;
    }

    private void Resolve(bool answer)
    {
        HideInstant();
        pendingRequest?.TrySetResult(answer);
        pendingRequest = null;
    }

    private void HideInstant()
    {
        content.SetActive(false);
    }
}
