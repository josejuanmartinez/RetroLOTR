using System.Collections;
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

    private Coroutine displayCoroutine;

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
        messageText.enabled = false;
    }

    /// <summary>
    /// Static method to display a message in the center of the screen for 3 seconds
    /// </summary>
    /// <param name="message">Text message to display</param>
    /// <param name="color">Color for the text (defaults to white if not specified)</param>
    public static void ShowMessage(string message, Color? color = null)
    {
        instance.Display(message, color ?? Color.white);
    }


    /// <summary>
    /// Internal method that handles the actual display
    /// </summary>
    private void Display(string message, Color textColor)
    {
        // Stop any running display coroutine
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);

        // Start the display coroutine
        displayCoroutine = StartCoroutine(DisplayCoroutine(message, textColor));
    }

    private IEnumerator DisplayCoroutine(string message, Color textColor)
    {
        messageText.enabled = true;
        // Set up the message
        messageText.text = message;
        messageText.color = textColor;

        yield return FadeCanvasGroup(canvasGroup, 0f, 1f, fadeDuration);

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration - (fadeDuration * 2));

        // Fade out
        yield return FadeCanvasGroup(canvasGroup, 1f, 0f, fadeDuration);

        displayCoroutine = null;
        messageText.enabled = false;
    }

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
}