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
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Queue<MessageData> messageQueue = new Queue<MessageData>();
    private bool isDisplayingMessage = false;

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
        if (!game.started || game.currentlyPlaying != game.player)
        {
            Debug.Log("Other player message: " + message);
            return;
        }
        instance.EnqueueMessage(message, color ?? Color.white);
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
}