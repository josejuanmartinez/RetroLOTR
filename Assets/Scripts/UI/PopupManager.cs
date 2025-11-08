using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    public GameObject container;
    public Image actor1;
    public Image actor2;
    public TextMeshProUGUI textWidget;
    public TextMeshProUGUI titleWidget;
    public TypewriterEffect typeWriterEffect;

    public void Initialize(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite)
    {
        container.SetActive(true);
        if(typeWrite)
        {
            typeWriterEffect.enabled = true;
            typeWriterEffect.fullText = text;
            textWidget.text = "";
            typeWriterEffect.StartWriting();
        } else
        {
            typeWriterEffect.fullText = "";
            typeWriterEffect.enabled=false;
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
}
