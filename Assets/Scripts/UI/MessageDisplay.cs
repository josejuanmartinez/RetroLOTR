using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class MessageDisplay : MonoBehaviour
{
    private static MessageDisplay instance;
    private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float displayDuration = 1f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Queue<MessageData> messageQueue = new Queue<MessageData>();
    private bool isDisplayingMessage = false;
    private bool persistentActive = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // Singleton pattern to ensure only one instance exists
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Hide message initially
        canvasGroup.alpha = 0f;
        messageText.enabled = false;
    }

    /// <summary>
    /// Static method to display a message in the center of the screen
    /// </summary>
    /// <param name="message">Text message to display</param>
    /// <param name="color">Color for the text (defaults to white if not specified)</param>
    public static void ShowMessage(string message, Color? color = null)
    {
        Game game = FindFirstObjectByType<Game>();
        if (game == null) return;
        if (!game.started || game.currentlyPlaying != game.player) return;
        Color resolved = color ?? Color.white;
        if (IsNegativeColor(resolved))
        {
            Sounds.Instance?.PlayNegative();
        }
        else if (IsPositiveColor(resolved))
        {
            Sounds.Instance?.PlayPositive();
        }
        else
        {
            Sounds.Instance?.PlayMessage();
        }
        instance.EnqueueMessage(message, resolved);
    }

    public static bool IsBusy()
    {
        if (instance == null) return false;
        return instance.persistentActive || instance.isDisplayingMessage || instance.messageQueue.Count > 0;
    }

    /// <summary>
    /// Show a persistent message (no fade, stays until cleared). Used for turn banners.
    /// </summary>
    public static void ShowPersistent(string message, Color? color = null)
    {
        if (instance == null) return;
        instance.SetPersistent(message, color ?? Color.white);
    }

    /// <summary>
    /// Clear any persistent message.
    /// </summary>
    public static void ClearPersistent()
    {
        if (instance == null) return;
        instance.RemovePersistent();
    }

    /// <summary>
    /// Adds a message to the queue and starts processing if not already doing so
    /// </summary>
    private void EnqueueMessage(string message, Color textColor)
    {
        // Create message data object
        MessageData messageData = new MessageData(message, textColor);

        // Add to queue
        messageQueue.Enqueue(messageData);

        // If not currently displaying a message, start the process
        if (!isDisplayingMessage)
        {
            ProcessNextMessage();
        }
    }

    /// <summary>
    /// Processes the next message in the queue if one exists
    /// </summary>
    private void ProcessNextMessage()
    {
        if (persistentActive) { isDisplayingMessage = false; return; }
        if (messageQueue.Count > 0)
        {
            MessageData nextMessage = messageQueue.Dequeue();
            // Start displaying this message
            StartCoroutine(DisplayCoroutine(nextMessage.Message, nextMessage.TextColor));
        }
        else
        {
            // No more messages to display
            isDisplayingMessage = false;
        }
    }

    /// <summary>
    /// Coroutine to display a message with fade effects
    /// </summary>
    private IEnumerator DisplayCoroutine(string message, Color textColor)
    {
        isDisplayingMessage = true;
        messageText.enabled = true;

        // Set up the message
        messageText.text = message;
        messageText.color = textColor;

        // Fade in
        yield return FadeCanvasGroup(canvasGroup, 0f, 1f, fadeDuration);

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration - (fadeDuration * 2));

        // Fade out
        yield return FadeCanvasGroup(canvasGroup, 1f, 0f, fadeDuration);

        messageText.enabled = false;

        // Process the next message in the queue if there is one
        ProcessNextMessage();
    }

    /// <summary>
    /// Fades a canvas group from one alpha value to another over a specified duration
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        cg.alpha = endAlpha;
    }

    /// <summary>
    /// Internal class to store message data in the queue
    /// </summary>
    private class MessageData
    {
        public string Message { get; private set; }
        public Color TextColor { get; private set; }

        public MessageData(string message, Color textColor)
        {
            Message = message;
            TextColor = textColor;
        }
    }

    private void SetPersistent(string message, Color textColor)
    {
        StopAllCoroutines();
        messageQueue.Clear();
        isDisplayingMessage = false;
        persistentActive = true;

        messageText.enabled = true;
        messageText.text = message;
        messageText.color = textColor;
        canvasGroup.alpha = 1f;
    }

    private void RemovePersistent()
    {
        persistentActive = false;
        messageText.text = "";
        messageText.enabled = false;
        canvasGroup.alpha = 0f;
    }

    private static bool IsNegativeColor(Color color)
    {
        return color.r >= 0.7f && color.g <= 0.4f;
    }

    private static bool IsPositiveColor(Color color)
    {
        return color.g >= 0.6f && color.b <= 0.6f;
    }
}
