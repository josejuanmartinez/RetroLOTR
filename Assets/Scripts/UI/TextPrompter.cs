using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
public class SmartTextPrompter : MonoBehaviour
{
    public TextMeshPro textMeshPro;
    public float charactersPerSecond = 10f;

    private string fullText;
    private float timer;
    private int charIndex;
    private bool shouldPrompt;

    public float updateInterval = 5f; // Wait 5 seconds between prompt cycles
    private float updateTimer;

    void Start()
    {
        if (textMeshPro == null)
            textMeshPro = GetComponent<TextMeshPro>();

        // Get and measure text
        fullText = textMeshPro.text;
        textMeshPro.ForceMeshUpdate();

        float visibleWidth = textMeshPro.rectTransform.rect.width;
        float textWidth = textMeshPro.preferredWidth;

        // Only scroll if text is too wide for the box
        shouldPrompt = textWidth > visibleWidth + 5f;

        if (shouldPrompt)
        {
            textMeshPro.text = "";
            timer = 0f;
            charIndex = 0;
        }
        else
        {
            // It fits — just show full text immediately
            textMeshPro.text = fullText;
        }
    }

    void Update()
    {
        float visibleWidth = textMeshPro.rectTransform.rect.width;
        float textWidth = textMeshPro.preferredWidth;

        // Only scroll if text is too wide for the box
        shouldPrompt = textWidth > visibleWidth + 5f;

        // If text fits or prompt finished — wait 5 seconds before restarting
        if (!shouldPrompt || charIndex >= fullText.Length)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                // Reset prompt cycle
                if (shouldPrompt)
                {
                    textMeshPro.text = "";
                    timer = 0f;
                    charIndex = 0;
                }
            }
            return;
        }

        // If prompting — update continuously (no waiting)
        timer += Time.deltaTime * charactersPerSecond;
        int newIndex = Mathf.FloorToInt(timer);
        if (newIndex != charIndex)
        {
            charIndex = Mathf.Clamp(newIndex, 0, fullText.Length);
            textMeshPro.text = fullText.Substring(0, charIndex);
        }
    }
}
