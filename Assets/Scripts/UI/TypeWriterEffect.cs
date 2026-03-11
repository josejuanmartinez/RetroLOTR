using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TypewriterEffect : MonoBehaviour
{
    public TMP_Text textMeshPro;
    public AutoScroll autoScroll;
    
    [TextArea] public string fullText;

    [Tooltip("Characters per second. Higher values type faster.")]
    public float typingSpeed = 20f;

    public Coroutine coroutine;

    public bool startUponInstantiating = false;

    void Start()
    {
        if(textMeshPro == null) textMeshPro = GetComponent<TextMeshPro>();
        if (fullText.Trim().Length == 0 && textMeshPro.text.Trim().Length > 0)
        {
            fullText = textMeshPro.text;
            textMeshPro.text = "";
        }
        if (startUponInstantiating) StartWriting(fullText);    
    }
    public void StartWriting(string text = null)
    {
        if (coroutine != null) StopCoroutine(coroutine);
        coroutine = StartCoroutine(TypeText(text));
    }

    IEnumerator TypeText(string text = null)
    {
        textMeshPro.text = ""; // Clear text initially

        if (text == null) text = fullText;
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        float charactersPerSecond = Mathf.Max(0.01f, typingSpeed);
        float visibleCharacters = 0f;
        int lastShownCount = -1;

        while (lastShownCount < text.Length)
        {
            visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
            int shownCount = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, text.Length);

            if (shownCount != lastShownCount)
            {
                textMeshPro.text = text.Substring(0, shownCount);
                autoScroll?.Refresh();
                lastShownCount = shownCount;
            }

            if (shownCount >= text.Length)
            {
                break;
            }

            yield return null;
        }
    }
}
