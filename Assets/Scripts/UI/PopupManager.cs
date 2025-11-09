using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("References")]
    public GameObject container;
    public Image actor1;
    public Image actor2;
    public TextMeshProUGUI textWidget;
    public TextMeshProUGUI titleWidget;
    public TypewriterEffect typeWriterEffect;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // optional: persists across scenes
    }

    public void Initialize(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite)
    {
        container.SetActive(true);

        if (typeWrite)
        {
            typeWriterEffect.enabled = true;
            typeWriterEffect.fullText = text;
            textWidget.text = "";
            typeWriterEffect.StartWriting();
        }
        else
        {
            typeWriterEffect.enabled = false;
            typeWriterEffect.fullText = "";
            textWidget.text = text;
        }

        titleWidget.text = title;
        actor1.sprite = spriteActor1;
        actor2.sprite = spriteActor2;
    }

    public void Hide()
    {
        FindFirstObjectByType<Sounds>().StopAllSounds();
        container.SetActive(false);
    }

    public static void Show(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite)
    => Instance.Initialize(title, spriteActor1, spriteActor2, text, typeWrite);

    public static void HidePopup()
        => Instance.Hide();
}
