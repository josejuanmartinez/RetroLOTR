using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionDialog : MonoBehaviour
{
    public static SelectionDialog Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private TextMeshProUGUI yesButtonText;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI noButtonText;
    [SerializeField] private TMP_Dropdown dropdown;

    private TaskCompletionSource<string> pendingRequest;

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
            dropdown.value = Random.Range(0, options.Count);        
        } 
        else
        {
            content.SetActive(true);

            messageLabel.text = message;
            yesButtonText.text = yesString;
            noButtonText.text = noString;
            dropdown.options = new ();
            options.ForEach(x => dropdown.options.Add(new TMP_Dropdown.OptionData(x)));
            dropdown.value = 0;
            dropdown.RefreshShownValue();            
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
    }

    private void HideInstant()
    {
        content.SetActive(false);
    }
}
