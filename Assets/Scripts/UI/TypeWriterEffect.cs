using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterEffect : MonoBehaviour
{
    public TMP_Text textMeshPro;  // Assign in Inspector
    
    [TextArea] public string fullText; // The text to display
    
    public float typingSpeed = 0.05f; // Adjust speed

    public Coroutine coroutine;

    public bool startUponInstantiating = false;

    void Start()
    {
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

        for (int i = 0; i <= text.Length; i++)
        {
            textMeshPro.text = text.Substring(0, i);
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}
